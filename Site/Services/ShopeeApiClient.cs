using System.Diagnostics;
using System.Globalization;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;

namespace HackerToolbox.Services;

public enum ShopeeApiFailure
{
    NotConfigured,
    Authentication,
    RateLimited,
    Timeout,
    Network,
    RemoteError,
    InvalidResponse
}

public sealed class ShopeeApiException : Exception
{
    public ShopeeApiException(ShopeeApiFailure failure) : base(failure.ToString())
    {
        Failure = failure;
    }

    public ShopeeApiFailure Failure { get; }
}

public sealed record ShopeeProduct(
    long ItemId,
    string Name,
    string? Sku,
    string Status,
    string? Currency,
    decimal? Price,
    long? AvailableStock,
    long UpdateTime);

public sealed record ShopeeProductPage(
    IReadOnlyList<ShopeeProduct> Products,
    long TotalCount,
    bool HasNextPage,
    long NextOffset);

public sealed class ShopeeApiClient : IDisposable
{
    private const string ApiHost = "https://partner.shopeemobile.com";
    private const string ItemListPath = "/api/v2/product/get_item_list";
    private const string ItemBaseInfoPath = "/api/v2/product/get_item_base_info";
    private const int PageSize = 50;
    private const int MaxResponseBytes = 2 * 1024 * 1024;
    private static readonly TimeSpan MinimumRequestInterval = TimeSpan.FromSeconds(1);

    private readonly HttpClient _httpClient;
    private readonly ShopeeApiOptions _options;
    private readonly SemaphoreSlim _requestGate = new(1, 1);
    private long _lastRequestTimestamp;
    private bool _hasSentRequest;

    public ShopeeApiClient(HttpClient httpClient, IOptions<ShopeeApiOptions> options)
    {
        _httpClient = httpClient;
        _options = options.Value;
    }

    public async Task<ShopeeProductPage> GetActiveProductsAsync(long offset, CancellationToken cancellationToken)
    {
        EnsureConfigured();
        if (offset < 0)
            throw new ArgumentOutOfRangeException(nameof(offset));

        var listJson = await SendGetAsync(
            ItemListPath,
            [
                new("item_status", "NORMAL"),
                new("offset", offset.ToString(CultureInfo.InvariantCulture)),
                new("page_size", PageSize.ToString(CultureInfo.InvariantCulture))
            ],
            cancellationToken);

        var listedItems = ParseItemList(listJson);
        if (listedItems.Items.Count == 0)
        {
            return new ShopeeProductPage(
                Array.Empty<ShopeeProduct>(),
                listedItems.TotalCount,
                listedItems.HasNextPage,
                listedItems.NextOffset);
        }

        var itemIds = "[" + string.Join(",", listedItems.Items.Select(item =>
            item.ItemId.ToString(CultureInfo.InvariantCulture))) + "]";
        var detailsJson = await SendGetAsync(
            ItemBaseInfoPath,
            [new("item_id_list", itemIds)],
            cancellationToken);

        var products = ParseProductDetails(detailsJson, listedItems.Items);
        return new ShopeeProductPage(
            products,
            listedItems.TotalCount,
            listedItems.HasNextPage,
            listedItems.NextOffset);
    }

    public void Dispose()
    {
        _httpClient.Dispose();
        _requestGate.Dispose();
    }

    private void EnsureConfigured()
    {
        if (!_options.IsConfigured)
            throw new ShopeeApiException(ShopeeApiFailure.NotConfigured);
    }

    private async Task<string> SendGetAsync(
        string path,
        IReadOnlyList<KeyValuePair<string, string>> parameters,
        CancellationToken cancellationToken)
    {
        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var signature = ShopeeRequestSigner.CreateSignature(
            _options.PartnerId,
            path,
            timestamp,
            _options.AccessToken,
            _options.ShopId,
            _options.PartnerKey);

        var queryParameters = new List<KeyValuePair<string, string>>(parameters)
        {
            new("partner_id", _options.PartnerId.ToString(CultureInfo.InvariantCulture)),
            new("timestamp", timestamp.ToString(CultureInfo.InvariantCulture)),
            new("access_token", _options.AccessToken),
            new("shop_id", _options.ShopId.ToString(CultureInfo.InvariantCulture)),
            new("sign", signature)
        };
        var query = string.Join("&", queryParameters.Select(parameter =>
            $"{Uri.EscapeDataString(parameter.Key)}={Uri.EscapeDataString(parameter.Value)}"));
        using var request = new HttpRequestMessage(HttpMethod.Get, $"{ApiHost}{path}?{query}");

        await _requestGate.WaitAsync(cancellationToken);
        try
        {
            if (_hasSentRequest)
            {
                var elapsed = Stopwatch.GetElapsedTime(_lastRequestTimestamp);
                if (elapsed < MinimumRequestInterval)
                    await Task.Delay(MinimumRequestInterval - elapsed, cancellationToken);
            }

            _lastRequestTimestamp = Stopwatch.GetTimestamp();
            _hasSentRequest = true;

            using var response = await _httpClient.SendAsync(
                request,
                HttpCompletionOption.ResponseHeadersRead,
                cancellationToken);
            if (response.StatusCode == HttpStatusCode.TooManyRequests)
                throw new ShopeeApiException(ShopeeApiFailure.RateLimited);
            if (!response.IsSuccessStatusCode)
            {
                var failure = response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden
                    ? ShopeeApiFailure.Authentication
                    : ShopeeApiFailure.RemoteError;
                throw new ShopeeApiException(failure);
            }

            var body = await ReadBoundedResponseAsync(response, cancellationToken);
            CheckApiError(body);
            return body;
        }
        catch (ShopeeApiException)
        {
            throw;
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw new ShopeeApiException(ShopeeApiFailure.Timeout);
        }
        catch (HttpRequestException)
        {
            throw new ShopeeApiException(ShopeeApiFailure.Network);
        }
        catch (IOException)
        {
            throw new ShopeeApiException(ShopeeApiFailure.Network);
        }
        finally
        {
            _requestGate.Release();
        }
    }

    private static async Task<string> ReadBoundedResponseAsync(
        HttpResponseMessage response,
        CancellationToken cancellationToken)
    {
        if (response.Content.Headers.ContentLength > MaxResponseBytes)
            throw new ShopeeApiException(ShopeeApiFailure.InvalidResponse);

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var buffer = new MemoryStream();
        var chunk = new byte[8192];
        int bytesRead;
        while ((bytesRead = await stream.ReadAsync(chunk, cancellationToken)) > 0)
        {
            if (buffer.Length + bytesRead > MaxResponseBytes)
                throw new ShopeeApiException(ShopeeApiFailure.InvalidResponse);

            await buffer.WriteAsync(chunk.AsMemory(0, bytesRead), cancellationToken);
        }

        return Encoding.UTF8.GetString(buffer.ToArray());
    }

    private static void CheckApiError(string json)
    {
        try
        {
            using var document = JsonDocument.Parse(json, new JsonDocumentOptions { MaxDepth = 32 });
            if (document.RootElement.ValueKind != JsonValueKind.Object)
                throw new ShopeeApiException(ShopeeApiFailure.InvalidResponse);

            if (document.RootElement.TryGetProperty("error", out var error) &&
                error.ValueKind == JsonValueKind.String &&
                !string.IsNullOrWhiteSpace(error.GetString()) &&
                error.GetString() != "-")
            {
                var failure = error.GetString() switch
                {
                    "error_rate_limit" or "error_limit" => ShopeeApiFailure.RateLimited,
                    "error_auth" or "error_sign" or "invalid_access_token" or "invalid_acceess_token" =>
                        ShopeeApiFailure.Authentication,
                    _ => ShopeeApiFailure.RemoteError
                };
                throw new ShopeeApiException(failure);
            }
        }
        catch (Exception exception) when (exception is JsonException or InvalidOperationException)
        {
            throw new ShopeeApiException(ShopeeApiFailure.InvalidResponse);
        }
    }

    private static (IReadOnlyList<ListedItem> Items, long TotalCount, bool HasNextPage, long NextOffset)
        ParseItemList(string json)
    {
        try
        {
            using var document = JsonDocument.Parse(json, new JsonDocumentOptions { MaxDepth = 32 });
            var response = document.RootElement.GetProperty("response");
            var items = response.GetProperty("item")
                .EnumerateArray()
                .Select(item => new ListedItem(
                    item.GetProperty("item_id").GetInt64(),
                    item.GetProperty("item_status").GetString() ?? string.Empty,
                    item.GetProperty("update_time").GetInt64()))
                .ToArray();

            return (
                items,
                response.GetProperty("total_count").GetInt64(),
                response.GetProperty("has_next_page").GetBoolean(),
                response.GetProperty("next_offset").GetInt64());
        }
        catch (Exception exception) when (exception is JsonException or InvalidOperationException or KeyNotFoundException or FormatException or OverflowException)
        {
            throw new ShopeeApiException(ShopeeApiFailure.InvalidResponse);
        }
    }

    private static IReadOnlyList<ShopeeProduct> ParseProductDetails(string json, IReadOnlyList<ListedItem> listedItems)
    {
        try
        {
            using var document = JsonDocument.Parse(json, new JsonDocumentOptions { MaxDepth = 32 });
            var detailsById = document.RootElement.GetProperty("response")
                .GetProperty("item_list")
                .EnumerateArray()
                .Select(ParseProduct)
                .ToDictionary(product => product.ItemId);

            return listedItems
                .Where(item => detailsById.ContainsKey(item.ItemId))
                .Select(item => detailsById[item.ItemId] with { UpdateTime = item.UpdateTime })
                .ToArray();
        }
        catch (Exception exception) when (exception is JsonException or InvalidOperationException or KeyNotFoundException or FormatException or ArgumentException or OverflowException)
        {
            throw new ShopeeApiException(ShopeeApiFailure.InvalidResponse);
        }
    }

    private static ShopeeProduct ParseProduct(JsonElement item)
    {
        var priceInfo = item.GetProperty("price_info");
        string? currency = null;
        decimal? price = null;
        if (priceInfo.ValueKind == JsonValueKind.Array && priceInfo.GetArrayLength() > 0)
        {
            var firstPrice = priceInfo[0];
            currency = firstPrice.TryGetProperty("currency", out var currencyElement)
                ? currencyElement.GetString()
                : null;
            price = firstPrice.TryGetProperty("current_price", out var priceElement) &&
                    priceElement.TryGetDecimal(out var parsedPrice)
                ? parsedPrice
                : null;
        }

        long? stock = null;
        if (item.TryGetProperty("stock_info_v2", out var stockInfo) &&
            stockInfo.TryGetProperty("summary_info", out var summary) &&
            summary.TryGetProperty("total_available_stock", out var stockElement) &&
            stockElement.TryGetInt64(out var parsedStock))
        {
            stock = parsedStock;
        }

        return new ShopeeProduct(
            item.GetProperty("item_id").GetInt64(),
            item.GetProperty("item_name").GetString() ?? string.Empty,
            item.TryGetProperty("item_sku", out var sku) ? sku.GetString() : null,
            item.TryGetProperty("item_status", out var status) ? status.GetString() ?? string.Empty : string.Empty,
            currency,
            price,
            stock,
            0);
    }

    private sealed record ListedItem(long ItemId, string Status, long UpdateTime);
}

public static class ShopeeRequestSigner
{
    public static string CreateSignature(
        long partnerId,
        string apiPath,
        long timestamp,
        string accessToken,
        long shopId,
        string partnerKey)
    {
        var baseString = string.Concat(
            partnerId.ToString(CultureInfo.InvariantCulture),
            apiPath,
            timestamp.ToString(CultureInfo.InvariantCulture),
            accessToken,
            shopId.ToString(CultureInfo.InvariantCulture));
        var hash = HMACSHA256.HashData(
            Encoding.UTF8.GetBytes(partnerKey),
            Encoding.UTF8.GetBytes(baseString));
        return Convert.ToHexStringLower(hash);
    }
}
