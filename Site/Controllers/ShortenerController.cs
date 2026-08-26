using HackerToolbox.Data;
using HackerToolbox.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HackerToolbox.Controllers
{
    public class ShortenerController : Controller
    {
        private readonly ApplicationDbContext _context;

        public ShortenerController(ApplicationDbContext context)
        {
            _context = context;
        }

        // Rota protegida: Painel para criar links curtos
        [Authorize]
        [HttpGet]
        public async Task<IActionResult> Index()
        {
            var urls = await _context.ShortUrls.OrderByDescending(u => u.CreatedAt).ToListAsync();
            return View(urls);
        }

        // Endpoint POST para encurtar URL
        [Authorize]
        [HttpPost]
        public async Task<IActionResult> Create(string originalUrl)
        {
            if (string.IsNullOrWhiteSpace(originalUrl))
            {
                TempData["Error"] = "Informe uma URL válida.";
                return RedirectToAction(nameof(Index));
            }

            // Garante que a URL tem http/https
            if (!originalUrl.StartsWith("http://") && !originalUrl.StartsWith("https://"))
            {
                originalUrl = "https://" + originalUrl;
            }

            // Gera um código único aleatório de 6 caracteres (alfanumérico)
            string code = Guid.NewGuid().ToString().Substring(0, 6);

            var shortUrl = new ShortUrl
            {
                OriginalUrl = originalUrl,
                Code = code
            };

            _context.ShortUrls.Add(shortUrl);
            await _context.SaveChangesAsync();

            TempData["Success"] = $"Link encurtado com sucesso! Código: {code}";
            return RedirectToAction(nameof(Index));
        }

        // Rota Pública: O redirecionador mágico (/s/{code})
        [HttpGet("s/{code}")]
        public async Task<IActionResult> RedirectToOriginal(string code)
        {
            if (string.IsNullOrWhiteSpace(code)) return NotFound();

            var shortUrl = await _context.ShortUrls.FirstOrDefaultAsync(u => u.Code == code);
            if (shortUrl == null)
            {
                return NotFound("Link encurtado não encontrado ou expirado.");
            }

            // Incrementa o contador de cliques de forma assíncrona
            shortUrl.ClickCount++;
            _context.Update(shortUrl);
            await _context.SaveChangesAsync();

            // Redireciona permanentemente/temporariamente para a URL original
            return Redirect(shortUrl.OriginalUrl);
        }
    }
}