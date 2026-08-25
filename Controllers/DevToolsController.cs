// // using Microsoft.AspNetCore.Authorization;
// // using Microsoft.AspNetCore.Mvc;
// // using System.Text.Json;
// // using System.Text;
// // using System.Security.Cryptography;

// // namespace HackerToolbox.Controllers
// // {
// //     [Authorize]
// //     public class DevToolsController : Controller
// //     {
// //         // Rota: /DevTools/JsonJwtTool
// //         [HttpGet]
// //         public IActionResult JsonJwtTool()
// //         {
// //             return View();
// //         }

// //         // Rota: /DevTools/HashesECripto
// //         [HttpGet]
// //         public IActionResult HashesECripto()
// //         {
// //             return View();
// //         }

// //         // Rota: /DevTools/TuneladorTcp
// //         [HttpGet]
// //         public IActionResult TuneladorTcp()
// //         {
// //             return View();
// //         }

// //         // Endpoint AJAX para Validar e Formatar JSON
// //         [HttpPost]
// //         public IActionResult FormatJson([FromBody] JsonRequestModel model)
// //         {
// //             if (string.IsNullOrWhiteSpace(model?.Input))
// //             {
// //                 return Json(new { success = false, message = "Input vazio." });
// //             }

// //             try
// //             {
// //                 using var doc = JsonDocument.Parse(model.Input);
// //                 var prettyJson = JsonSerializer.Serialize(doc.RootElement, new JsonSerializerOptions
// //                 {
// //                     WriteIndented = true
// //                 });

// //                 return Json(new { success = true, result = prettyJson });
// //             }
// //             catch (JsonException ex)
// //             {
// //                 return Json(new { success = false, message = $"Erro de sintaxe JSON: {ex.Message}" });
// //             }
// //         }

// //         // Endpoint AJAX para Decodificar JWT
// //         [HttpPost]
// //         public IActionResult DecodeJwt([FromBody] JwtRequestModel model)
// //         {
// //             if (string.IsNullOrWhiteSpace(model?.Token))
// //             {
// //                 return Json(new { success = false, message = "Token JWT vazio." });
// //             }

// //             try
// //             {
// //                 var parts = model.Token.Split('.');
// //                 if (parts.Length != 3)
// //                 {
// //                     return Json(new { success = false, message = "Token JWT inválido. Um JWT deve conter exatamente 3 partes separadas por pontos (.)" });
// //                 }

// //                 var headerJson = DecodeBase64Url(parts[0]);
// //                 var payloadJson = DecodeBase64Url(parts[1]);
// //                 var signature = parts[2];

// //                 var formattedHeader = FormatJsonString(headerJson);
// //                 var formattedPayload = FormatJsonString(payloadJson);

// //                 return Json(new
// //                 {
// //                     success = true,
// //                     header = formattedHeader,
// //                     payload = formattedPayload,
// //                     signature = signature
// //                 });
// //             }
// //             catch (Exception ex)
// //             {
// //                 return Json(new { success = false, message = $"Erro ao decodificar o token: {ex.Message}" });
// //             }
// //         }

// //         // Endpoint AJAX para calcular múltiplos Hashes e Base64
// //         [HttpPost]
// //         public IActionResult ProcessHashes([FromBody] HashRequestModel model)
// //         {
// //             if (string.IsNullOrEmpty(model?.Input))
// //             {
// //                 return Json(new { success = false, message = "Texto de entrada vazio." });
// //             }

// //             try
// //             {
// //                 var inputBytes = Encoding.UTF8.GetBytes(model.Input);

// //                 // 1. Cálculo de Base64 (Codificação)
// //                 var base64Encoded = Convert.ToBase64String(inputBytes);

// //                 // 2. Cálculo de MD5
// //                 string md5Hash;
// //                 using (var md5 = MD5.Create())
// //                 {
// //                     var hashBytes = md5.ComputeHash(inputBytes);
// //                     md5Hash = BitConverter.ToString(hashBytes).Replace("-", "").ToLowerInvariant();
// //                 }

// //                 // 3. Cálculo de SHA256
// //                 string sha256Hash;
// //                 using (var sha256 = SHA256.Create())
// //                 {
// //                     var hashBytes = sha256.ComputeHash(inputBytes);
// //                     sha256Hash = BitConverter.ToString(hashBytes).Replace("-", "").ToLowerInvariant();
// //                 }

// //                 // 4. Cálculo de SHA512
// //                 string sha512Hash;
// //                 using (var sha512 = SHA512.Create())
// //                 {
// //                     var hashBytes = sha512.ComputeHash(inputBytes);
// //                     sha512Hash = BitConverter.ToString(hashBytes).Replace("-", "").ToLowerInvariant();
// //                 }

// //                 return Json(new
// //                 {
// //                     success = true,
// //                     base64 = base64Encoded,
// //                     md5 = md5Hash,
// //                     sha256 = sha256Hash,
// //                     sha512 = sha512Hash
// //                 });
// //             }
// //             catch (Exception ex)
// //             {
// //                 return Json(new { success = false, message = $"Erro ao processar hashes: {ex.Message}" });
// //             }
// //         }

// //         private string DecodeBase64Url(string base64Url)
// //         {
// //             string base64 = base64Url.Replace('-', '+').Replace('_', '/');
// //             switch (base64.Length % 4)
// //             {
// //                 case 2: base64 += "=="; break;
// //                 case 3: base64 += "="; break;
// //             }
// //             var bytes = Convert.FromBase64String(base64);
// //             return Encoding.UTF8.GetString(bytes);
// //         }

// //         private string FormatJsonString(string json)
// //         {
// //             try
// //             {
// //                 using var doc = JsonDocument.Parse(json);
// //                 return JsonSerializer.Serialize(doc.RootElement, new JsonSerializerOptions { WriteIndented = true });
// //             }
// //             catch
// //             {
// //                 return json;
// //             }
// //         }
// //     }

// //     public class JsonRequestModel
// //     {
// //         public string Input { get; set; }
// //     }

// //     public class JwtRequestModel
// //     {
// //         public string Token { get; set; }
// //     }

// //     public class HashRequestModel
// //     {
// //         public string Input { get; set; }
// //     }

// // }
// using Microsoft.AspNetCore.Authorization;
// using Microsoft.AspNetCore.Mvc;
// using System.Text.Json;
// using System.Text;
// using System.Security.Cryptography;
// using System.Net;
// using System.Net.Sockets;
// using System.Collections.Concurrent;

// namespace HackerToolbox.Controllers
// {
//     [Authorize]
//     public class DevToolsController : Controller
//     {
//         // Armazena túneis TCP ativos na memória para monitoramento
//         private static readonly ConcurrentDictionary<string, CancellationTokenSource> _activeTunnels = new();

//         // Rota: /DevTools/JsonJwtTool
//         [HttpGet]
//         public IActionResult JsonJwtTool()
//         {
//             return View();
//         }

//         // Rota: /DevTools/HashesECripto
//         [HttpGet]
//         public IActionResult HashesECripto()
//         {
//             return View();
//         }

//         // Rota: /DevTools/TuneladorTcp
//         [HttpGet]
//         public IActionResult TuneladorTcp()
//         {
//             ViewBag.ActiveTunnels = _activeTunnels.Keys.ToList();
//             return View();
//         }

//         // Endpoint POST para iniciar o redirecionamento de túnel TCP (Ex: Rubinot)
//         [HttpPost]
//         public IActionResult StartTunnel(int localPort, string targetHost, int targetPort)
//         {
//             if (localPort <= 0 || string.IsNullOrWhiteSpace(targetHost) || targetPort <= 0)
//             {
//                 TempData["Error"] = "Parâmetros inválidos para iniciar o túnel.";
//                 return RedirectToAction(nameof(TuneladorTcp));
//             }

//             string tunnelKey = $"0.0.0.0:{localPort} -> {targetHost}:{targetPort}";

//             if (_activeTunnels.ContainsKey(tunnelKey))
//             {
//                 TempData["Error"] = "Já existe um túnel ativo nesta porta local.";
//                 return RedirectToAction(nameof(TuneladorTcp));
//             }

//             var cts = new CancellationTokenSource();
            
//             // Dispara o listener TCP assíncrono em background
//             _ = Task.Run(() => RunTcpProxyServerAsync(localPort, targetHost, targetPort, cts.Token));

//             _activeTunnels.TryAdd(tunnelKey, cts);
//             TempData["Success"] = $"Túnel TCP iniciado com sucesso na porta {localPort}!";
            
//             return RedirectToAction(nameof(TuneladorTcp));
//         }

//         // Endpoint POST para encerrar um túnel ativo
//         [HttpPost]
//         public IActionResult StopTunnel(int localPort)
//         {
//             var targetKey = _activeTunnels.Keys.FirstOrDefault(k => k.StartsWith($"0.0.0.0:{localPort}"));
//             if (targetKey != null && _activeTunnels.TryRemove(targetKey, out var cts))
//             {
//                 cts.Cancel();
//                 cts.Dispose();
//                 TempData["Success"] = $"Túnel da porta {localPort} encerrado.";
//             }
//             else
//             {
//                 TempData["Error"] = "Túnel não encontrado.";
//             }

//             return RedirectToAction(nameof(TuneladorTcp));
//         }

//         // Motor de Proxy TCP de Alta Performance (Camada 4)
//         private async Task RunTcpProxyServerAsync(int localPort, string targetHost, int targetPort, CancellationToken cancellationToken)
//         {
//             var listener = new TcpListener(IPAddress.Any, localPort);
//             listener.Start();

//             try
//             {
//                 while (!cancellationToken.IsCancellationRequested)
//                 {
//                     var clientSocket = await listener.AcceptTcpClientAsync(cancellationToken);

//                     _ = Task.Run(async () =>
//                     {
//                         using (clientSocket)
//                         {
//                             using var targetClient = new TcpClient();
//                             try
//                             {
//                                 await targetClient.ConnectAsync(targetHost, targetPort, cancellationToken);

//                                 using var clientStream = clientSocket.GetStream();
//                                 using var targetStream = targetClient.GetStream();

//                                 var forwardClientToTarget = clientStream.CopyToAsync(targetStream, cancellationToken);
//                                 var forwardTargetToClient = targetStream.CopyToAsync(clientStream, cancellationToken);

//                                 await Task.WhenAny(forwardClientToTarget, forwardTargetToClient);
//                             }
//                             catch
//                             {
//                                 // Silencia falhas de pipe em conexões encerradas abruptamente
//                             }
//                         }
//                     }, cancellationToken);
//                 }
//             }
//             catch (OperationCanceledException)
//             {
//                 // Parada controlada
//             }
//             finally
//             {
//                 listener.Stop();
//             }
//         }

//         // (Demais endpoints de FormatJson, DecodeJwt e ProcessHashes continuam aqui...)
//         [HttpPost]
//         public IActionResult FormatJson([FromBody] JsonRequestModel model) { /* ... */ return Json(new { success = true }); }

//         [HttpPost]
//         public IActionResult DecodeJwt([FromBody] JwtRequestModel model) { /* ... */ return Json(new { success = true }); }

//         [HttpPost]
//         public IActionResult ProcessHashes([FromBody] HashRequestModel model) { /* ... */ return Json(new { success = true }); }

//         private string DecodeBase64Url(string base64Url) => "";
//         private string FormatJsonString(string json) => json;
//     }

//     public class JsonRequestModel { public string Input { get; set; } }
//     public class JwtRequestModel { public string Token { get; set; } }
//     public class HashRequestModel { public string Input { get; set; } }
// }
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Text.Json;
using System.Text;
using System.Security.Cryptography;
using System.Net;
using System.Net.Sockets;
using System.Collections.Concurrent;

namespace HackerToolbox.Controllers
{
    [Authorize]
    public class DevToolsController : Controller
    {
        // Armazena túneis TCP ativos na memória para monitoramento
        private static readonly ConcurrentDictionary<string, CancellationTokenSource> _activeTunnels = new();

        // Necessário para localizar a pasta privada onde o instalador do agente fica salvo
        private readonly IWebHostEnvironment _env;

        public DevToolsController(IWebHostEnvironment env)
        {
            _env = env;
        }

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
            ViewBag.ActiveTunnels = _activeTunnels.Keys.ToList();
            return View();
        }

        // Rota: /DevTools/DownloadAgent
        // Protegida pelo [Authorize] da classe: qualquer usuário não logado
        // que tentar acessar essa URL é redirecionado para o login pelo
        // middleware de autenticação, antes mesmo de esta action executar.
        [HttpGet]
        public IActionResult DownloadAgent()
        {
            // O arquivo fica FORA de wwwroot, numa pasta privada (PrivateFiles/),
            // que não é mapeada pelo UseStaticFiles(). Isso garante que a única
            // forma de baixar o .exe é passando por esta action autenticada —
            // não existe uma URL estática que o exponha diretamente.
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

        // Endpoint POST para iniciar o redirecionamento de túnel TCP (Ex: Rubinot)
        [HttpPost]
        public IActionResult StartTunnel(int localPort, string targetHost, int targetPort)
        {
            if (localPort <= 0 || string.IsNullOrWhiteSpace(targetHost) || targetPort <= 0)
            {
                TempData["Error"] = "Parâmetros inválidos para iniciar o túnel.";
                return RedirectToAction(nameof(TuneladorTcp));
            }

            string tunnelKey = $"0.0.0.0:{localPort} -> {targetHost}:{targetPort}";

            if (_activeTunnels.ContainsKey(tunnelKey))
            {
                TempData["Error"] = "Já existe um túnel ativo nesta porta local.";
                return RedirectToAction(nameof(TuneladorTcp));
            }

            var cts = new CancellationTokenSource();

            // Dispara o listener TCP assíncrono em background
            _ = Task.Run(() => RunTcpProxyServerAsync(localPort, targetHost, targetPort, cts.Token));

            _activeTunnels.TryAdd(tunnelKey, cts);
            TempData["Success"] = $"Túnel TCP iniciado com sucesso na porta {localPort}!";

            return RedirectToAction(nameof(TuneladorTcp));
        }

        // Endpoint POST para encerrar um túnel ativo
        [HttpPost]
        public IActionResult StopTunnel(int localPort)
        {
            var targetKey = _activeTunnels.Keys.FirstOrDefault(k => k.StartsWith($"0.0.0.0:{localPort}"));
            if (targetKey != null && _activeTunnels.TryRemove(targetKey, out var cts))
            {
                cts.Cancel();
                cts.Dispose();
                TempData["Success"] = $"Túnel da porta {localPort} encerrado.";
            }
            else
            {
                TempData["Error"] = "Túnel não encontrado.";
            }

            return RedirectToAction(nameof(TuneladorTcp));
        }

        // Motor de Proxy TCP de Alta Performance (Camada 4)
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

        // (Demais endpoints de FormatJson, DecodeJwt e ProcessHashes continuam aqui...)
        [HttpPost]
        public IActionResult FormatJson([FromBody] JsonRequestModel model) { /* ... */ return Json(new { success = true }); }

        [HttpPost]
        public IActionResult DecodeJwt([FromBody] JwtRequestModel model) { /* ... */ return Json(new { success = true }); }

        [HttpPost]
        public IActionResult ProcessHashes([FromBody] HashRequestModel model) { /* ... */ return Json(new { success = true }); }

        private string DecodeBase64Url(string base64Url) => "";
        private string FormatJsonString(string json) => json;
    }

    public class JsonRequestModel { public string Input { get; set; } }
    public class JwtRequestModel { public string Token { get; set; } }
    public class HashRequestModel { public string Input { get; set; } }
}