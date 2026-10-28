using System.Net;
using System.Security.Cryptography;
using System.Text;
using HackerToolbox.Controllers;
using HackerToolbox.Models;
using HackerToolbox.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Logging.Abstractions;

namespace HackerToolbox.Tests;

[TestClass]
public sealed class ShopeeApiClientTests
{
    private const string PartnerKey = "test-partner-key";
    private const string AccessToken = "fictional-access-token";

    [TestMethod]
    public void CreateSignature_UsesDocumentedHmacSha256BaseString()
    {
        var signature = ShopeeRequestSigner.CreateSignature(
            123,
            "/api/v2/product/get_item_list",
            1_610_000_000,
            "fake-token",
            456,
            PartnerKey);

        Assert.AreEqual(
            "70720e350ac296bfa445f7c4515d0691bbb87216d208b2b1dc3042b777daff55",
            signature);
    }

    [TestMethod]
    public async Task ShopeeDashboard_IsAdminOnlyAndShowsSetupWithoutCallingTheApi()
    {
        var handler = new FakeHttpMessageHandler(_ => throw new AssertFailedException("A Shopee API call was not expected."));
        var options = Options.Create(new ShopeeApiOptions());
        using var api = new ShopeeApiClient(new HttpClient(handler), options);
        var controller = new ShopeeController(api, options, NullLogger<ShopeeController>.Instance);

        var result = await controller.Index();

        var view = result as ViewResult;
        Assert.IsNotNull(view);
        var model = view.Model as ShopeeDashboardViewModel;
        Assert.IsNotNull(model);
        Assert.IsFalse(model.IsConfigured);
        Assert.AreEqual(0, handler.RequestUris.Count);

        var authorize = typeof(ShopeeController)
            .GetCustomAttributes(typeof(AuthorizeAttribute), true)
            .Cast<AuthorizeAttribute>()
            .SingleOrDefault();
        Assert.IsNotNull(authorize);
        Assert.AreEqual("Admin", authorize.Roles);
    }

    [TestMethod]
    public async Task GetActiveProductsAsync_SignsReadOnlyRequestAndParsesProductPage()
    {
        var handler = new FakeHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(
                """{"error":"","response":{"item":[],"total_count":0,"has_next_page":false,"next_offset":0}}""",
                Encoding.UTF8,
                "application/json")
        });
        using var client = CreateClient(handler);

        var page = await client.GetActiveProductsAsync(0, CancellationToken.None);

        Assert.AreEqual(0, page.Products.Count);
        Assert.AreEqual(0, page.TotalCount);
        Assert.IsFalse(page.HasNextPage);

        var requestUri = handler.RequestUris[0];
        var query = System.Web.HttpUtility.ParseQueryString(requestUri.Query);
        Assert.AreEqual("/api/v2/product/get_item_list", requestUri.AbsolutePath);
        Assert.AreEqual("NORMAL", query["item_status"]);
        Assert.AreEqual("50", query["page_size"]);
        Assert.AreEqual("123", query["partner_id"]);
        Assert.AreEqual("456", query["shop_id"]);
        Assert.AreEqual(AccessToken, query["access_token"]);

        var timestamp = long.Parse(query["timestamp"]!);
        var expectedSignature = ShopeeRequestSigner.CreateSignature(
            123,
            requestUri.AbsolutePath,
            timestamp,
            AccessToken,
            456,
            PartnerKey);
        Assert.AreEqual(expectedSignature, query["sign"]);
    }

    [TestMethod]
    public async Task GetActiveProductsAsync_FetchesProductDetailsReadOnly()
    {
        var responses = new Queue<HttpResponseMessage>(
        [
            JsonResponse("""{"error":"","response":{"item":[{"item_id":987,"item_status":"NORMAL","update_time":1700000000}],"total_count":55,"has_next_page":true,"next_offset":50}}"""),
            JsonResponse("""{"error":"","response":{"item_list":[{"item_id":987,"item_name":"Produto fictício","item_sku":"SKU-TEST","item_status":"NORMAL","price_info":[{"currency":"BRL","current_price":19.9}],"stock_info_v2":{"summary_info":{"total_available_stock":7}}}]}}""")
        ]);
        var handler = new FakeHttpMessageHandler(_ => responses.Dequeue());
        using var client = CreateClient(handler);

        var page = await client.GetActiveProductsAsync(0, CancellationToken.None);

        Assert.AreEqual(1, page.Products.Count);
        Assert.AreEqual(55L, page.TotalCount);
        Assert.IsTrue(page.HasNextPage);
        Assert.AreEqual(50L, page.NextOffset);
        Assert.AreEqual("Produto fictício", page.Products[0].Name);
        Assert.AreEqual("SKU-TEST", page.Products[0].Sku);
        Assert.AreEqual("BRL", page.Products[0].Currency);
        Assert.AreEqual(19.9m, page.Products[0].Price);
        Assert.AreEqual(7L, page.Products[0].AvailableStock);
        Assert.AreEqual(1_700_000_000L, page.Products[0].UpdateTime);
        Assert.AreEqual(2, handler.RequestUris.Count);
        Assert.IsTrue(handler.RequestTimes[1] - handler.RequestTimes[0] >= TimeSpan.FromMilliseconds(900));
        Assert.AreEqual("/api/v2/product/get_item_base_info", handler.RequestUris[1].AbsolutePath);
        Assert.AreEqual("[987]", System.Web.HttpUtility.ParseQueryString(handler.RequestUris[1].Query)["item_id_list"]);
    }

    [TestMethod]
    public async Task GetActiveProductsAsync_MapsShopeeRateLimitWithoutLeakingResponse()
    {
        const string privateResponse = """{"error":"error_rate_limit","message":"fake-secret-in-response"}""";
        var handler = new FakeHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(privateResponse, Encoding.UTF8, "application/json")
        });
        using var client = CreateClient(handler);

        var exception = await Assert.ThrowsAsync<ShopeeApiException>(
            () => client.GetActiveProductsAsync(0, CancellationToken.None));

        Assert.AreEqual(ShopeeApiFailure.RateLimited, exception.Failure);
        Assert.IsFalse(exception.Message.Contains(AccessToken, StringComparison.Ordinal));
        Assert.IsFalse(exception.Message.Contains("fake-secret-in-response", StringComparison.Ordinal));
    }

    [TestMethod]
    public async Task GetActiveProductsAsync_MapsAuthenticationFailureWithoutLeakingResponse()
    {
        var handler = new FakeHttpMessageHandler(_ => JsonResponse(
            """{"error":"error_auth","message":"fake-secret-in-response"}"""));
        using var client = CreateClient(handler);

        var exception = await Assert.ThrowsAsync<ShopeeApiException>(
            () => client.GetActiveProductsAsync(0, CancellationToken.None));

        Assert.AreEqual(ShopeeApiFailure.Authentication, exception.Failure);
        Assert.IsFalse(exception.Message.Contains(AccessToken, StringComparison.Ordinal));
        Assert.IsFalse(exception.Message.Contains("fake-secret-in-response", StringComparison.Ordinal));
    }

    [TestMethod]
    public async Task GetActiveProductsAsync_MapsInvalidJsonToSanitizedFailure()
    {
        var handler = new FakeHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("not-json", Encoding.UTF8, "application/json")
        });
        using var client = CreateClient(handler);

        var exception = await Assert.ThrowsAsync<ShopeeApiException>(
            () => client.GetActiveProductsAsync(0, CancellationToken.None));

        Assert.AreEqual(ShopeeApiFailure.InvalidResponse, exception.Failure);
        Assert.IsFalse(exception.Message.Contains(AccessToken, StringComparison.Ordinal));
    }

    [TestMethod]
    public async Task GetActiveProductsAsync_MapsHttpFailureWithoutExposingRequestUrl()
    {
        var handler = new FakeHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.InternalServerError)
        {
            Content = new StringContent(AccessToken, Encoding.UTF8, "text/plain")
        });
        using var client = CreateClient(handler);

        var exception = await Assert.ThrowsAsync<ShopeeApiException>(
            () => client.GetActiveProductsAsync(0, CancellationToken.None));

        Assert.AreEqual(ShopeeApiFailure.RemoteError, exception.Failure);
        Assert.IsFalse(exception.Message.Contains(AccessToken, StringComparison.Ordinal));
    }

    [TestMethod]
    public async Task GetActiveProductsAsync_MapsTimeoutToSanitizedFailure()
    {
        var handler = new FakeHttpMessageHandler(async (_, cancellationToken) =>
        {
            await Task.Delay(TimeSpan.FromSeconds(5), cancellationToken);
            return new HttpResponseMessage(HttpStatusCode.OK);
        });
        var httpClient = new HttpClient(handler) { Timeout = TimeSpan.FromMilliseconds(50) };
        using var client = new ShopeeApiClient(
            httpClient,
            Options.Create(new ShopeeApiOptions
            {
                PartnerId = 123,
                PartnerKey = PartnerKey,
                ShopId = 456,
                AccessToken = AccessToken
            }));

        var exception = await Assert.ThrowsAsync<ShopeeApiException>(
            () => client.GetActiveProductsAsync(0, CancellationToken.None));

        Assert.AreEqual(ShopeeApiFailure.Timeout, exception.Failure);
        Assert.IsFalse(exception.Message.Contains(AccessToken, StringComparison.Ordinal));
    }

    private static ShopeeApiClient CreateClient(FakeHttpMessageHandler handler)
    {
        var httpClient = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(5) };
        var options = Options.Create(new ShopeeApiOptions
        {
            PartnerId = 123,
            PartnerKey = PartnerKey,
            ShopId = 456,
            AccessToken = AccessToken
        });
        return new ShopeeApiClient(httpClient, options);
    }

    private sealed class FakeHttpMessageHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> _responseFactory;

        public FakeHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> responseFactory)
            : this((request, _) => Task.FromResult(responseFactory(request)))
        {
        }

        public FakeHttpMessageHandler(
            Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> responseFactory)
        {
            _responseFactory = responseFactory;
        }

        public List<Uri> RequestUris { get; } = [];
        public List<DateTimeOffset> RequestTimes { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            RequestUris.Add(request.RequestUri!);
            RequestTimes.Add(DateTimeOffset.UtcNow);
            return _responseFactory(request, cancellationToken);
        }
    }

    private static HttpResponseMessage JsonResponse(string json) => new(HttpStatusCode.OK)
    {
        Content = new StringContent(json, Encoding.UTF8, "application/json")
    };
}
