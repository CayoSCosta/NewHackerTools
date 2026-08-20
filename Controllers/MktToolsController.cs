using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.IO.Compression;
using System.IO;

namespace HackerToolbox.Controllers
{
    [Authorize]
    public class MktToolsController : Controller
    {
        [HttpGet]
        public IActionResult ImageConverter()
        {
            return View();
        }

        // Endpoint POST para processamento em lote (Conversão + Renomeação + ZIP)
        [HttpPost]
        public async Task<IActionResult> ConvertBatch(List<IFormFile> imageFiles, string targetFormat, bool enableRename, string customPrefix)
        {
            if (imageFiles == null || imageFiles.Count == 0 || imageFiles.All(f => f.Length == 0))
            {
                TempData["Error"] = "Nenhum arquivo de imagem válido foi enviado.";
                return RedirectToAction(nameof(ImageConverter));
            }

            if (string.IsNullOrEmpty(targetFormat))
            {
                TempData["Error"] = "Selecione um formato de saída.";
                return RedirectToAction(nameof(ImageConverter));
            }

            try
            {
                string newExtension = targetFormat.ToLower().TrimStart('.');
                var zipMemoryStream = new MemoryStream();

                using (var archive = new ZipArchive(zipMemoryStream, ZipArchiveMode.Create, true))
                {
                    int index = 1;
                    foreach (var file in imageFiles)
                    {
                        if (file.Length == 0) continue;

                        using var inputMemoryStream = new MemoryStream();
                        await file.CopyToAsync(inputMemoryStream);
                        byte[] fileBytes = inputMemoryStream.ToArray();

                        // Define o nome do arquivo resultante
                        string finalFileName;
                        if (enableRename && !string.IsNullOrWhiteSpace(customPrefix))
                        {
                            string cleanPrefix = Path.GetInvalidFileNameChars().Aggregate(customPrefix.Trim(), (current, c) => current.Replace(c, '_'));
                            finalFileName = $"{cleanPrefix}_{index}.{newExtension}";
                            index++;
                        }
                        else
                        {
                            string originalName = Path.GetFileNameWithoutExtension(file.FileName);
                            finalFileName = $"{originalName}.{newExtension}";
                        }

                        // Adiciona a entrada no arquivo ZIP em memória
                        var zipEntry = archive.CreateEntry(finalFileName, CompressionLevel.Optimal);
                        using var entryStream = zipEntry.Open();
                        await entryStream.WriteAsync(fileBytes, 0, fileBytes.Length);
                    }
                }

                zipMemoryStream.Position = 0;
                string zipName = $"HackerToolbox_ConvertedImages_{DateTime.Now:yyyyMMdd_HHmmss}.zip";

                return File(zipMemoryStream.ToArray(), "application/zip", zipName);
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"Erro crítico ao processar o lote: {ex.Message}";
                return RedirectToAction(nameof(ImageConverter));
            }
        }

        [HttpGet]
        public IActionResult ExifCleaner()
        {
            return View();
        }
    }
}