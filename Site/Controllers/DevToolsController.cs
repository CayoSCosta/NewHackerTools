using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Text.Json;
using System.Text;
using System.Security.Cryptography;
using System.Net;
using System.Net.Sockets;
using System.Collections.Concurrent;
using System.Security.Claims;

namespace HackerToolbox.Controllers
{
    [Authorize]
    public class DevToolsController : Controller
    {
        // Armazena túneis TCP ativos na memória para monitoramento (prefixados com userId)
        private static readonly ConcurrentDictionary<string, CancellationTokenSource> _activeTunnels = new();

        // Porta padrão do protocolo RDP
        private const int RdpPort = 3389;

        // Necessário para localizar a pasta privada onde o instalador do agente fica salvo
        private readonly IWebHostEnvironment _env;

        public DevToolsController(IWebHostEnvironment env)
        {
            _env = env;
        }

        // Recupera o ID único do operador logado atualmente
        private string GetCurrentUserId() => User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "anonymous";

        // Rota: /DevTools/JsonJwtTool
        [HttpGet]
        public IActionResult JsonJwtTool()
        {
            return View();
        }

        // Rota: /DevTools/HashesECripto
        [HttpGet]
        public IActionResult HashesECripto()
        {
            return View();
        }

        // Rota: /DevTools/TuneladorTcp
        [HttpGet]
        public IActionResult TuneladorTcp()
        {
            var userId = GetCurrentUserId();
            // Filtra apenas os túneis TCP pertencentes ao usuário logado atual
            ViewBag.ActiveTunnels = _activeTunnels.Keys
                .Where(k => k.StartsWith($"{userId}_") && !k.EndsWith($":{RdpPort}"))
                .Select(k => k.Substring(userId.Length + 1)) // Remove o prefixo do ID para exibição
                .ToList();
            return View();
        }

        // Rota: /DevTools/DownloadAgent
        [HttpGet]
        public IActionResult DownloadAgent()
        {
            var filePath = Path.Combine(_env.ContentRootPath, "PrivateFiles", "RubinotTunnelAgent.exe");

            if (!System.IO.File.Exists(filePath))
            {
                TempData["Error"] = "Agente ainda não disponível para download. Contate o administrador.";
                return RedirectToAction(nameof(TuneladorTcp));
            }

            const string fileName = "RubinotTunnelAgent.exe";
            const string contentType = "application/octet-stream";

            return PhysicalFile(filePath, contentType, fileName);
        }

        [HttpPost]
        public IActionResult StartTunnel(int localPort, string targetHost, int targetPort)
        {
            if (localPort <= 0 || string.IsNullOrWhiteSpace(targetHost) || targetPort <= 0)
            {
                TempData["Error"] = "Parâmetros inválidos para iniciar o túnel.";
                return RedirectToAction(nameof(TuneladorTcp));
            }

            var userId = GetCurrentUserId();
            string tunnelKey = $"{userId}_0.0.0.0:{localPort} -> {targetHost}:{targetPort}";

            // Evita conflito apenas nas portas do próprio usuário
            if (_activeTunnels.Keys.Any(k => k.StartsWith($"{userId}_0.0.0.0:{localPort}")))
            {
                TempData["Error"] = "Você já possui um túnel ativo nesta porta local.";
                return RedirectToAction(nameof(TuneladorTcp));
            }

            var cts = new CancellationTokenSource();
            _ = Task.Run(() => RunTcpProxyServerAsync(localPort, targetHost, targetPort, cts.Token));

            _activeTunnels.TryAdd(tunnelKey, cts);
            TempData["Success"] = $"Túnel TCP iniciado com sucesso na porta {localPort}!";

            return RedirectToAction(nameof(TuneladorTcp));
        }

        [HttpPost]
        public IActionResult StopTunnel(int localPort)
        {
            var userId = GetCurrentUserId();
            var targetKey = _activeTunnels.Keys
                .FirstOrDefault(k => k.StartsWith($"{userId}_0.0.0.0:{localPort}"));

            if (targetKey != null && _activeTunnels.TryRemove(targetKey, out var cts))
            {
                cts.Cancel();
                cts.Dispose();
                TempData["Success"] = $"Túnel da porta {localPort} encerrado.";
            }
            else
            {
                TempData["Error"] = "Túnel não encontrado ou não pertence a você.";
            }

            return RedirectToAction(nameof(TuneladorTcp));
        }

        // =========================================================
        // MÓDULO: ACESSO REMOTO VIA RDP (ISOLADO POR USUÁRIO)
        // =========================================================

        [HttpGet]
        public IActionResult AcessoRemotoRdp()
        {
            var userId = GetCurrentUserId();
            var rdpTunnels = _activeTunnels.Keys
                .Where(k => k.StartsWith($"{userId}_") && k.EndsWith($":{RdpPort}"))
                .Select(k => {
                    var cleanKey = k.Substring(userId.Length + 1);
                    var parts = cleanKey.Split(' ');
                    return parts.Length > 0 ? parts[0].Replace("0.0.0.0:", "") : cleanKey;
                })
                .ToList();

            ViewBag.RdpTunnels = rdpTunnels;
            return View();
        }

        [HttpPost]
        public IActionResult IniciarAcessoRdp(int localPort, string targetHost)
        {
            if (localPort <= 0 || string.IsNullOrWhiteSpace(targetHost))
            {
                TempData["Error"] = "Informe a porta local e o host de destino.";
                return RedirectToAction(nameof(AcessoRemotoRdp));
            }

            var userId = GetCurrentUserId();
            string tunnelKey = $"{userId}_0.0.0.0:{localPort} -> {targetHost}:{RdpPort}";

            if (_activeTunnels.Keys.Any(k => k.StartsWith($"{userId}_0.0.0.0:{localPort}")))
            {
                TempData["Error"] = "Já existe um túnel RDP ativo nesta porta local sob sua responsabilidade.";
                return RedirectToAction(nameof(AcessoRemotoRdp));
            }

            var cts = new CancellationTokenSource();
            _ = Task.Run(() => RunTcpProxyServerAsync(localPort, targetHost, RdpPort, cts.Token));

            _activeTunnels.TryAdd(tunnelKey, cts);
            TempData["Success"] = $"Túnel RDP iniciado na porta local {localPort}. Baixe o arquivo .rdp para conectar.";

            return RedirectToAction(nameof(AcessoRemotoRdp));
        }

        [HttpPost]
        public IActionResult PararAcessoRdp(int localPort)
        {
            var userId = GetCurrentUserId();
            var targetKey = _activeTunnels.Keys
                .FirstOrDefault(k => k.StartsWith($"{userId}_0.0.0.0:{localPort}") && k.EndsWith($":{RdpPort}"));

            if (targetKey != null && _activeTunnels.TryRemove(targetKey, out var cts))
            {
                cts.Cancel();
                cts.Dispose();
                TempData["Success"] = $"Túnel RDP da porta {localPort} encerrado.";
            }
            else
            {
                TempData["Error"] = "Túnel RDP não encontrado ou não pertence a você.";
            }

            return RedirectToAction(nameof(AcessoRemotoRdp));
        }

        [HttpGet]
        public IActionResult DownloadRdpFile(int localPort)
        {
            if (localPort <= 0)
            {
                TempData["Error"] = "Porta local inválida.";
                return RedirectToAction(nameof(AcessoRemotoRdp));
            }

            var userId = GetCurrentUserId();
            var isActive = _activeTunnels.Keys.Any(k => k.StartsWith($"{userId}_0.0.0.0:{localPort}") && k.EndsWith($":{RdpPort}"));

            if (!isActive)
            {
                TempData["Error"] = "Não há túnel RDP ativo nessa porta local sob sua responsabilidade.";
                return RedirectToAction(nameof(AcessoRemotoRdp));
            }

            var rdpContent = new StringBuilder();
            rdpContent.AppendLine($"full address:s:127.0.0.1:{localPort}");
            rdpContent.AppendLine("prompt for credentials:i:1");
            rdpContent.AppendLine("administrative session:i:0");
            rdpContent.AppendLine("screen mode id:i:2");
            rdpContent.AppendLine("use multimon:i:0");
            rdpContent.AppendLine("desktopwidth:i:1920");
            rdpContent.AppendLine("desktopheight:i:1080");
            rdpContent.AppendLine("session bpp:i:32");
            rdpContent.AppendLine("compression:i:1");
            rdpContent.AppendLine("audiomode:i:0");
            rdpContent.AppendLine("authentication level:i:2");
            rdpContent.AppendLine("connect to console:i:0");
            rdpContent.AppendLine("networkautodetect:i:1");
            rdpContent.AppendLine("bandwidthautodetect:i:1");

            var bytes = Encoding.UTF8.GetBytes(rdpContent.ToString());
            var fileName = $"acesso-rdp-{localPort}.rdp";

            return File(bytes, "application/x-rdp", fileName);
        }

        // =========================================================
        // MOTOR DE PROXY TCP (CAMADA 4)
        // =========================================================
        private async Task RunTcpProxyServerAsync(int localPort, string targetHost, int targetPort, CancellationToken cancellationToken)
        {
            var listener = new TcpListener(IPAddress.Any, localPort);
            listener.Start();

            try
            {
                while (!cancellationToken.IsCancellationRequested)
                {
                    var clientSocket = await listener.AcceptTcpClientAsync(cancellationToken);

                    _ = Task.Run(async () =>
                    {
                        using (clientSocket)
                        {
                            using var targetClient = new TcpClient();
                            try
                            {
                                await targetClient.ConnectAsync(targetHost, targetPort, cancellationToken);

                                using var clientStream = clientSocket.GetStream();
                                using var targetStream = targetClient.GetStream();

                                var forwardClientToTarget = clientStream.CopyToAsync(targetStream, cancellationToken);
                                var forwardTargetToClient = targetStream.CopyToAsync(clientStream, cancellationToken);

                                await Task.WhenAny(forwardClientToTarget, forwardTargetToClient);
                            }
                            catch
                            {
                                // Silencia falhas de pipe em conexões encerradas abruptamente
                            }
                        }
                    }, cancellationToken);
                }
            }
            catch (OperationCanceledException)
            {
                // Parada controlada
            }
            finally
            {
                listener.Stop();
            }
        }
    }

    public class JsonRequestModel { public string Input { get; set; } }
    public class JwtRequestModel { public string Token { get; set; } }
    public class HashRequestModel { public string Input { get; set; } }
}