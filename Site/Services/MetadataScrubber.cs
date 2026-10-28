using System.ComponentModel;
using System.Diagnostics;
using System.IO.Compression;
using System.Text.Json;
using System.Xml.Linq;

namespace HackerToolbox.Services
{
    public static class MetadataScrubber
    {
        public static readonly HashSet<string> VideoExtensions = new() { ".mp4", ".mov", ".m4v", ".mkv", ".webm", ".avi" };
        private static readonly HashSet<string> OoxmlExtensions = new() { ".docx", ".xlsx", ".pptx" };

        public static string GetMimeType(string extension) => extension.ToLowerInvariant() switch
        {
            ".jpg" or ".jpeg" => "image/jpeg",
            ".png" => "image/png",
            ".pdf" => "application/pdf",
            ".docx" => "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
            ".xlsx" => "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            ".pptx" => "application/vnd.openxmlformats-officedocument.presentationml.presentation",
            ".mp4" or ".m4v" => "video/mp4",
            ".mov" => "video/quicktime",
            ".mkv" => "video/x-matroska",
            ".webm" => "video/webm",
            ".avi" => "video/x-msvideo",
            _ => "application/octet-stream"
        };

        // Retorna false quando o formato não tem limpeza suportada (arquivo é copiado sem alterações)
        public static async Task<bool> CleanAsync(string inputPath, string outputPath, string extension, CancellationToken ct = default)
        {
            extension = extension.ToLowerInvariant();

            if (VideoExtensions.Contains(extension))
            {
                await CleanVideoAsync(inputPath, outputPath, extension, ct);
                return true;
            }
            if (extension is ".jpg" or ".jpeg")
            {
                await File.WriteAllBytesAsync(outputPath, StripJpeg(await File.ReadAllBytesAsync(inputPath, ct)), ct);
                return true;
            }
            if (extension == ".png")
            {
                await File.WriteAllBytesAsync(outputPath, StripPng(await File.ReadAllBytesAsync(inputPath, ct)), ct);
                return true;
            }
            if (OoxmlExtensions.Contains(extension))
            {
                StripOoxml(inputPath, outputPath);
                return true;
            }

            File.Copy(inputPath, outputPath, true);
            return false;
        }

        public static async Task<Dictionary<string, string>> ReadAsync(string path, string extension, CancellationToken ct = default)
        {
            extension = extension.ToLowerInvariant();
            try
            {
                if (OoxmlExtensions.Contains(extension)) return ReadOoxml(path);
                if (VideoExtensions.Contains(extension) || extension is ".jpg" or ".jpeg" or ".png")
                    return await ReadWithFfprobeAsync(path, extension is ".jpg" or ".jpeg" or ".png", ct);
            }
            catch (Win32Exception)
            {
                return new Dictionary<string, string> { ["(aviso)"] = "ffprobe não encontrado no servidor; leitura indisponível." };
            }
            return new Dictionary<string, string>();
        }

        private static async Task CleanVideoAsync(string inputPath, string outputPath, string extension, CancellationToken ct)
        {
            var psi = new ProcessStartInfo("ffmpeg")
            {
                RedirectStandardError = true,
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            foreach (var arg in new[]
            {
                "-y", "-i", inputPath, "-map", "0", "-map_metadata", "-1", "-map_chapters", "-1",
                "-fflags", "+bitexact", "-flags:v", "+bitexact", "-flags:a", "+bitexact", "-c", "copy"
            })
            {
                psi.ArgumentList.Add(arg);
            }
            if (extension is ".mp4" or ".mov" or ".m4v")
            {
                psi.ArgumentList.Add("-movflags");
                psi.ArgumentList.Add("+faststart");
            }
            psi.ArgumentList.Add("-f");
            psi.ArgumentList.Add(extension switch
            {
                ".mp4" or ".m4v" => "mp4",
                ".mov" => "mov",
                ".mkv" => "matroska",
                ".webm" => "webm",
                ".avi" => "avi",
                _ => throw new InvalidOperationException($"Extensão de vídeo não suportada: {extension}")
            });
            psi.ArgumentList.Add(outputPath);

            var (exit, _, stderr) = await RunAsync(psi, TimeSpan.FromMinutes(10), ct);
            if (exit != 0 || !File.Exists(outputPath))
            {
                string tail = stderr.Length > 300 ? stderr[^300..] : stderr;
                throw new InvalidOperationException($"ffmpeg falhou (exit {exit}): {tail}", new Exception(stderr));
            }
        }

        private static async Task<Dictionary<string, string>> ReadWithFfprobeAsync(string path, bool isImage, CancellationToken ct)
        {
            var psi = new ProcessStartInfo("ffprobe")
            {
                RedirectStandardError = true,
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            foreach (var arg in new[] { "-v", "error", "-print_format", "json", "-show_format", "-show_streams" })
                psi.ArgumentList.Add(arg);
            if (isImage)
            {
                psi.ArgumentList.Add("-show_frames");
                psi.ArgumentList.Add("-read_intervals");
                psi.ArgumentList.Add("%+#1");
            }
            psi.ArgumentList.Add(path);

            var (exit, stdout, _) = await RunAsync(psi, TimeSpan.FromMinutes(2), ct);
            var result = new Dictionary<string, string>();
            if (exit != 0 || string.IsNullOrWhiteSpace(stdout)) return result;

            using var doc = JsonDocument.Parse(stdout);
            var root = doc.RootElement;

            if (root.TryGetProperty("format", out var format))
                AddTags(result, format, "");
            if (root.TryGetProperty("streams", out var streams))
            {
                int i = 0;
                foreach (var s in streams.EnumerateArray()) AddTags(result, s, $"stream{i++}/");
            }
            if (root.TryGetProperty("frames", out var frames))
            {
                foreach (var f in frames.EnumerateArray()) { AddTags(result, f, "frame/"); break; }
            }
            return result;
        }

        private static void AddTags(Dictionary<string, string> target, JsonElement element, string prefix)
        {
            if (!element.TryGetProperty("tags", out var tags) || tags.ValueKind != JsonValueKind.Object) return;
            foreach (var p in tags.EnumerateObject())
                target[prefix + p.Name] = p.Value.ToString();
        }

        private static async Task<(int ExitCode, string Stdout, string Stderr)> RunAsync(ProcessStartInfo psi, TimeSpan timeout, CancellationToken ct)
        {
            using var process = Process.Start(psi) ?? throw new InvalidOperationException($"Não foi possível iniciar {psi.FileName}.");
            var stdoutTask = process.StandardOutput.ReadToEndAsync();
            var stderrTask = process.StandardError.ReadToEndAsync();

            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(timeout);
            try
            {
                await process.WaitForExitAsync(cts.Token);
            }
            catch (OperationCanceledException)
            {
                try { process.Kill(true); } catch { }
                throw new TimeoutException($"Tempo limite excedido em {psi.FileName}.");
            }
            return (process.ExitCode, await stdoutTask, await stderrTask);
        }

        // Remove APP1 (EXIF/XMP), APP3-APP13 (IPTC/Photoshop etc.) e COM; mantém APP0, APP2 (ICC) e APP14
        private static byte[] StripJpeg(byte[] d)
        {
            if (d.Length < 4 || d[0] != 0xFF || d[1] != 0xD8) throw new InvalidDataException("JPEG inválido.");

            using var o = new MemoryStream(d.Length);
            o.Write(d, 0, 2);
            int i = 2;
            while (i + 4 <= d.Length)
            {
                if (d[i] != 0xFF) throw new InvalidDataException("JPEG corrompido.");
                byte m = d[i + 1];
                if (m == 0xFF) { i++; continue; }
                if (m == 0xDA || m == 0xD9) break;
                if ((m >= 0xD0 && m <= 0xD7) || m == 0x01)
                {
                    o.Write(d, i, 2);
                    i += 2;
                    continue;
                }

                int len = (d[i + 2] << 8) | d[i + 3];
                if (len < 2 || i + 2 + len > d.Length) throw new InvalidDataException("JPEG corrompido.");
                bool drop = m == 0xE1 || (m >= 0xE3 && m <= 0xED) || m == 0xFE;
                if (!drop) o.Write(d, i, 2 + len);
                i += 2 + len;
            }
            o.Write(d, i, d.Length - i);
            return o.ToArray();
        }

        private static readonly HashSet<string> PngDropChunks = new() { "tEXt", "zTXt", "iTXt", "eXIf", "tIME", "dSIG" };

        private static byte[] StripPng(byte[] d)
        {
            byte[] sig = { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A };
            if (d.Length < 8 || !d.AsSpan(0, 8).SequenceEqual(sig)) throw new InvalidDataException("PNG inválido.");

            using var o = new MemoryStream(d.Length);
            o.Write(d, 0, 8);
            int i = 8;
            while (i + 12 <= d.Length)
            {
                int len = (d[i] << 24) | (d[i + 1] << 16) | (d[i + 2] << 8) | d[i + 3];
                if (len < 0 || i + 12 + (long)len > d.Length) throw new InvalidDataException("PNG corrompido.");
                string type = System.Text.Encoding.ASCII.GetString(d, i + 4, 4);
                if (!PngDropChunks.Contains(type)) o.Write(d, i, 12 + len);
                i += 12 + len;
            }
            o.Write(d, i, d.Length - i);
            return o.ToArray();
        }

        private const string EmptyCore =
            "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>" +
            "<cp:coreProperties xmlns:cp=\"http://schemas.openxmlformats.org/package/2006/metadata/core-properties\" " +
            "xmlns:dc=\"http://purl.org/dc/elements/1.1/\" xmlns:dcterms=\"http://purl.org/dc/terms/\" " +
            "xmlns:xsi=\"http://www.w3.org/2001/XMLSchema-instance\"/>";
        private const string EmptyApp =
            "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>" +
            "<Properties xmlns=\"http://schemas.openxmlformats.org/officeDocument/2006/extended-properties\"/>";
        private const string EmptyCustom =
            "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>" +
            "<Properties xmlns=\"http://schemas.openxmlformats.org/officeDocument/2006/custom-properties\" " +
            "xmlns:vt=\"http://schemas.openxmlformats.org/officeDocument/2006/docPropsVTypes\"/>";

        private static void StripOoxml(string inputPath, string outputPath)
        {
            using var input = ZipFile.OpenRead(inputPath);
            using var outStream = new FileStream(outputPath, FileMode.Create, FileAccess.Write);
            using var output = new ZipArchive(outStream, ZipArchiveMode.Create);

            foreach (var entry in input.Entries)
            {
                var newEntry = output.CreateEntry(entry.FullName, CompressionLevel.Optimal);
                using var dest = newEntry.Open();
                string? replacement = entry.FullName switch
                {
                    "docProps/core.xml" => EmptyCore,
                    "docProps/app.xml" => EmptyApp,
                    "docProps/custom.xml" => EmptyCustom,
                    _ => null
                };
                if (replacement != null)
                {
                    using var w = new StreamWriter(dest, new System.Text.UTF8Encoding(false));
                    w.Write(replacement);
                }
                else
                {
                    using var src = entry.Open();
                    src.CopyTo(dest);
                }
            }
        }

        private static Dictionary<string, string> ReadOoxml(string path)
        {
            var result = new Dictionary<string, string>();
            using var zip = ZipFile.OpenRead(path);
            foreach (var name in new[] { "docProps/core.xml", "docProps/app.xml", "docProps/custom.xml" })
            {
                var entry = zip.GetEntry(name);
                if (entry == null) continue;
                using var s = entry.Open();
                var doc = XDocument.Load(s);
                foreach (var el in doc.Root!.Elements())
                {
                    string value = el.HasElements
                        ? string.Join(" ", el.Descendants().Where(e => !e.HasElements).Select(e => e.Value))
                        : el.Value;
                    if (!string.IsNullOrWhiteSpace(value))
                    {
                        string key = el.Name.LocalName == "property" ? (el.Attribute("name")?.Value ?? "property") : el.Name.LocalName;
                        result[key] = value;
                    }
                }
            }
            return result;
        }
    }
}
