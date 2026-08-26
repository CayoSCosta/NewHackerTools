using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using HackerToolbox.Models;

namespace HackerToolbox.Controllers;

public class HomeController : Controller
{
    private readonly ILogger<HomeController> _logger;

    public HomeController(ILogger<HomeController> logger)
    {
        _logger = logger;
    }

    // A rota raiz "/" vai cair aqui
    public IActionResult Index()
    {
        // Se o usuário NÃO estiver logado, redireciona direto para a tela de Login do Identity
        if (!User.Identity.IsAuthenticated)
        {
            return Redirect("/Identity/Account/Login");
        }

        // Se estiver logado, mantém na Index normal
        return View();
    }

    public IActionResult Privacy()
    {
        return View();
    }

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }
}
