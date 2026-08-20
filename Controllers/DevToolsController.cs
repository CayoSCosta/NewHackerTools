using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Text.Json;
using System.Text;
using System.Security.Cryptography;

namespace HackerToolbox.Controllers
{
    [Authorize]
    public class DevToolsController : Controller
    {
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
            return View();
        }

        // Endpoint AJAX para Validar e Formatar JSON
        [HttpPost]
        public IActionResult FormatJson([FromBody] JsonRequestModel model)
        {
            if (string.IsNullOrWhiteSpace(model?.Input))
            {
                return Json(new { success = false, message = "Input vazio." });
            }

            try
            {
                using var doc = JsonDocument.Parse(model.Input);
                var prettyJson = JsonSerializer.Serialize(doc.RootElement, new JsonSerializerOptions
                {
                    WriteIndented = true
                });

                return Json(new { success = true, result = prettyJson });
            }
            catch (JsonException ex)
            {
                return Json(new { success = false, message = $"Erro de sintaxe JSON: {ex.Message}" });
            }
        }

        // Endpoint AJAX para Decodificar JWT
        [HttpPost]
        public IActionResult DecodeJwt([FromBody] JwtRequestModel model)
        {
            if (string.IsNullOrWhiteSpace(model?.Token))
            {
                return Json(new { success = false, message = "Token JWT vazio." });
            }

            try
            {
                var parts = model.Token.Split('.');
                if (parts.Length != 3)
                {
                    return Json(new { success = false, message = "Token JWT inválido. Um JWT deve conter exatamente 3 partes separadas por pontos (.)" });
                }

                var headerJson = DecodeBase64Url(parts[0]);
                var payloadJson = DecodeBase64Url(parts[1]);
                var signature = parts[2];

                var formattedHeader = FormatJsonString(headerJson);
                var formattedPayload = FormatJsonString(payloadJson);

                return Json(new
                {
                    success = true,
                    header = formattedHeader,
                    payload = formattedPayload,
                    signature = signature
                });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = $"Erro ao decodificar o token: {ex.Message}" });
            }
        }

        // Endpoint AJAX para calcular múltiplos Hashes e Base64
        [HttpPost]
        public IActionResult ProcessHashes([FromBody] HashRequestModel model)
        {
            if (string.IsNullOrEmpty(model?.Input))
            {
                return Json(new { success = false, message = "Texto de entrada vazio." });
            }

            try
            {
                var inputBytes = Encoding.UTF8.GetBytes(model.Input);

                // 1. Cálculo de Base64 (Codificação)
                var base64Encoded = Convert.ToBase64String(inputBytes);

                // 2. Cálculo de MD5
                string md5Hash;
                using (var md5 = MD5.Create())
                {
                    var hashBytes = md5.ComputeHash(inputBytes);
                    md5Hash = BitConverter.ToString(hashBytes).Replace("-", "").ToLowerInvariant();
                }

                // 3. Cálculo de SHA256
                string sha256Hash;
                using (var sha256 = SHA256.Create())
                {
                    var hashBytes = sha256.ComputeHash(inputBytes);
                    sha256Hash = BitConverter.ToString(hashBytes).Replace("-", "").ToLowerInvariant();
                }

                // 4. Cálculo de SHA512
                string sha512Hash;
                using (var sha512 = SHA512.Create())
                {
                    var hashBytes = sha512.ComputeHash(inputBytes);
                    sha512Hash = BitConverter.ToString(hashBytes).Replace("-", "").ToLowerInvariant();
                }

                return Json(new
                {
                    success = true,
                    base64 = base64Encoded,
                    md5 = md5Hash,
                    sha256 = sha256Hash,
                    sha512 = sha512Hash
                });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = $"Erro ao processar hashes: {ex.Message}" });
            }
        }

        private string DecodeBase64Url(string base64Url)
        {
            string base64 = base64Url.Replace('-', '+').Replace('_', '/');
            switch (base64.Length % 4)
            {
                case 2: base64 += "=="; break;
                case 3: base64 += "="; break;
            }
            var bytes = Convert.FromBase64String(base64);
            return Encoding.UTF8.GetString(bytes);
        }

        private string FormatJsonString(string json)
        {
            try
            {
                using var doc = JsonDocument.Parse(json);
                return JsonSerializer.Serialize(doc.RootElement, new JsonSerializerOptions { WriteIndented = true });
            }
            catch
            {
                return json;
            }
        }
    }

    public class JsonRequestModel
    {
        public string Input { get; set; }
    }

    public class JwtRequestModel
    {
        public string Token { get; set; }
    }

    public class HashRequestModel
    {
        public string Input { get; set; }
    }

}