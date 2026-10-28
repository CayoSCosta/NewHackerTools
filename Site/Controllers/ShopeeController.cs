using HackerToolbox.Models;
using HackerToolbox.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace HackerToolbox.Controllers;

[Authorize(Roles = "Admin")]
public sealed class ShopeeController : Controller
{
    private readonly ShopeeApiClient _shopeeApi;
    private readonly ShopeeApiOptions _options;
    private readonly ILogger<ShopeeController> _logger;

    public ShopeeController(
        ShopeeApiClient shopeeApi,
        IOptions<ShopeeApiOptions> options,
        ILogger<ShopeeController> logger)
    {
        _shopeeApi = shopeeApi;
        _options = options.Value;
        _logger = logger;
    }

    [HttpGet]
    public async Task<IActionResult> Index([FromQuery] long offset = 0, CancellationToken cancellationToken = default)
    {
        if (offset < 0)
            return BadRequest();

        var model = new ShopeeDashboardViewModel
        {
            IsConfigured = _options.IsConfigured,
            Offset = offset
        };

        if (!model.IsConfigured)
            return View(model);

        try
        {
            return View(new ShopeeDashboardViewModel
            {
                IsConfigured = true,
                Offset = offset,
                ProductPage = await _shopeeApi.GetActiveProductsAsync(offset, cancellationToken)
            });
        }
        catch (ShopeeApiException exception)
        {
            _logger.LogWarning("Shopee product query failed: {Failure}", exception.Failure);
            return View(new ShopeeDashboardViewModel
            {
                IsConfigured = true,
                Offset = offset,
                ErrorMessage = GetSafeErrorMessage(exception.Failure)
            });
        }
    }

    private static string GetSafeErrorMessage(ShopeeApiFailure failure) => failure switch
    {
        ShopeeApiFailure.Authentication =>
            "A Shopee recusou a autenticação. Confira a autorização da loja e os tokens configurados.",
        ShopeeApiFailure.RateLimited =>
            "A Shopee limitou as consultas. Aguarde um momento antes de tentar novamente.",
        ShopeeApiFailure.Timeout =>
            "A Shopee demorou para responder. Tente novamente.",
        ShopeeApiFailure.Network =>
            "Não foi possível conectar à Shopee. Tente novamente mais tarde.",
        ShopeeApiFailure.InvalidResponse =>
            "A Shopee retornou uma resposta inválida. Tente novamente mais tarde.",
        _ =>
            "Não foi possível consultar os produtos da Shopee. Confira a configuração e tente novamente."
    };
}
