using HackerToolbox.Controllers;
using HackerToolbox.Data;
using HackerToolbox.Models;
using HackerToolbox.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace HackerToolbox.Tests;

// Testa o encurtador na camada do controller (chamado diretamente, sem pipeline HTTP/autenticação)
// contra SQLite (arquivo temporário) e PostgreSQL (banco descartável via HBX_TEST_PG_CONNECTION).
[TestClass]
public class ShortenerDateTests
{
    private sealed class NullTempDataProvider : ITempDataProvider
    {
        public IDictionary<string, object> LoadTempData(HttpContext context) => new Dictionary<string, object>();
        public void SaveTempData(HttpContext context, IDictionary<string, object> values) { }
    }

    private static ShortenerController NewController(ApplicationDbContext db)
    {
        var http = new DefaultHttpContext();
        return new ShortenerController(db)
        {
            ControllerContext = new ControllerContext { HttpContext = http },
            TempData = new TempDataDictionary(http, new NullTempDataProvider())
        };
    }

    // ---------- fábricas de banco isoladas ----------

    private static ApplicationDbContext NewSqlite(string file)
    {
        var o = new DbContextOptionsBuilder<ApplicationDbContext>();
        DatabaseProviderConfigurator.Configure(o, "Sqlite", $"Data Source={file}");
        return new ApplicationDbContext(o.Options);
    }

    private static string? PgBase => Environment.GetEnvironmentVariable("HBX_TEST_PG_CONNECTION");

    private static ApplicationDbContext NewPg(string connection)
    {
        var o = new DbContextOptionsBuilder<ApplicationDbContext>();
        DatabaseProviderConfigurator.Configure(o, "Postgres", connection);
        return new ApplicationDbContext(o.Options);
    }

    private static string PgUniqueDb()
    {
        if (string.IsNullOrWhiteSpace(PgBase))
            Assert.Inconclusive("HBX_TEST_PG_CONNECTION não definida: teste PostgreSQL não executado.");
        var b = new NpgsqlConnectionStringBuilder(PgBase) { Database = "hbx_test_" + Guid.NewGuid().ToString("N") };
        return b.ConnectionString;
    }

    private static async Task<List<ShortUrl>> ReadAll(ApplicationDbContext db) =>
        await db.ShortUrls.AsNoTracking().OrderBy(u => u.Id).ToListAsync();

    // ---------- unidade: conversão de exibição ----------

    [TestMethod]
    public void BrasiliaTime_ConvertsUtcAndUnspecifiedAsUtc_WithoutDoubleConversion()
    {
        var utc = new DateTime(2026, 10, 27, 23, 30, 0, DateTimeKind.Utc);
        var unspecified = DateTime.SpecifyKind(utc, DateTimeKind.Unspecified);

        Assert.AreEqual("27/10/2026 20:30", BrasiliaTime.Format(utc));
        Assert.AreEqual("27/10/2026 20:30", BrasiliaTime.Format(unspecified));
        // chamar ToBrasilia não muda o valor de entrada
        Assert.AreEqual(DateTimeKind.Utc, utc.Kind);
        Assert.AreEqual(new DateTime(2026, 10, 27, 20, 30, 0), BrasiliaTime.ToBrasilia(utc));
    }

    [TestMethod]
    public void NewShortUrl_CreatedAt_IsUtc()
    {
        var before = DateTime.UtcNow;
        var s = new ShortUrl();
        Assert.AreEqual(DateTimeKind.Utc, s.CreatedAt.Kind);
        Assert.IsTrue(s.CreatedAt >= before && s.CreatedAt <= DateTime.UtcNow);
    }

    // ---------- SQLite ----------

    [TestMethod]
    public async Task Sqlite_CreateRedirectOrderAndDisplay()
    {
        var file = Path.Combine(Path.GetTempPath(), $"hbx-test-{Guid.NewGuid():N}.db");
        try
        {
            await using (var db = NewSqlite(file)) await db.Database.MigrateAsync();

            await using (var db = NewSqlite(file))
            {
                var c = NewController(db);
                await c.Create("example.com/a");
                await Task.Delay(20);
                await c.Create("https://example.com/b");
            }

            await using (var db = NewSqlite(file))
            {
                var rows = await ReadAll(db);
                Assert.AreEqual(2, rows.Count);
                Assert.AreEqual("https://example.com/a", rows[0].OriginalUrl);
                Assert.AreEqual(6, rows[0].Code.Length);

                // SQLite não guarda Kind: materializa Unspecified, mas o valor de relógio é o UTC gravado.
                Assert.AreEqual(DateTimeKind.Unspecified, rows[0].CreatedAt.Kind);
                var nowUtc = DateTime.UtcNow;
                Assert.IsTrue(Math.Abs((rows[0].CreatedAt - nowUtc).TotalMinutes) < 5, "valor gravado deve ser UTC");

                // Exibição: UTC (Unspecified assumido como UTC) -> Brasília, uma única conversão
                Assert.AreEqual(BrasiliaTime.Format(DateTime.SpecifyKind(rows[0].CreatedAt, DateTimeKind.Utc)),
                                BrasiliaTime.Format(rows[0].CreatedAt));

                var index = (ViewResult)await NewController(db).Index();
                var ordered = ((IEnumerable<ShortUrl>)index.Model!).ToList();
                Assert.AreEqual(rows[1].Code, ordered[0].Code, "mais recente primeiro");

                var redirect = (RedirectResult)await NewController(db).RedirectToOriginal(rows[0].Code);
                Assert.AreEqual("https://example.com/a", redirect.Url);
            }

            await using (var db = NewSqlite(file))
                Assert.AreEqual(1, (await ReadAll(db))[0].ClickCount);
        }
        finally
        {
            Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
            foreach (var f in new[] { file, file + "-wal", file + "-shm" }) if (File.Exists(f)) File.Delete(f);
        }
    }

    // ---------- PostgreSQL ----------

    [TestMethod]
    public async Task Postgres_CreateRedirectOrderAndDisplay_UtcInstantPreserved()
    {
        var cs = PgUniqueDb();
        try
        {
            await using (var db = NewPg(cs)) await db.Database.MigrateAsync();

            var before = DateTime.UtcNow;
            await using (var db = NewPg(cs))
            {
                var c = NewController(db);
                await c.Create("example.com/a");
                await Task.Delay(20);
                await c.Create("https://example.com/b");
            }

            await using (var db = NewPg(cs))
            {
                var rows = await ReadAll(db);
                Assert.AreEqual(2, rows.Count);
                Assert.AreEqual(DateTimeKind.Utc, rows[0].CreatedAt.Kind, "Npgsql materializa timestamptz como Utc");
                Assert.IsTrue(rows[0].CreatedAt >= before.AddSeconds(-1) && rows[0].CreatedAt <= DateTime.UtcNow.AddSeconds(1));

                var index = (ViewResult)await NewController(db).Index();
                Assert.AreEqual(rows[1].Code, ((IEnumerable<ShortUrl>)index.Model!).First().Code);

                var redirect = (RedirectResult)await NewController(db).RedirectToOriginal(rows[1].Code);
                Assert.AreEqual("https://example.com/b", redirect.Url);

                // O instante armazenado é independente do TimeZone da sessão e a exibição em Brasília bate com o PostgreSQL.
                await using var conn = new NpgsqlConnection(cs);
                await conn.OpenAsync();
                foreach (var tz in new[] { "UTC", "Asia/Tokyo" })
                {
                    await using (var set = new NpgsqlCommand($"SET TIME ZONE '{tz}'", conn)) await set.ExecuteNonQueryAsync();
                    await using var q = new NpgsqlCommand(
                        "select to_char(\"CreatedAt\" at time zone 'America/Sao_Paulo','DD/MM/YYYY HH24:MI'), extract(epoch from \"CreatedAt\") from \"ShortUrls\" where \"Id\" = @id", conn);
                    q.Parameters.AddWithValue("id", rows[0].Id);
                    await using var r = await q.ExecuteReaderAsync();
                    Assert.IsTrue(await r.ReadAsync());
                    Assert.AreEqual(BrasiliaTime.Format(rows[0].CreatedAt), r.GetString(0), $"TimeZone da sessão: {tz}");
                    var epochMs = (double)r.GetDecimal(1) * 1000;
                    var expectedMs = (rows[0].CreatedAt - DateTime.UnixEpoch).TotalMilliseconds;
                    Assert.IsTrue(Math.Abs(epochMs - expectedMs) < 1, $"instante igual em {tz}");
                }
            }

            await using (var db = NewPg(cs))
                Assert.AreEqual(1, (await ReadAll(db))[1].ClickCount);
        }
        finally
        {
            await using var db = NewPg(cs);
            await db.Database.EnsureDeletedAsync();
        }
    }

    [TestMethod]
    public async Task Postgres_RejectsNonUtcKinds_ForTimestamptz()
    {
        var cs = PgUniqueDb();
        try
        {
            await using (var db = NewPg(cs)) await db.Database.MigrateAsync();

            foreach (var kind in new[] { DateTimeKind.Local, DateTimeKind.Unspecified })
            {
                await using var db = NewPg(cs);
                db.ShortUrls.Add(new ShortUrl
                {
                    OriginalUrl = "https://x.test",
                    Code = "k" + (int)kind,
                    CreatedAt = DateTime.SpecifyKind(DateTime.Now, kind)
                });
                var ex = await Assert.ThrowsAsync<Exception>(() => db.SaveChangesAsync(), $"Kind={kind}");
                Console.WriteLine($"Kind={kind}: {ex.GetType().Name} / {ex.InnerException?.Message ?? ex.Message}");
            }
        }
        finally
        {
            await using var db = NewPg(cs);
            await db.Database.EnsureDeletedAsync();
        }
    }
}
