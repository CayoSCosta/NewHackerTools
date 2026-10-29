using System.Net;
using System.Text;
using HackerToolbox.Services;
using Microsoft.Extensions.Options;

namespace HackerToolbox.Tests;

[TestClass]
public sealed class ShopeeAffiliateApiClientTests
{
    private const string Payload = "{\"query\":\"{\\nbrandOffer{\\n    nodes{\\n        commissionRate\\n        offerName\\n    }\\n}\\n}\"}";

    [TestMethod]
    public void Signature_MatchesOfficialDocumentationVector()
    {
        var header = ShopeeAffiliateSigner.BuildAuthorizationHeader("123456", 1577836800, Payload, "demo");
        Assert.AreEqual(
            "SHA256 Credential=123456, Timestamp=1577836800, Signature=dc88d72feea70c80c52c3399751a7d34966763f51a7f056aa070a5e9df645412",
            header);
    }

    [TestMethod]
    public async Task TestConnection_SignsRequestAndParsesNodes()
    {
        var handler = new StubHandler(HttpStatusCode.OK, "{\"data\":{\"shopeeOfferV2\":{\"nodes\":[{\"commissionRate\":\"1\",\"offerName\":\"x\"}]}}}");
        var client = CreateClient(handler, "123456", "demo");

        var count = await client.TestConnectionAsync(CancellationToken.None);

        Assert.AreEqual(1, count);
        StringAssert.Contains(handler.Body!, "shopeeOfferV2");
        Assert.AreEqual(
            ShopeeAffiliateSigner.BuildAuthorizationHeader("123456", 1577836800, handler.Body!, "demo"),
            handler.Authorization);
        Assert.AreEqual(ShopeeAffiliateApiClient.Endpoint, handler.Uri);
    }

    [TestMethod]
    public async Task MissingConfiguration_Fails()
    {
        var client = CreateClient(new StubHandler(HttpStatusCode.OK, "{}"), "", "");
        await AssertFailure(client, ShopeeAffiliateFailure.NotConfigured);
    }

    [TestMethod]
    public async Task AuthErrorCode_MapsToAuthentication()
    {
        var body = "{\"errors\":[{\"message\":\"error [10020]: Invalid Authorization Header\",\"extensions\":{\"code\":10020}}]}";
        await AssertFailure(CreateClient(new StubHandler(HttpStatusCode.OK, body)), ShopeeAffiliateFailure.Authentication);
    }

    [TestMethod]
    public async Task OtherGraphQlError_MapsToApiError()
    {
        var body = "{\"errors\":[{\"message\":\"boom\",\"extensions\":{\"code\":99999}}]}";
        await AssertFailure(CreateClient(new StubHandler(HttpStatusCode.OK, body)), ShopeeAffiliateFailure.ApiError);
    }

    [TestMethod]
    public async Task RateLimitCode10030_MapsToRateLimited()
    {
        var body = "{\"errors\":[{\"message\":\"limit\",\"extensions\":{\"code\":10030,\"message\":\"too many\"}}]}";
        await AssertFailure(CreateClient(new StubHandler(HttpStatusCode.OK, body)), ShopeeAffiliateFailure.RateLimited);
    }

    [TestMethod]
    public async Task BusinessError11000_ExposesCodeAndMessageOnly()
    {
        var body = "{\"errors\":[{\"message\":\"overview\",\"extensions\":{\"code\":11000,\"message\":\"invalid origin url\"}}]}";
        var ex = await Assert.ThrowsExactlyAsync<ShopeeAffiliateException>(() =>
            CreateClient(new StubHandler(HttpStatusCode.OK, body)).TestConnectionAsync(CancellationToken.None));
        Assert.AreEqual(ShopeeAffiliateFailure.ApiError, ex.Failure);
        Assert.AreEqual("código 11000: invalid origin url", ex.Detail);
    }

    [TestMethod]
    public async Task MissingNodes_IsInvalidResponse()
    {
        await AssertFailure(CreateClient(new StubHandler(HttpStatusCode.OK, "{\"data\":{\"shopeeOfferV2\":{}}}")), ShopeeAffiliateFailure.InvalidResponse);
    }

    [TestMethod]
    public async Task NonJson_IsInvalidResponse()
    {
        await AssertFailure(CreateClient(new StubHandler(HttpStatusCode.OK, "<html>")), ShopeeAffiliateFailure.InvalidResponse);
    }

    [TestMethod]
    public async Task Http429_IsRateLimited()
    {
        await AssertFailure(CreateClient(new StubHandler(HttpStatusCode.TooManyRequests, "{}")), ShopeeAffiliateFailure.RateLimited);
    }

    [TestMethod]
    public async Task Http500Html_IsApiError()
    {
        await AssertFailure(CreateClient(new StubHandler(HttpStatusCode.InternalServerError, "oops")), ShopeeAffiliateFailure.ApiError);
    }

    [TestMethod]
    public async Task Timeout_IsMapped()
    {
        await AssertFailure(CreateClient(new StubHandler(throwTimeout: true)), ShopeeAffiliateFailure.Timeout);
    }

    [TestMethod]
    public async Task NetworkFailure_IsMapped()
    {
        await AssertFailure(CreateClient(new StubHandler(throwNetwork: true)), ShopeeAffiliateFailure.Network);
    }

    [TestMethod]
    [DataRow("https://shopee.com.br/produto-i.1.2", true)]
    [DataRow("https://s.shopee.com.br/abc", true)]
    [DataRow("http://shopee.com.br/x", false)]
    [DataRow("https://evil.com/shopee.com.br", false)]
    [DataRow("https://shopee.com.br.evil.com/x", false)]
    [DataRow("https://user@shopee.com.br/x", false)]
    [DataRow("not a url", false)]
    [DataRow("", false)]
    public void UrlValidator_RestrictsDomains(string input, bool expected)
    {
        Assert.AreEqual(expected, ShopeeProductUrlValidator.TryValidate(input, out _));
    }

    [TestMethod]
    public async Task Generate_Succeeds_AndSignsExactBody()
    {
        var handler = new StubHandler(HttpStatusCode.OK, "{\"data\":{\"generateShortLink\":{\"shortLink\":\"https://s.shopee.com.br/AbC123\"}}}");
        var client = CreateClient(handler);

        var link = await client.GenerateShortLinkAsync("https://shopee.com.br/produto-i.1.2", ["whatsapp_grupo"], CancellationToken.None);

        Assert.AreEqual("https://s.shopee.com.br/AbC123", link);
        StringAssert.Contains(handler.Body!, "generateShortLink");
        StringAssert.Contains(handler.Body!, "whatsapp_grupo");
        var expected = ShopeeAffiliateSigner.BuildAuthorizationHeader("123456", 1577836800, handler.Body!, "demo");
        Assert.AreEqual(expected, handler.Authorization);
    }

    [TestMethod]
    public async Task Generate_EscapesInjectionAttempts()
    {
        var handler = new StubHandler(HttpStatusCode.OK, "{\"data\":{\"generateShortLink\":{\"shortLink\":\"https://s.shopee.com.br/x\"}}}");
        var client = CreateClient(handler);
        await Assert.ThrowsExactlyAsync<ShopeeAffiliateException>(() =>
            client.GenerateShortLinkAsync("https://shopee.com.br/x", ["a\") { x } #"], CancellationToken.None));
        Assert.IsNull(handler.Body);
    }

    [TestMethod]
    [DataRow("not a url")]
    [DataRow("http://shopee.com.br/x")]
    [DataRow("https://evil.com/x")]
    [DataRow("javascript:alert(1)")]
    public async Task Generate_RejectsBadUrls(string url)
    {
        var handler = new StubHandler(HttpStatusCode.OK, "{}");
        var ex = await Assert.ThrowsExactlyAsync<ShopeeAffiliateException>(() =>
            CreateClient(handler).GenerateShortLinkAsync(url, null, CancellationToken.None));
        Assert.AreEqual(ShopeeAffiliateFailure.InvalidInput, ex.Failure);
        Assert.IsNull(handler.Body);
    }

    [TestMethod]
    public async Task Generate_RejectsTooManySubIds()
    {
        var ex = await Assert.ThrowsExactlyAsync<ShopeeAffiliateException>(() =>
            CreateClient(new StubHandler(HttpStatusCode.OK, "{}"))
                .GenerateShortLinkAsync("https://shopee.com.br/x", ["a", "b", "c", "d", "e", "f"], CancellationToken.None));
        Assert.AreEqual(ShopeeAffiliateFailure.InvalidInput, ex.Failure);
    }

    [TestMethod]
    [DataRow("{\"errors\":[{\"message\":\"boom\",\"extensions\":{\"code\":1}}]}", ShopeeAffiliateFailure.ApiError)]
    [DataRow("{\"data\":{\"generateShortLink\":{}}}", ShopeeAffiliateFailure.InvalidResponse)]
    [DataRow("{\"data\":{\"generateShortLink\":{\"shortLink\":\"\"}}}", ShopeeAffiliateFailure.InvalidResponse)]
    [DataRow("{\"data\":{\"generateShortLink\":{\"shortLink\":\"javascript:x\"}}}", ShopeeAffiliateFailure.InvalidResponse)]
    public async Task Generate_MapsBadResponses(string body, ShopeeAffiliateFailure expected)
    {
        var ex = await Assert.ThrowsExactlyAsync<ShopeeAffiliateException>(() =>
            CreateClient(new StubHandler(HttpStatusCode.OK, body))
                .GenerateShortLinkAsync("https://shopee.com.br/x", null, CancellationToken.None));
        Assert.AreEqual(expected, ex.Failure);
    }

    [TestMethod]
    public async Task Generate_TimeoutAndHttpFailure()
    {
        var t = await Assert.ThrowsExactlyAsync<ShopeeAffiliateException>(() =>
            CreateClient(new StubHandler(throwTimeout: true)).GenerateShortLinkAsync("https://shopee.com.br/x", null, CancellationToken.None));
        Assert.AreEqual(ShopeeAffiliateFailure.Timeout, t.Failure);
        var n = await Assert.ThrowsExactlyAsync<ShopeeAffiliateException>(() =>
            CreateClient(new StubHandler(throwNetwork: true)).GenerateShortLinkAsync("https://shopee.com.br/x", null, CancellationToken.None));
        Assert.AreEqual(ShopeeAffiliateFailure.Network, n.Failure);
    }

    [TestMethod]
    public void SubIdParser_SplitsAndValidates()
    {
        CollectionAssert.AreEqual(new[] { "a", "b" }, ShopeeSubIdValidator.Parse("a, b").ToArray());
        Assert.IsFalse(ShopeeSubIdValidator.IsValid(["bad id"]));
        Assert.IsTrue(ShopeeSubIdValidator.IsValid(["whatsapp_grupo"]));
    }

    [TestMethod]
    public async Task Search_ParsesOffers_AndSkipsUnsafeLinks()
    {
        var body = "{\"data\":{\"productOfferV2\":{\"nodes\":[" +
            "{\"productName\":\"A\",\"shopName\":\"S\",\"priceMin\":\"10\",\"priceMax\":\"12\",\"commissionRate\":\"0.1\",\"sales\":5,\"offerLink\":\"https://shope.ee/abc\",\"productLink\":\"https://shopee.com.br/p\",\"imageUrl\":\"javascript:x\"}," +
            "{\"productName\":\"B\",\"offerLink\":\"javascript:alert(1)\"}]," +
            "\"pageInfo\":{\"page\":1,\"hasNextPage\":true}}}}";
        var handler = new StubHandler(HttpStatusCode.OK, body);

        var page = await CreateClient(handler).SearchProductsAsync("tenis \"x\"", 2, 1, 10, CancellationToken.None);

        Assert.AreEqual(1, page.Offers.Count);
        Assert.AreEqual("https://shope.ee/abc", page.Offers[0].OfferLink);
        Assert.IsNull(page.Offers[0].ImageUrl);
        Assert.IsTrue(page.HasNextPage);
        using var payload = System.Text.Json.JsonDocument.Parse(handler.Body!);
        var query = payload.RootElement.GetProperty("query").GetString()!;
        StringAssert.Contains(query, "productOfferV2");
        StringAssert.Contains(query, "keyword: \"tenis \\u0022x\\u0022\"");
    }

    [TestMethod]
    [DataRow(0, 10, 2)]
    [DataRow(1, 21, 2)]
    [DataRow(1, 10, 9)]
    public void Search_RejectsInvalidParameters(int page, int limit, int sort)
    {
        var ex = Assert.ThrowsExactly<ShopeeAffiliateException>(() =>
            CreateClient(new StubHandler(HttpStatusCode.OK, "{}")).SearchProductsAsync(null, sort, page, limit, CancellationToken.None));
        Assert.AreEqual(ShopeeAffiliateFailure.InvalidInput, ex.Failure);
    }

    [TestMethod]
    public async Task ActivityLog_RecordsSuccessAndFailure_WithoutSecrets()
    {
        var activity = new ShopeeAffiliateActivityLog();
        var ok = new ShopeeAffiliateApiClient(
            new HttpClient(new StubHandler(HttpStatusCode.OK, "{\"data\":{\"shopeeOfferV2\":{\"nodes\":[]}}}")),
            Options.Create(new ShopeeAffiliateOptions { AppId = "123456", AppSecret = "demo-secret" }),
            TimeProvider.System, null, activity);
        await ok.TestConnectionAsync(CancellationToken.None);
        var bad = new ShopeeAffiliateApiClient(
            new HttpClient(new StubHandler(HttpStatusCode.OK, "{\"errors\":[{\"extensions\":{\"code\":10020}}]}")),
            Options.Create(new ShopeeAffiliateOptions { AppId = "123456", AppSecret = "demo-secret" }),
            TimeProvider.System, null, activity);
        await Assert.ThrowsExactlyAsync<ShopeeAffiliateException>(() => bad.TestConnectionAsync(CancellationToken.None));

        var entries = activity.Snapshot();
        Assert.AreEqual(2, entries.Count);
        Assert.IsFalse(entries[0].Success);
        Assert.IsTrue(entries[1].Success);
        Assert.IsFalse(entries.Any(e => e.Message.Contains("demo-secret") || e.Message.Contains("Signature")));
    }

    private static async Task AssertFailure(ShopeeAffiliateApiClient client, ShopeeAffiliateFailure expected)
    {
        var ex = await Assert.ThrowsExactlyAsync<ShopeeAffiliateException>(() => client.TestConnectionAsync(CancellationToken.None));
        Assert.AreEqual(expected, ex.Failure);
    }

    private static ShopeeAffiliateApiClient CreateClient(StubHandler handler, string appId = "123456", string secret = "demo") =>
        new(new HttpClient(handler),
            Options.Create(new ShopeeAffiliateOptions { AppId = appId, AppSecret = secret }),
            new FixedTime(DateTimeOffset.FromUnixTimeSeconds(1577836800)));

    private sealed class FixedTime(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    private sealed class StubHandler(HttpStatusCode status = HttpStatusCode.OK, string body = "", bool throwTimeout = false, bool throwNetwork = false) : HttpMessageHandler
    {
        public string? Body { get; private set; }
        public string? Authorization { get; private set; }
        public string? Uri { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (throwTimeout) throw new TaskCanceledException();
            if (throwNetwork) throw new HttpRequestException();
            Body = await request.Content!.ReadAsStringAsync(cancellationToken);
            Authorization = request.Headers.Authorization is null
                ? request.Headers.GetValues("Authorization").Single()
                : request.Headers.Authorization.ToString();
            Uri = request.RequestUri!.ToString();
            return new HttpResponseMessage(status) { Content = new StringContent(body, Encoding.UTF8, "application/json") };
        }
    }
}
