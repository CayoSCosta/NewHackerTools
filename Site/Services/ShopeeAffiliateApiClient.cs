using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;

namespace HackerToolbox.Services;

public enum ShopeeAffiliateFailure
{
    NotConfigured,
    InvalidInput,
    Authentication,
    RateLimited,
    Timeout,
    Network,
    ApiError,
    InvalidResponse
}

public sealed class ShopeeAffiliateException : Exception
{
    public ShopeeAffiliateException(ShopeeAffiliateFailure failure, string? detail = null) : base(failure.ToString())
    {
        Failure = failure;
        Detail = detail;
    }

    public ShopeeAffiliateFailure Failure { get; }

    // Safe, truncated diagnostic from the remote API (error code/message or HTTP status); never contains credentials.
    public string? Detail { get; }
}

public static class ShopeeAffiliateSigner
{
    // Documented: SHA256(AppId + Timestamp + Payload + Secret), lowercase hex.
    public static string ComputeSignature(string appId, long timestamp, string payload, string secret)
    {
        var factor = string.Concat(appId, timestamp.ToString(System.Globalization.CultureInfo.InvariantCulture), payload, secret);
        return Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(factor)));
    }

    public static string BuildAuthorizationHeader(string appId, long timestamp, string payload, string secret) =>
        $"SHA256 Credential={appId}, Timestamp={timestamp}, Signature={ComputeSignature(appId, timestamp, payload, secret)}";
}

public static class ShopeeProductUrlValidator
{
    private static readonly string[] AllowedHosts =
        ["shopee.com.br", "www.shopee.com.br", "s.shopee.com.br"];

    public static bool TryValidate(string? input, out Uri? uri)
    {
        uri = null;
        if (string.IsNullOrWhiteSpace(input) || input.Length > 2048)
            return false;
        if (!Uri.TryCreate(input.Trim(), UriKind.Absolute, out var parsed))
            return false;
        if (parsed.Scheme != Uri.UriSchemeHttps || !string.IsNullOrEmpty(parsed.UserInfo))
            return false;
        if (!AllowedHosts.Contains(parsed.Host, StringComparer.OrdinalIgnoreCase))
            return false;
        uri = parsed;
        return true;
    }
}

public static class ShopeeSubIdValidator
{
    public const int MaxSubIds = 5;

    // The max length/charset are conservative local limits, not confirmed official ones.
    public static bool IsValid(IReadOnlyList<string> subIds) =>
        subIds.Count <= MaxSubIds &&
        subIds.All(s => s.Length is >= 1 and <= 50 &&
            s.All(c => char.IsAsciiLetterOrDigit(c) || c is '_' or '-'));

    public static IReadOnlyList<string> Parse(string? raw) =>
        (raw ?? string.Empty).Split([',', ';', '\n', '\r', ' '], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
}

public sealed record ShopeeAffiliateLogEntry(DateTimeOffset Time, string Operation, bool Success, string Message, long ElapsedMs);

// In-memory ring buffer of recent API results for the admin screen. Holds no credentials, signatures or headers.
public sealed class ShopeeAffiliateActivityLog
{
    private const int Capacity = 50;
    private readonly object _gate = new();
    private readonly LinkedList<ShopeeAffiliateLogEntry> _entries = new();

    public void Add(ShopeeAffiliateLogEntry entry)
    {
        lock (_gate)
        {
            _entries.AddFirst(entry);
            while (_entries.Count > Capacity)
                _entries.RemoveLast();
        }
    }

    public IReadOnlyList<ShopeeAffiliateLogEntry> Snapshot()
    {
        lock (_gate)
            return _entries.ToList();
    }
}

public sealed record ShopeeOffer(
    string? Name, string? ShopName, string? ImageUrl, string? PriceMin, string? PriceMax,
    int? DiscountRate, string? CommissionRate, string? Rating, int? Sales, string? ProductLink, string? OfferLink);

public sealed record ShopeeOfferPage(IReadOnlyList<ShopeeOffer> Offers, int Page, bool HasNextPage);

public sealed class ShopeeAffiliateApiClient
{
    public const string Endpoint = "https://open-api.affiliate.shopee.com.br/graphql";
    private const int MaxResponseBytes = 1024 * 1024;
    // Official error codes: 10020 = identity authentication (signature incorrect/expired), 10030 = traffic limiting.
    private const int AuthenticationErrorCode = 10020;
    private const int RateLimitErrorCode = 10030;

    private readonly HttpClient _httpClient;
    private readonly ShopeeAffiliateOptions _options;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<ShopeeAffiliateApiClient>? _logger;
    private readonly ShopeeAffiliateActivityLog? _activity;

    public ShopeeAffiliateApiClient(
        HttpClient httpClient,
        IOptions<ShopeeAffiliateOptions> options,
        TimeProvider timeProvider,
        ILogger<ShopeeAffiliateApiClient>? logger = null,
        ShopeeAffiliateActivityLog? activity = null)
    {
        _httpClient = httpClient;
        _options = options.Value;
        _timeProvider = timeProvider;
        _logger = logger;
        _activity = activity;
    }

    public bool IsConfigured => _options.IsConfigured;

    // Runs an operation and records a highly visible log line (console + admin panel) for its outcome.
    private async Task<T> LoggedAsync<T>(string operation, Func<Task<T>> run, Func<T, string> summarize)
    {
        var started = System.Diagnostics.Stopwatch.GetTimestamp();
        try
        {
            var result = await run();
            Record(operation, true, summarize(result), started);
            return result;
        }
        catch (ShopeeAffiliateException ex)
        {
            Record(operation, false, $"{ex.Failure}{(string.IsNullOrWhiteSpace(ex.Detail) ? "" : " | " + ex.Detail)}", started);
            throw;
        }
        catch (OperationCanceledException)
        {
            Record(operation, false, "Cancelado", started);
            throw;
        }
    }

    private void Record(string operation, bool success, string message, long started)
    {
        var elapsed = (long)System.Diagnostics.Stopwatch.GetElapsedTime(started).TotalMilliseconds;
        _activity?.Add(new ShopeeAffiliateLogEntry(_timeProvider.GetUtcNow(), operation, success, message, elapsed));
        if (success)
            _logger?.LogInformation("✅✅✅ [SHOPEE-AFFILIATE] {Operation} SUCESSO em {Elapsed}ms => {Message}", operation, elapsed, message);
        else
            _logger?.LogWarning("❌❌❌ [SHOPEE-AFFILIATE] {Operation} FALHA em {Elapsed}ms => {Message}", operation, elapsed, message);
    }

    // Authenticated read-only call using the official shopeeOfferV2 query (brandOffer/offerV1 is deprecated).
    public Task<int> TestConnectionAsync(CancellationToken cancellationToken) =>
        LoggedAsync("testConnection", async () =>
        {
            using var data = await SendAsync("{\nshopeeOfferV2(page: 1, limit: 1){\n    nodes{\n        offerName\n        commissionRate\n    }\n}\n}", cancellationToken);
            if (!data.RootElement.TryGetProperty("shopeeOfferV2", out var offer) ||
                offer.ValueKind != JsonValueKind.Object ||
                !offer.TryGetProperty("nodes", out var nodes) ||
                nodes.ValueKind != JsonValueKind.Array)
                throw new ShopeeAffiliateException(ShopeeAffiliateFailure.InvalidResponse);
            return nodes.GetArrayLength();
        }, n => $"autenticação OK, {n} oferta(s) de amostra");

    // Searches productOfferV2 (read-only). Each offer already carries the affiliate offerLink.
    public Task<ShopeeOfferPage> SearchProductsAsync(string? keyword, int sortType, int page, int limit, CancellationToken cancellationToken)
    {
        if (page < 1 || limit is < 1 or > 20 || sortType is < 2 or > 5 ||
            (keyword is not null && keyword.Length > 100))
            throw new ShopeeAffiliateException(ShopeeAffiliateFailure.InvalidInput);

        var keywordArg = string.IsNullOrWhiteSpace(keyword) ? string.Empty : $"keyword: {JsonSerializer.Serialize(keyword.Trim())}, ";
        var query = "{\nproductOfferV2(" + keywordArg + $"sortType: {sortType}, page: {page}, limit: {limit}) {{\n" +
            "  nodes {\n    productName\n    shopName\n    imageUrl\n    priceMin\n    priceMax\n    priceDiscountRate\n    commissionRate\n    ratingStar\n    sales\n    productLink\n    offerLink\n  }\n" +
            "  pageInfo {\n    page\n    hasNextPage\n  }\n}\n}";

        return LoggedAsync("productOfferV2", async () =>
        {
            using var data = await SendAsync(query, cancellationToken);
            if (!data.RootElement.TryGetProperty("productOfferV2", out var root) || root.ValueKind != JsonValueKind.Object ||
                !root.TryGetProperty("nodes", out var nodes) || nodes.ValueKind != JsonValueKind.Array)
                throw new ShopeeAffiliateException(ShopeeAffiliateFailure.InvalidResponse);

            var offers = new List<ShopeeOffer>();
            foreach (var n in nodes.EnumerateArray())
            {
                if (n.ValueKind != JsonValueKind.Object)
                    continue;
                var offerLink = Str(n, "offerLink");
                if (offerLink is null || !Uri.TryCreate(offerLink, UriKind.Absolute, out var u) || u.Scheme != Uri.UriSchemeHttps)
                    continue;
                offers.Add(new ShopeeOffer(
                    Str(n, "productName"), Str(n, "shopName"), SafeHttps(Str(n, "imageUrl")),
                    Str(n, "priceMin"), Str(n, "priceMax"), Int(n, "priceDiscountRate"),
                    Str(n, "commissionRate"), Str(n, "ratingStar"), Int(n, "sales"),
                    SafeHttps(Str(n, "productLink")), u.AbsoluteUri));
            }

            var hasNext = root.TryGetProperty("pageInfo", out var pi) && pi.ValueKind == JsonValueKind.Object &&
                pi.TryGetProperty("hasNextPage", out var h) && h.ValueKind == JsonValueKind.True;
            return new ShopeeOfferPage(offers, page, hasNext);
        }, p => $"{p.Offers.Count} oferta(s) na página {p.Page}, próxima página: {(p.HasNextPage ? "sim" : "não")}");
    }

    private static string? Str(JsonElement e, string name) =>
        e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;

    private static int? Int(JsonElement e, string name) =>
        e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.Number && v.TryGetInt32(out var i) ? i : null;

    private static string? SafeHttps(string? url) =>
        url is not null && Uri.TryCreate(url, UriKind.Absolute, out var u) && u.Scheme == Uri.UriSchemeHttps ? u.AbsoluteUri : null;

    public Task<string> GenerateShortLinkAsync(string? originUrl, IReadOnlyList<string>? subIds, CancellationToken cancellationToken)
    {
        if (!ShopeeProductUrlValidator.TryValidate(originUrl, out var uri) || uri is null)
            throw new ShopeeAffiliateException(ShopeeAffiliateFailure.InvalidInput);
        subIds ??= [];
        if (!ShopeeSubIdValidator.IsValid(subIds))
            throw new ShopeeAffiliateException(ShopeeAffiliateFailure.InvalidInput);

        // Inputs are validated and emitted as JSON-escaped string literals (GraphQL-compatible).
        var subIdList = string.Join(", ", subIds.Select(s => JsonSerializer.Serialize(s)));
        var subIdArgument = subIds.Count > 0 ? $", subIds: [{subIdList}]" : string.Empty;
        var query = $"mutation {{\n  generateShortLink(input: {{originUrl: {JsonSerializer.Serialize(uri.AbsoluteUri)}{subIdArgument}}}) {{\n    shortLink\n  }}\n}}";

        return LoggedAsync("generateShortLink", async () =>
        {
            using var data = await SendAsync(query, cancellationToken);
            if (!data.RootElement.TryGetProperty("generateShortLink", out var result) ||
                result.ValueKind != JsonValueKind.Object ||
                !result.TryGetProperty("shortLink", out var link) ||
                link.ValueKind != JsonValueKind.String)
                throw new ShopeeAffiliateException(ShopeeAffiliateFailure.InvalidResponse);

            var shortLink = link.GetString();
            if (string.IsNullOrWhiteSpace(shortLink) || shortLink.Length > 2048 ||
                !Uri.TryCreate(shortLink, UriKind.Absolute, out var shortUri) ||
                shortUri.Scheme != Uri.UriSchemeHttps)
                throw new ShopeeAffiliateException(ShopeeAffiliateFailure.InvalidResponse);
            return shortUri.AbsoluteUri;
        }, l => $"link gerado: {l}");
    }

    // Returns the "data" element of a GraphQL response after validating HTTP and GraphQL errors.
    internal async Task<JsonDocument> SendAsync(string query, CancellationToken cancellationToken)
    {
        if (!_options.IsConfigured)
            throw new ShopeeAffiliateException(ShopeeAffiliateFailure.NotConfigured);

        var payload = JsonSerializer.Serialize(new Dictionary<string, string> { ["query"] = query });
        var timestamp = _timeProvider.GetUtcNow().ToUnixTimeSeconds();
        using var request = new HttpRequestMessage(HttpMethod.Post, Endpoint)
        {
            Content = new StringContent(payload, new UTF8Encoding(false), "application/json")
        };
        request.Headers.TryAddWithoutValidation("Authorization",
            ShopeeAffiliateSigner.BuildAuthorizationHeader(_options.AppId.Trim(), timestamp, payload, _options.AppSecret.Trim()));

        string body;
        try
        {
            using var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            if (response.StatusCode == HttpStatusCode.TooManyRequests)
                throw new ShopeeAffiliateException(ShopeeAffiliateFailure.RateLimited);
            if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
                throw new ShopeeAffiliateException(ShopeeAffiliateFailure.Authentication);
            body = await ReadLimitedAsync(response, cancellationToken);
            if (!response.IsSuccessStatusCode && !body.TrimStart().StartsWith('{'))
                throw new ShopeeAffiliateException(ShopeeAffiliateFailure.ApiError, $"HTTP {(int)response.StatusCode}");
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw new ShopeeAffiliateException(ShopeeAffiliateFailure.Timeout);
        }
        catch (HttpRequestException)
        {
            throw new ShopeeAffiliateException(ShopeeAffiliateFailure.Network);
        }

        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(body);
        }
        catch (JsonException)
        {
            throw new ShopeeAffiliateException(ShopeeAffiliateFailure.InvalidResponse);
        }

        using (document)
        {
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
                throw new ShopeeAffiliateException(ShopeeAffiliateFailure.InvalidResponse);

            if (root.TryGetProperty("errors", out var errors) && errors.ValueKind == JsonValueKind.Array && errors.GetArrayLength() > 0)
            {
                foreach (var error in errors.EnumerateArray())
                {
                    var code = GetErrorCode(error);
                    if (code == AuthenticationErrorCode)
                        throw new ShopeeAffiliateException(ShopeeAffiliateFailure.Authentication);
                    if (code == RateLimitErrorCode)
                        throw new ShopeeAffiliateException(ShopeeAffiliateFailure.RateLimited);
                }
                throw new ShopeeAffiliateException(ShopeeAffiliateFailure.ApiError, DescribeError(errors[0]));
            }

            if (!root.TryGetProperty("data", out var data) || data.ValueKind != JsonValueKind.Object)
                throw new ShopeeAffiliateException(ShopeeAffiliateFailure.InvalidResponse);

            return JsonDocument.Parse(data.GetRawText());
        }
    }

    private static int? GetErrorCode(JsonElement error) =>
        error.ValueKind == JsonValueKind.Object &&
        error.TryGetProperty("extensions", out var ext) && ext.ValueKind == JsonValueKind.Object &&
        ext.TryGetProperty("code", out var code) && code.ValueKind == JsonValueKind.Number &&
        code.TryGetInt32(out var c) ? c : null;

    // Builds "código N: descrição" from extensions.code / extensions.message (falling back to message).
    private static string? DescribeError(JsonElement error)
    {
        if (error.ValueKind != JsonValueKind.Object)
            return null;
        string? text = null;
        if (error.TryGetProperty("extensions", out var ext) && ext.ValueKind == JsonValueKind.Object &&
            ext.TryGetProperty("message", out var em) && em.ValueKind == JsonValueKind.String)
            text = em.GetString();
        if (string.IsNullOrWhiteSpace(text) && error.TryGetProperty("message", out var m) && m.ValueKind == JsonValueKind.String)
            text = m.GetString();
        if (text is { Length: > 200 })
            text = text[..200];
        var code = GetErrorCode(error);
        return code is null ? text : $"código {code}: {text}";
    }

    private static async Task<string> ReadLimitedAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var buffer = new MemoryStream();
        var chunk = new byte[8192];
        int read;
        while ((read = await stream.ReadAsync(chunk, cancellationToken)) > 0)
        {
            if (buffer.Length + read > MaxResponseBytes)
                throw new ShopeeAffiliateException(ShopeeAffiliateFailure.InvalidResponse);
            buffer.Write(chunk, 0, read);
        }
        return Encoding.UTF8.GetString(buffer.GetBuffer(), 0, (int)buffer.Length);
    }
}
