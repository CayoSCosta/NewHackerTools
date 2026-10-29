using HackerToolbox.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HackerToolbox.Controllers;

public sealed class ShopeeAffiliateViewModel
{
    public bool IsConfigured { get; set; }
    public bool? ConnectionOk { get; set; }
    public string? Message { get; set; }
    public string? OriginUrl { get; set; }
    public string? SubIds { get; set; } = "whatsapp_grupo";
    public string? ShortLink { get; set; }
    public bool? GenerateOk { get; set; }
    public string? GenerateMessage { get; set; }
    public string? Keyword { get; set; }
    public int SortType { get; set; } = 2;
    public int Page { get; set; } = 1;
    public ShopeeOfferPage? Offers { get; set; }
    public bool? SearchOk { get; set; }
    public string? SearchMessage { get; set; }
}

[Authorize(Roles = "Admin")]
public sealed class ShopeeAffiliateController : Controller
{
    private readonly ShopeeAffiliateApiClient _client;

    public ShopeeAffiliateController(ShopeeAffiliateApiClient client)
    {
        _client = client;
    }

    [HttpGet]
    public IActionResult Index() => View(new ShopeeAffiliateViewModel { IsConfigured = _client.IsConfigured });

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Search(string? keyword, int sortType, int page, CancellationToken cancellationToken)
    {
        var model = new ShopeeAffiliateViewModel
        {
            IsConfigured = _client.IsConfigured,
            Keyword = keyword,
            SortType = sortType is >= 2 and <= 5 ? sortType : 2,
            Page = page < 1 ? 1 : page
        };
        try
        {
            model.Offers = await _client.SearchProductsAsync(keyword, model.SortType, model.Page, 10, cancellationToken);
            model.SearchOk = true;
            model.SearchMessage = model.Offers.Offers.Count == 0 ? "Nenhuma oferta encontrada." : $"{model.Offers.Offers.Count} oferta(s) encontrada(s).";
        }
        catch (ShopeeAffiliateException ex)
        {
            model.SearchOk = false;
            model.SearchMessage = ex.Failure switch
            {
                ShopeeAffiliateFailure.InvalidInput => "Busca inválida: use até 100 caracteres e uma ordenação válida.",
                ShopeeAffiliateFailure.NotConfigured => "Credenciais de afiliado não configuradas.",
                ShopeeAffiliateFailure.Authentication => "Autenticação recusada. Verifique App ID e App Secret.",
                ShopeeAffiliateFailure.RateLimited => "Limite de requisições atingido. Tente novamente mais tarde.",
                ShopeeAffiliateFailure.Timeout => "A Shopee não respondeu a tempo.",
                ShopeeAffiliateFailure.Network => "Falha de rede ao contatar a Shopee.",
                ShopeeAffiliateFailure.InvalidResponse => "Resposta inesperada da Shopee.",
                _ => "A API de afiliados retornou um erro." + (string.IsNullOrWhiteSpace(ex.Detail) ? "" : " Detalhe: " + ex.Detail)
            };
        }
        return View("Index", model);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> ConvertLink([FromForm] string? originUrl, [FromForm] string? subIds, CancellationToken cancellationToken)
    {
        try
        {
            var link = await _client.GenerateShortLinkAsync(originUrl, ShopeeSubIdValidator.Parse(subIds), cancellationToken);
            return Json(new { ok = true, link });
        }
        catch (ShopeeAffiliateException ex)
        {
            var message = ex.Failure switch
            {
                ShopeeAffiliateFailure.InvalidInput => "Link ou SubIds inválidos.",
                ShopeeAffiliateFailure.NotConfigured => "Credenciais não configuradas.",
                ShopeeAffiliateFailure.Authentication => "Autenticação recusada.",
                ShopeeAffiliateFailure.RateLimited => "Limite de requisições atingido.",
                ShopeeAffiliateFailure.Timeout => "Tempo esgotado.",
                ShopeeAffiliateFailure.Network => "Falha de rede.",
                ShopeeAffiliateFailure.InvalidResponse => "Resposta sem link utilizável.",
                _ => "Erro da API" + (string.IsNullOrWhiteSpace(ex.Detail) ? "." : ": " + ex.Detail)
            };
            return Json(new { ok = false, message, rateLimited = ex.Failure == ShopeeAffiliateFailure.RateLimited });
        }
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Generate(string? originUrl, string? subIds, CancellationToken cancellationToken)
    {
        var model = new ShopeeAffiliateViewModel
        {
            IsConfigured = _client.IsConfigured,
            OriginUrl = originUrl,
            SubIds = subIds
        };
        try
        {
            model.ShortLink = await _client.GenerateShortLinkAsync(originUrl, ShopeeSubIdValidator.Parse(subIds), cancellationToken);
            model.GenerateOk = true;
            model.GenerateMessage = "Link de afiliado gerado.";
        }
        catch (ShopeeAffiliateException ex)
        {
            model.GenerateOk = false;
            model.GenerateMessage = ex.Failure switch
            {
                ShopeeAffiliateFailure.InvalidInput => "Entrada inválida: use um link https de shopee.com.br e até 5 SubIds (letras, números, _ ou -, até 50 caracteres).",
                ShopeeAffiliateFailure.NotConfigured => "Credenciais de afiliado não configuradas.",
                ShopeeAffiliateFailure.Authentication => "Autenticação recusada. Verifique App ID e App Secret.",
                ShopeeAffiliateFailure.RateLimited => "Limite de requisições atingido. Tente novamente mais tarde.",
                ShopeeAffiliateFailure.Timeout => "A Shopee não respondeu a tempo.",
                ShopeeAffiliateFailure.Network => "Falha de rede ao contatar a Shopee.",
                ShopeeAffiliateFailure.InvalidResponse => "A Shopee não retornou um link utilizável.",
                _ => "A API de afiliados recusou a geração do link."
            };
            if (ex.Failure == ShopeeAffiliateFailure.ApiError && !string.IsNullOrWhiteSpace(ex.Detail))
                model.GenerateMessage += " Detalhe: " + ex.Detail;
        }
        return View("Index", model);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> TestConnection(CancellationToken cancellationToken)
    {
        var model = new ShopeeAffiliateViewModel { IsConfigured = _client.IsConfigured };
        try
        {
            await _client.TestConnectionAsync(cancellationToken);
            model.ConnectionOk = true;
            model.Message = "Autenticação na Affiliate Open API validada.";
        }
        catch (ShopeeAffiliateException ex)
        {
            model.ConnectionOk = false;
            model.Message = ex.Failure switch
            {
                ShopeeAffiliateFailure.NotConfigured => "Credenciais de afiliado não configuradas.",
                ShopeeAffiliateFailure.Authentication => "Autenticação recusada. Verifique App ID e App Secret.",
                ShopeeAffiliateFailure.RateLimited => "Limite de requisições atingido. Tente novamente mais tarde.",
                ShopeeAffiliateFailure.Timeout => "A Shopee não respondeu a tempo.",
                ShopeeAffiliateFailure.Network => "Falha de rede ao contatar a Shopee.",
                ShopeeAffiliateFailure.InvalidResponse => "Resposta inesperada da Shopee.",
                _ => "A API de afiliados retornou um erro."
            };
            if (ex.Failure == ShopeeAffiliateFailure.ApiError && !string.IsNullOrWhiteSpace(ex.Detail))
                model.Message += " Detalhe: " + ex.Detail;
        }
        return View("Index", model);
    }
}
