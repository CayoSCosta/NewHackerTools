using System.Diagnostics;
using System.IO.Compression;
using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;
using static HackerToolbox.Tests.AdminSeedAndAuthTests;

namespace HackerToolbox.Tests;

// Fluxo HTTP do scrubber de metadados (/MktTools/ScrubFile) com ARQUIVOS SINTÉTICOS gerados em memória
// (nenhum arquivo pessoal). O vídeo usa o ffmpeg do PATH e fica "inconclusivo" se ele não existir.
// Limite de 500 MB: não é testado aqui (exigiria gravar ~500 MB); veja docs/DOCKER.md para o teste manual no container.
[TestClass]
public class MetadataScrubberHttpTests
{
    private const string ExifSecret = "SYNTH_EXIF_SECRET";
    private const string CommentSecret = "SYNTH_COMMENT_SECRET";
    private const string IptcSecret = "SYNTH_IPTC_SECRET";
    private const string PngSecret = "SYNTH_PNG_SECRET";
    private const string AuthorSecret = "SYNTH_AUTHOR_SECRET";

    // ---------- arquivos sintéticos ----------

    private static byte[] Seg(byte marker, byte[] payload)
    {
        var len = payload.Length + 2;
        return [0xFF, marker, (byte)(len >> 8), (byte)len, .. payload];
    }

    private static byte[] BuildJpeg()
    {
        using var m = new MemoryStream();
        m.Write([0xFF, 0xD8]);
        m.Write(Seg(0xE0, [.. "JFIF\0"u8, 1, 1, 0, 0, 1, 0, 1, 0, 0]));
        m.Write(Seg(0xE1, [.. "Exif\0\0"u8, .. Encoding.ASCII.GetBytes(ExifSecret)]));
        m.Write(Seg(0xED, [.. "Photoshop 3.0\0"u8, .. Encoding.ASCII.GetBytes(IptcSecret)]));
        m.Write(Seg(0xFE, Encoding.ASCII.GetBytes(CommentSecret)));
        m.Write(Seg(0xDA, [1, 1, 0, 0, 0x3F, 0]));
        m.Write([0x12, 0x34, 0x56, 0xFF, 0xD9]);
        return m.ToArray();
    }

    private static uint Crc32(ReadOnlySpan<byte> data)
    {
        var crc = 0xFFFFFFFFu;
        foreach (var b in data)
        {
            crc ^= b;
            for (var k = 0; k < 8; k++) crc = (crc & 1) != 0 ? (crc >> 1) ^ 0xEDB88320u : crc >> 1;
        }
        return ~crc;
    }

    private static byte[] PngChunk(string type, byte[] data)
    {
        var typeAndData = Encoding.ASCII.GetBytes(type).Concat(data).ToArray();
        var crc = Crc32(typeAndData);
        return
        [
            (byte)(data.Length >> 24), (byte)(data.Length >> 16), (byte)(data.Length >> 8), (byte)data.Length,
            .. typeAndData,
            (byte)(crc >> 24), (byte)(crc >> 16), (byte)(crc >> 8), (byte)crc
        ];
    }

    private static byte[] BuildPng()
    {
        byte[] idat;
        using (var z = new MemoryStream())
        {
            using (var zs = new ZLibStream(z, CompressionLevel.Optimal, true)) zs.Write([0, 255, 0, 0]);
            idat = z.ToArray();
        }
        using var m = new MemoryStream();
        m.Write([0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A]);
        m.Write(PngChunk("IHDR", [0, 0, 0, 1, 0, 0, 0, 1, 8, 2, 0, 0, 0]));
        m.Write(PngChunk("tEXt", Encoding.ASCII.GetBytes("Comment\0" + PngSecret)));
        m.Write(PngChunk("tIME", [7, 234, 1, 1, 0, 0, 0]));
        m.Write(PngChunk("IDAT", idat));
        m.Write(PngChunk("IEND", []));
        return m.ToArray();
    }

    private static byte[] BuildDocx()
    {
        using var ms = new MemoryStream();
        using (var zip = new ZipArchive(ms, ZipArchiveMode.Create, true))
        {
            void Add(string name, string content)
            {
                using var w = new StreamWriter(zip.CreateEntry(name).Open(), new UTF8Encoding(false));
                w.Write(content);
            }
            Add("[Content_Types].xml", "<Types xmlns=\"http://schemas.openxmlformats.org/package/2006/content-types\"/>");
            Add("word/document.xml", "<w:document xmlns:w=\"http://schemas.openxmlformats.org/wordprocessingml/2006/main\"><w:body>BODY_KEPT</w:body></w:document>");
            Add("docProps/core.xml", $"<cp:coreProperties xmlns:cp=\"http://schemas.openxmlformats.org/package/2006/metadata/core-properties\" xmlns:dc=\"http://purl.org/dc/elements/1.1/\"><dc:creator>{AuthorSecret}</dc:creator></cp:coreProperties>");
            Add("docProps/app.xml", "<Properties xmlns=\"http://schemas.openxmlformats.org/officeDocument/2006/extended-properties\"><Company>SYNTH_COMPANY_SECRET</Company></Properties>");
        }
        return ms.ToArray();
    }

    private static bool FfmpegAvailable()
    {
        try
        {
            using var p = Process.Start(new ProcessStartInfo("ffmpeg", "-version") { RedirectStandardOutput = true, RedirectStandardError = true })!;
            p.WaitForExit(10000);
            return p.ExitCode == 0;
        }
        catch { return false; }
    }

    private static byte[] BuildMp4()
    {
        var path = Path.Combine(Path.GetTempPath(), $"hbx-synth-{Guid.NewGuid():N}.mp4");
        try
        {
            var psi = new ProcessStartInfo("ffmpeg") { RedirectStandardError = true, RedirectStandardOutput = true };
            foreach (var a in new[] { "-y", "-f", "lavfi", "-i", "testsrc=size=64x64:rate=5", "-t", "1", "-c:v", "mpeg4",
                         "-metadata", "title=SYNTH_TITLE_SECRET", "-metadata", "comment=SYNTH_VIDEO_COMMENT_SECRET", path })
                psi.ArgumentList.Add(a);
            using var p = Process.Start(psi)!;
            p.StandardError.ReadToEnd();
            p.WaitForExit(60000);
            Assert.AreEqual(0, p.ExitCode, "ffmpeg não conseguiu gerar o vídeo sintético");
            return File.ReadAllBytes(path);
        }
        finally { File.Delete(path); }
    }

    private static bool Contains(byte[] data, string text) => data.AsSpan().IndexOf(Encoding.ASCII.GetBytes(text)) >= 0;

    // ---------- infraestrutura ----------

    private sealed record Scrub(HttpStatusCode Status, JsonElement Json, string BatchId);

    private static async Task<Scrub> Upload(HttpClient c, string fileName, byte[] content, string? batchId = null)
    {
        batchId ??= Guid.NewGuid().ToString("N");
        using var form = new MultipartFormDataContent
        {
            { new ByteArrayContent(content), "file", fileName },
            { new StringContent(batchId), "batchId" }
        };
        var r = await c.PostAsync("/MktTools/ScrubFile", form);
        var body = await r.Content.ReadAsStringAsync();
        var json = body.StartsWith('{') ? JsonDocument.Parse(body).RootElement.Clone() : default;
        return new Scrub(r.StatusCode, json, batchId);
    }

    private static string BatchDir(string provider, string connection, string email, string batchId)
    {
        using var ctx = Ctx(provider, connection);
        var id = ctx.Users.AsNoTracking().Single(u => u.Email == email).Id;
        return Path.Combine(Path.GetTempPath(), "hbx_scrub", id, batchId);
    }

    private static async Task<(AppFactory Factory, HttpClient Client, string Email, TestDb Db)> LoggedIn(string provider)
    {
        var db = await NewMigratedDb(provider);
        var email = RandomEmail();
        var pw = RandomPassword();
        var factory = new AppFactory(db, email, pw);
        var client = NewClient(factory);
        Assert.AreEqual(HttpStatusCode.Redirect, (await Login(client, email, pw)).StatusCode);
        return (factory, client, email, db);
    }

    private static bool AnyTagContains(JsonElement tags, string text) =>
        tags.ValueKind == JsonValueKind.Object && tags.EnumerateObject().Any(p => p.Value.ToString().Contains(text));

    // ---------- testes ----------

    [TestMethod]
    [DataRow("Sqlite")]
    [DataRow("Postgres")]
    public async Task Scrub_JpegPngDocx_RemovesMetadata_KeepsContent_AndTempPolicy(string provider)
    {
        var (factory, client, email, db) = await LoggedIn(provider);
        await using var _ = db; using var __ = factory; using var ___ = client;

        // JPEG
        var jpegIn = BuildJpeg();
        Assert.IsTrue(Contains(jpegIn, ExifSecret) && Contains(jpegIn, CommentSecret));
        var jpeg = await Upload(client, "foto sintética.JPG", jpegIn);
        Assert.AreEqual(HttpStatusCode.OK, jpeg.Status);
        Assert.IsTrue(jpeg.Json.GetProperty("supported").GetBoolean());
        Assert.AreEqual("foto sintética_clean.jpg", jpeg.Json.GetProperty("outName").GetString());
        var jpegOut = await client.GetByteArrayAsync($"/MktTools/DownloadScrubbed?batchId={jpeg.BatchId}&fileId={jpeg.Json.GetProperty("fileId").GetString()}");
        foreach (var s in new[] { ExifSecret, CommentSecret, IptcSecret }) Assert.IsFalse(Contains(jpegOut, s), $"{s} deveria ter sido removido");
        Assert.IsTrue(Contains(jpegOut, "JFIF"), "APP0 (JFIF) é mantido");
        CollectionAssert.AreEqual(new byte[] { 0x12, 0x34, 0x56, 0xFF, 0xD9 }, jpegOut[^5..], "dados da imagem preservados");
        Assert.AreEqual(jpegOut.Length, jpeg.Json.GetProperty("sizeAfter").GetInt64());
        Assert.AreEqual(jpegIn.Length, jpeg.Json.GetProperty("sizeBefore").GetInt64());

        // PNG
        var pngIn = BuildPng();
        var png = await Upload(client, "img.png", pngIn);
        Assert.AreEqual(HttpStatusCode.OK, png.Status);
        var pngOut = await client.GetByteArrayAsync($"/MktTools/DownloadScrubbed?batchId={png.BatchId}&fileId={png.Json.GetProperty("fileId").GetString()}");
        Assert.IsTrue(Contains(pngIn, PngSecret));
        Assert.IsFalse(Contains(pngOut, PngSecret));
        Assert.IsFalse(Contains(pngOut, "tEXt") || Contains(pngOut, "tIME"));
        Assert.IsTrue(Contains(pngOut, "IHDR") && Contains(pngOut, "IDAT") && Contains(pngOut, "IEND"));

        // DOCX
        var docxIn = BuildDocx();
        var docx = await Upload(client, "doc.docx", docxIn);
        Assert.AreEqual(HttpStatusCode.OK, docx.Status);
        Assert.IsTrue(AnyTagContains(docx.Json.GetProperty("before"), AuthorSecret), "antes: autor visível");
        Assert.AreEqual(0, docx.Json.GetProperty("after").EnumerateObject().Count(), "depois: sem metadados");
        var docxOut = await client.GetByteArrayAsync($"/MktTools/DownloadScrubbed?batchId={docx.BatchId}&fileId={docx.Json.GetProperty("fileId").GetString()}");
        using (var zip = new ZipArchive(new MemoryStream(docxOut)))
        {
            using var core = new StreamReader(zip.GetEntry("docProps/core.xml")!.Open());
            using var app = new StreamReader(zip.GetEntry("docProps/app.xml")!.Open());
            Assert.IsFalse((await core.ReadToEndAsync()).Contains(AuthorSecret));
            Assert.IsFalse((await app.ReadToEndAsync()).Contains("SYNTH_COMPANY_SECRET"));
            using var body = new StreamReader(zip.GetEntry("word/document.xml")!.Open());
            StringAssert.Contains(await body.ReadToEndAsync(), "BODY_KEPT");
        }

        // Política de temporários: .in apagado; .out/.name retidos na pasta do lote do usuário
        var dir = BatchDir(provider, db.Connection, email, jpeg.BatchId);
        Assert.IsTrue(Directory.Exists(dir));
        Assert.AreEqual(0, Directory.GetFiles(dir, "*.in").Length, "entrada deve ser apagada após o processamento");
        Assert.AreEqual(1, Directory.GetFiles(dir, "*.out").Length);
        Assert.AreEqual(1, Directory.GetFiles(dir, "*.name").Length);
    }

    [TestMethod]
    public async Task Scrub_UnsupportedFormat_IsCopiedUnchanged_AndReportedAsUnsupported()
    {
        var (factory, client, _, db) = await LoggedIn("Sqlite");
        await using var _1 = db; using var _2 = factory; using var _3 = client;

        var content = Encoding.ASCII.GetBytes("%PDF-1.4 sintético, sem limpeza suportada");
        var r = await Upload(client, "relatorio.pdf", content);
        Assert.AreEqual(HttpStatusCode.OK, r.Status);
        Assert.IsFalse(r.Json.GetProperty("supported").GetBoolean());
        var downloaded = await client.GetByteArrayAsync($"/MktTools/DownloadScrubbed?batchId={r.BatchId}&fileId={r.Json.GetProperty("fileId").GetString()}");
        CollectionAssert.AreEqual(content, downloaded);
    }

    [TestMethod]
    public async Task Scrub_Mp4_RemovesContainerMetadata_UsingFfmpeg()
    {
        if (!FfmpegAvailable()) Assert.Inconclusive("ffmpeg não está no PATH: teste de vídeo não executado.");
        var (factory, client, _, db) = await LoggedIn("Sqlite");
        await using var _1 = db; using var _2 = factory; using var _3 = client;

        var input = BuildMp4();
        Assert.IsTrue(Contains(input, "SYNTH_TITLE_SECRET"), "pré-condição: vídeo sintético traz o título");
        var r = await Upload(client, "clipe.mp4", input);
        Assert.AreEqual(HttpStatusCode.OK, r.Status, r.Json.ToString());
        Assert.IsTrue(r.Json.GetProperty("supported").GetBoolean());
        var output = await client.GetByteArrayAsync($"/MktTools/DownloadScrubbed?batchId={r.BatchId}&fileId={r.Json.GetProperty("fileId").GetString()}");
        Assert.IsFalse(Contains(output, "SYNTH_TITLE_SECRET"));
        Assert.IsFalse(Contains(output, "SYNTH_VIDEO_COMMENT_SECRET"));
        Assert.IsGreaterThan(0, output.Length);
        if (r.Json.GetProperty("before").EnumerateObject().Any())
        {
            Assert.IsTrue(AnyTagContains(r.Json.GetProperty("before"), "SYNTH_"), "ffprobe deveria listar as tags antes");
            Assert.IsFalse(AnyTagContains(r.Json.GetProperty("after"), "SYNTH_"), "ffprobe não deve listar as tags depois");
        }
    }

    [TestMethod]
    public async Task Scrub_Errors_AndValidations_LeaveNoPartialFiles()
    {
        var (factory, client, email, db) = await LoggedIn("Sqlite");
        await using var _1 = db; using var _2 = factory; using var _3 = client;

        // arquivo corrompido (JPEG inválido): 500 com mensagem e sem sobras
        var bad = await Upload(client, "quebrado.jpg", Encoding.ASCII.GetBytes("isto nao e um jpeg"));
        Assert.AreEqual(HttpStatusCode.InternalServerError, bad.Status);
        StringAssert.Contains(bad.Json.GetProperty("error").GetString()!, "JPEG");
        var dir = BatchDir("Sqlite", db.Connection, email, bad.BatchId);
        Assert.AreEqual(0, Directory.GetFiles(dir).Length, "falha não pode deixar .in/.out/.name");

        // arquivo vazio
        var empty = await Upload(client, "vazio.png", []);
        Assert.AreEqual(HttpStatusCode.BadRequest, empty.Status);

        // lote inválido
        var badBatch = await Upload(client, "a.png", BuildPng(), "../../etc");
        Assert.AreEqual(HttpStatusCode.BadRequest, badBatch.Status);

        // download com ids inválidos/inexistentes
        Assert.AreEqual(HttpStatusCode.NotFound, (await client.GetAsync($"/MktTools/DownloadScrubbed?batchId={Guid.NewGuid():N}&fileId={Guid.NewGuid():N}")).StatusCode);
        Assert.AreEqual(HttpStatusCode.NotFound, (await client.GetAsync("/MktTools/DownloadScrubbed?batchId=x&fileId=..%2F..%2Fx")).StatusCode);
    }

    [TestMethod]
    public async Task Scrub_RequiresLogin_AndIsolatesBatchesPerUser()
    {
        var (factory, admin, _, db) = await LoggedIn("Sqlite");
        await using var _1 = db; using var _2 = factory; using var _3 = admin;

        using (var anon = NewClient(factory))
        {
            var r = await Upload(anon, "a.png", BuildPng());
            Assert.AreEqual(HttpStatusCode.Redirect, r.Status);
        }

        var mine = await Upload(admin, "a.png", BuildPng());
        var fileId = mine.Json.GetProperty("fileId").GetString();

        var otherEmail = RandomEmail();
        var otherPw = RandomPassword();
        using (var scope = factory.Services.CreateScope())
            Assert.IsTrue((await scope.ServiceProvider.GetRequiredService<UserManager<IdentityUser>>()
                .CreateAsync(new IdentityUser { UserName = otherEmail, Email = otherEmail, EmailConfirmed = true }, otherPw)).Succeeded);
        using var other = NewClient(factory);
        Assert.AreEqual(HttpStatusCode.Redirect, (await Login(other, otherEmail, otherPw)).StatusCode);
        Assert.AreEqual(HttpStatusCode.NotFound, (await other.GetAsync($"/MktTools/DownloadScrubbed?batchId={mine.BatchId}&fileId={fileId}")).StatusCode,
            "outro usuário não pode baixar o resultado");
        Assert.AreEqual(HttpStatusCode.OK, (await admin.GetAsync($"/MktTools/DownloadScrubbed?batchId={mine.BatchId}&fileId={fileId}")).StatusCode);
    }

    [TestMethod]
    public async Task Scrub_BatchZip_HandlesDuplicateNames_AndOldBatchesAreRemovedOnNextScrub()
    {
        var (factory, client, email, db) = await LoggedIn("Sqlite");
        await using var _1 = db; using var _2 = factory; using var _3 = client;

        var batch = Guid.NewGuid().ToString("N");
        Assert.AreEqual(HttpStatusCode.OK, (await Upload(client, "dup.png", BuildPng(), batch)).Status);
        Assert.AreEqual(HttpStatusCode.OK, (await Upload(client, "dup.png", BuildPng(), batch)).Status);

        var zipBytes = await client.GetByteArrayAsync($"/MktTools/DownloadBatchZip?batchId={batch}");
        using (var zip = new ZipArchive(new MemoryStream(zipBytes)))
            CollectionAssert.AreEquivalent(new[] { "dup_clean.png", "dup_clean_2.png" }, zip.Entries.Select(e => e.FullName).ToArray());

        // retenção de 2 h: lote antigo é removido na próxima chamada de ScrubFile; lote recente permanece
        var oldDir = BatchDir("Sqlite", db.Connection, email, batch);
        Directory.SetLastWriteTimeUtc(oldDir, DateTime.UtcNow.AddHours(-3));
        var fresh = await Upload(client, "novo.png", BuildPng());
        Assert.AreEqual(HttpStatusCode.OK, fresh.Status);
        Assert.IsFalse(Directory.Exists(oldDir), "lote com mais de 2 h deve ser removido");
        Assert.IsTrue(Directory.Exists(BatchDir("Sqlite", db.Connection, email, fresh.BatchId)));
        Assert.AreEqual(HttpStatusCode.NotFound, (await client.GetAsync($"/MktTools/DownloadBatchZip?batchId={batch}")).StatusCode);
    }
}
