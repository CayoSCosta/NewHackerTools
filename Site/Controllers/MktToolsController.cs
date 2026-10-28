using HackerToolbox.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.IO.Compression;
using System.IO;
using System.Security.Claims;

namespace HackerToolbox.Controllers
{
    [Authorize]
    public class MktToolsController : Controller
    {
        private readonly ILogger<MktToolsController> _logger;

        public MktToolsController(ILogger<MktToolsController> logger)
        {
            _logger = logger;
        }
        private const long ScrubMaxFileBytes = 524_288_000; // 500 MB por arquivo
        private const long ScrubRequestBytes = ScrubMaxFileBytes + 1_048_576;
        private static readonly TimeSpan ScrubRetention = TimeSpan.FromHours(2);

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

        // Rota: /MktTools/UtmGenerator
        [HttpGet]
        public IActionResult UtmGenerator()
        {
            return View();
        }

        // Processa um arquivo do lote: lê metadados (antes), limpa, lê metadados (depois) e guarda o resultado temporariamente
        [HttpPost]
        [RequestSizeLimit(ScrubRequestBytes)]
        [RequestFormLimits(MultipartBodyLengthLimit = ScrubRequestBytes)]
        public async Task<IActionResult> ScrubFile(IFormFile file, string batchId, CancellationToken ct)
        {
            if (!TryGetBatchDir(batchId, out string batchDir))
                return BadRequest(new { error = "Lote inválido." });
            if (file == null || file.Length == 0)
                return BadRequest(new { error = "Arquivo vazio." });
            if (file.Length > ScrubMaxFileBytes)
                return BadRequest(new { error = "Arquivo excede 500 MB." });

            CleanupOldBatches();
            Directory.CreateDirectory(batchDir);

            string fileId = Guid.NewGuid().ToString("N");
            string safeName = Path.GetInvalidFileNameChars().Aggregate(Path.GetFileName(file.FileName), (c, ch) => c.Replace(ch, '_'));
            string extension = Path.GetExtension(safeName).ToLowerInvariant();
            string outName = $"{Path.GetFileNameWithoutExtension(safeName)}_clean{extension}";
            string inputPath = Path.Combine(batchDir, fileId + ".in");
            string outputPath = Path.Combine(batchDir, fileId + ".out");

            try
            {
                await using (var fs = new FileStream(inputPath, FileMode.Create, FileAccess.Write, FileShare.None, 81920, true))
                {
                    await file.CopyToAsync(fs, ct);
                }

                var before = await MetadataScrubber.ReadAsync(inputPath, extension, ct);
                bool supported = await MetadataScrubber.CleanAsync(inputPath, outputPath, extension, ct);
                var after = await MetadataScrubber.ReadAsync(outputPath, extension, ct);

                await System.IO.File.WriteAllTextAsync(Path.Combine(batchDir, fileId + ".name"), outName, ct);

                return Json(new
                {
                    fileId,
                    name = file.FileName,
                    outName,
                    supported,
                    sizeBefore = file.Length,
                    sizeAfter = new FileInfo(outputPath).Length,
                    before,
                    after
                });
            }
            catch (System.ComponentModel.Win32Exception ex)
            {
                _logger.LogError(ex, "ScrubFile: ffmpeg/ffprobe não encontrado. Arquivo={FileName}", file.FileName);
                return StatusCode(500, new { error = "ffmpeg não encontrado no servidor." });
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "ScrubFile falhou. Arquivo={FileName} Tamanho={Size} Extensao={Ext} Lote={BatchId}",
                    file.FileName, file.Length, extension, batchId);
                foreach (var p in new[] { outputPath, Path.Combine(batchDir, fileId + ".name") })
                {
                    try { System.IO.File.Delete(p); } catch { }
                }
                return StatusCode(500, new { error = ex.Message });
            }
            finally
            {
                try { System.IO.File.Delete(inputPath); } catch { }
            }
        }

        [HttpGet]
        public IActionResult DownloadScrubbed(string batchId, string fileId)
        {
            if (!TryGetBatchDir(batchId, out string batchDir) || !Guid.TryParseExact(fileId, "N", out _))
                return NotFound();

            string outputPath = Path.Combine(batchDir, fileId + ".out");
            string namePath = Path.Combine(batchDir, fileId + ".name");
            if (!System.IO.File.Exists(outputPath) || !System.IO.File.Exists(namePath))
                return NotFound();

            string outName = System.IO.File.ReadAllText(namePath);
            return PhysicalFile(outputPath, MetadataScrubber.GetMimeType(Path.GetExtension(outName)), outName);
        }

        [HttpGet]
        public async Task<IActionResult> DownloadBatchZip(string batchId)
        {
            if (!TryGetBatchDir(batchId, out string batchDir) || !Directory.Exists(batchDir))
                return NotFound();

            var entries = Directory.GetFiles(batchDir, "*.name").OrderBy(f => System.IO.File.GetCreationTimeUtc(f)).ToList();
            if (entries.Count == 0) return NotFound();

            var zipPath = Path.Combine(Path.GetTempPath(), "hbx_zip_" + Guid.NewGuid().ToString("N"));
            var zipStream = new FileStream(zipPath, FileMode.Create, FileAccess.ReadWrite, FileShare.None, 81920,
                FileOptions.Asynchronous | FileOptions.DeleteOnClose);

            var used = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            using (var archive = new ZipArchive(zipStream, ZipArchiveMode.Create, true))
            {
                foreach (var namePath in entries)
                {
                    string outputPath = Path.ChangeExtension(namePath, ".out");
                    if (!System.IO.File.Exists(outputPath)) continue;

                    string name = await System.IO.File.ReadAllTextAsync(namePath);
                    string unique = name;
                    int dup = 2;
                    while (!used.Add(unique))
                        unique = $"{Path.GetFileNameWithoutExtension(name)}_{dup++}{Path.GetExtension(name)}";

                    var entry = archive.CreateEntry(unique, CompressionLevel.NoCompression);
                    await using var es = entry.Open();
                    await using var src = new FileStream(outputPath, FileMode.Open, FileAccess.Read, FileShare.Read, 81920, true);
                    await src.CopyToAsync(es);
                }
            }
            zipStream.Position = 0;
            return File(zipStream, "application/zip", $"HackerToolbox_CleanFiles_{DateTime.Now:yyyyMMdd_HHmmss}.zip");
        }

        private string ScrubRoot()
        {
            string user = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "anon";
            user = Path.GetInvalidFileNameChars().Aggregate(user, (c, ch) => c.Replace(ch, '_'));
            return Path.Combine(Path.GetTempPath(), "hbx_scrub", user);
        }

        private bool TryGetBatchDir(string batchId, out string batchDir)
        {
            batchDir = string.Empty;
            if (!Guid.TryParseExact(batchId, "N", out _)) return false;
            batchDir = Path.Combine(ScrubRoot(), batchId);
            return true;
        }

        private void CleanupOldBatches()
        {
            try
            {
                string parent = Path.Combine(Path.GetTempPath(), "hbx_scrub");
                if (!Directory.Exists(parent)) return;
                foreach (var dir in Directory.EnumerateDirectories(parent, "*", SearchOption.AllDirectories))
                {
                    if (!Guid.TryParseExact(Path.GetFileName(dir), "N", out _)) continue;
                    if (DateTime.UtcNow - Directory.GetLastWriteTimeUtc(dir) > ScrubRetention)
                        Directory.Delete(dir, true);
                }
            }
            catch { }
        }
    }
}
