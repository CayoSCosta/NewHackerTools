using System.Net;
using System.Text.RegularExpressions;
using HackerToolbox.Data;
using HackerToolbox.Services;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;

namespace HackerToolbox.Tests;

// Testes de integração do aplicativo web (pipeline HTTP real em memória) com bancos descartáveis.
// SQLite: arquivo temporário. PostgreSQL: HBX_TEST_PG_CONNECTION (container descartável; veja docs/DATABASE.md).
// Nenhuma senha é versionada: todas são geradas em tempo de execução.
[TestClass]
public class AdminSeedAndAuthTests
{
    // ---------- infraestrutura ----------

    internal sealed class TestDb : IAsyncDisposable
    {
        public required string Provider { get; init; }
        public required string Connection { get; init; }
        private Func<Task>? _cleanup;
        public static TestDb Create(string provider, Func<Task>? cleanup = null) => throw new NotSupportedException();
        public TestDb WithCleanup(Func<Task> c) { _cleanup = c; return this; }
        public async ValueTask DisposeAsync() { if (_cleanup != null) await _cleanup(); }
    }

    internal static ApplicationDbContext Ctx(string provider, string cs)
    {
        var o = new DbContextOptionsBuilder<ApplicationDbContext>();
        DatabaseProviderConfigurator.Configure(o, provider, cs);
        return new ApplicationDbContext(o.Options);
    }

    // Cria um banco vazio, aplica as migrations do provider (explicitamente, como no procedimento documentado).
    internal static async Task<TestDb> NewMigratedDb(string provider)
    {
        if (provider == "Sqlite")
        {
            var file = Path.Combine(Path.GetTempPath(), $"hbx-auth-{Guid.NewGuid():N}.db");
            var cs = $"Data Source={file}";
            await using (var db = Ctx(provider, cs)) await db.Database.MigrateAsync();
            return new TestDb { Provider = provider, Connection = cs }.WithCleanup(() =>
            {
                Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
                foreach (var f in new[] { file, file + "-wal", file + "-shm" }) if (File.Exists(f)) File.Delete(f);
                return Task.CompletedTask;
            });
        }

        var baseCs = Environment.GetEnvironmentVariable("HBX_TEST_PG_CONNECTION");
        if (string.IsNullOrWhiteSpace(baseCs))
            Assert.Inconclusive("HBX_TEST_PG_CONNECTION não definida: teste PostgreSQL não executado.");
        var pg = new NpgsqlConnectionStringBuilder(baseCs) { Database = "hbx_test_" + Guid.NewGuid().ToString("N") }.ConnectionString;
        await using (var db = Ctx(provider, pg)) await db.Database.MigrateAsync();
        return new TestDb { Provider = provider, Connection = pg }.WithCleanup(async () =>
        {
            NpgsqlConnection.ClearAllPools();
            await using var db = Ctx(provider, pg);
            await db.Database.EnsureDeletedAsync();
        });
    }

    internal static string RandomPassword() => "Aa1!" + Guid.NewGuid().ToString("N");
    internal static string RandomEmail() => $"u{Guid.NewGuid():N}@example.test";

    private static string FindSiteDir()
    {
        for (var d = new DirectoryInfo(AppContext.BaseDirectory); d != null; d = d.Parent)
        {
            var site = Path.Combine(d.FullName, "Site");
            if (File.Exists(Path.Combine(site, "HackerToolbox.csproj"))) return site;
        }
        throw new InvalidOperationException("Diretório Site não encontrado.");
    }

    internal sealed class AppFactory(TestDb db, string? seedEmail, string? seedPassword) : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(Microsoft.AspNetCore.Hosting.IWebHostBuilder builder)
        {
            // "Testing" (não Development) evita carregar User Secrets reais da máquina.
            builder.UseEnvironment("Testing");
            builder.UseContentRoot(FindSiteDir());
            builder.UseSetting("Database:Provider", db.Provider);
            builder.UseSetting("ConnectionStrings:DefaultConnection", db.Connection);
            builder.UseSetting("AdminSeed:Email", seedEmail ?? "");
            builder.UseSetting("AdminSeed:Password", seedPassword ?? "");
        }
    }

    internal static HttpClient NewClient(AppFactory f) =>
        f.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, HandleCookies = true });

    internal static async Task<HttpResponseMessage> Login(HttpClient c, string email, string password)
    {
        var page = await c.GetStringAsync("/Identity/Account/Login");
        var token = Regex.Match(page, "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"").Groups[1].Value;
        Assert.IsFalse(string.IsNullOrEmpty(token), "token antiforgery não encontrado na página de login");
        return await c.PostAsync("/Identity/Account/Login", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["Input.Email"] = email,
            ["Input.Password"] = password,
            ["Input.RememberMe"] = "false",
            ["__RequestVerificationToken"] = token
        }));
    }

    private static bool IsLoginRedirect(HttpResponseMessage r) =>
        r.StatusCode == HttpStatusCode.Redirect && (r.Headers.Location?.ToString().Contains("/Identity/Account/Login", StringComparison.OrdinalIgnoreCase) ?? false);

    // ---------- migrations ----------

    [TestMethod]
    public async Task Postgres_Migrations_ApplyOnEmptyDatabase_CreateIdentityAndShortUrls()
    {
        await using var db = await NewMigratedDb("Postgres");
        await using var ctx = Ctx("Postgres", db.Connection);

        Assert.AreEqual(0, (await ctx.Database.GetPendingMigrationsAsync()).Count());
        CollectionAssert.AreEqual(new[] { "InitialPostgres" },
            (await ctx.Database.GetAppliedMigrationsAsync()).Select(m => m[(m.IndexOf('_') + 1)..]).ToArray());

        var expected = new[] { "AspNetRoleClaims", "AspNetRoles", "AspNetUserClaims", "AspNetUserLogins", "AspNetUserRoles", "AspNetUserTokens", "AspNetUsers", "ShortUrls", "__EFMigrationsHistory" };
        await using var conn = new NpgsqlConnection(db.Connection);
        await conn.OpenAsync();
        await using var cmd = new NpgsqlCommand("select table_name from information_schema.tables where table_schema='public' order by 1", conn);
        var tables = new List<string>();
        await using (var r = await cmd.ExecuteReaderAsync()) while (await r.ReadAsync()) tables.Add(r.GetString(0));
        CollectionAssert.AreEquivalent(expected, tables);

        // Base nasce vazia (nada é importado do SQLite) e reaplicar é um no-op.
        Assert.AreEqual(0, await ctx.Users.CountAsync());
        Assert.AreEqual(0, await ctx.ShortUrls.CountAsync());
        await ctx.Database.MigrateAsync();
        Assert.AreEqual(1, (await ctx.Database.GetAppliedMigrationsAsync()).Count());
    }

    // ---------- seed ----------

    [TestMethod]
    [DataRow("Sqlite")]
    [DataRow("Postgres")]
    public async Task Seed_NotConfigured_StartsAndCreatesNoUserOrRole(string provider)
    {
        await using var db = await NewMigratedDb(provider);
        await using var factory = new AppFactory(db, null, null);
        using var client = NewClient(factory);
        Assert.AreEqual(HttpStatusCode.OK, (await client.GetAsync("/healthz")).StatusCode);

        await using var ctx = Ctx(provider, db.Connection);
        Assert.AreEqual(0, await ctx.Users.CountAsync(), "sem configuração não pode existir conta padrão");
        Assert.AreEqual(0, await ctx.Roles.CountAsync());
    }

    [TestMethod]
    [DataRow("Sqlite")]
    [DataRow("Postgres")]
    public async Task Seed_CreatesRoleAndAdmin_IsIdempotent_AndNeverResetsPassword(string provider)
    {
        await using var db = await NewMigratedDb(provider);
        var email = RandomEmail();
        var password = RandomPassword();
        await using var factory = new AppFactory(db, email, password);
        using var client = NewClient(factory); // dispara o startup (e o seed)

        string hashBefore;
        await using (var ctx = Ctx(provider, db.Connection))
        {
            var user = await ctx.Users.SingleAsync();
            Assert.AreEqual(email, user.Email);
            Assert.IsTrue(user.EmailConfirmed);
            Assert.AreEqual(1, await ctx.Roles.CountAsync(r => r.Name == "Admin"));
            var roleId = (await ctx.Roles.SingleAsync()).Id;
            Assert.AreEqual(1, await ctx.UserRoles.CountAsync(ur => ur.UserId == user.Id && ur.RoleId == roleId));
            hashBefore = user.PasswordHash!;
            Assert.IsFalse(hashBefore.Contains(password), "a senha não pode ser armazenada em claro");
        }

        // Reaplicação, inclusive com OUTRA senha configurada: nada muda.
        using var scope = factory.Services.CreateScope();
        var logger = NullLogger.Instance;
        Assert.AreEqual(AdminSeedOutcome.AlreadyExisted, await AdminSeeder.SeedAsync(scope.ServiceProvider, email, password, logger));
        Assert.AreEqual(AdminSeedOutcome.AlreadyExisted, await AdminSeeder.SeedAsync(scope.ServiceProvider, email, RandomPassword(), logger));

        await using (var ctx = Ctx(provider, db.Connection))
        {
            Assert.AreEqual(1, await ctx.Users.CountAsync());
            Assert.AreEqual(1, await ctx.Roles.CountAsync());
            Assert.AreEqual(1, await ctx.UserRoles.CountAsync());
            Assert.AreEqual(hashBefore, (await ctx.Users.SingleAsync()).PasswordHash, "senha de usuário existente não pode ser redefinida");
        }

        // Usuário existente sem a role recebe a role de volta, sem mexer na senha.
        await using (var ctx = Ctx(provider, db.Connection))
        {
            ctx.UserRoles.RemoveRange(ctx.UserRoles);
            await ctx.SaveChangesAsync();
        }
        using var freshScope = factory.Services.CreateScope();
        Assert.AreEqual(AdminSeedOutcome.AlreadyExisted, await AdminSeeder.SeedAsync(freshScope.ServiceProvider, email, password, logger));
        await using (var ctx = Ctx(provider, db.Connection))
        {
            Assert.AreEqual(1, await ctx.UserRoles.CountAsync());
            Assert.AreEqual(hashBefore, (await ctx.Users.SingleAsync()).PasswordHash);
        }
    }

    [TestMethod]
    public async Task Seed_InvalidConfiguration_FailsWithClearMessage_WithoutLeakingPassword()
    {
        await using var db = await NewMigratedDb("Sqlite");
        await using var factory = new AppFactory(db, null, null);
        using var client = NewClient(factory);
        using var scope = factory.Services.CreateScope();
        var sp = scope.ServiceProvider;
        var secret = "Weak" + Guid.NewGuid().ToString("N")[..3]; // curto demais para a política (6) só se < 6; usamos abaixo
        var tooShort = "ab1";

        var onlyEmail = await Assert.ThrowsAsync<AdminSeedException>(() => AdminSeeder.SeedAsync(sp, RandomEmail(), null, NullLogger.Instance));
        StringAssert.Contains(onlyEmail.Message, "AdminSeed:Email");
        var onlyPassword = await Assert.ThrowsAsync<AdminSeedException>(() => AdminSeeder.SeedAsync(sp, " ", secret, NullLogger.Instance));
        Assert.IsFalse(onlyPassword.Message.Contains(secret));

        var badEmail = await Assert.ThrowsAsync<AdminSeedException>(() => AdminSeeder.SeedAsync(sp, "nao-e-email", secret, NullLogger.Instance));
        StringAssert.Contains(badEmail.Message, "e-mail");
        Assert.IsFalse(badEmail.Message.Contains(secret));

        var weak = await Assert.ThrowsAsync<AdminSeedException>(() => AdminSeeder.SeedAsync(sp, RandomEmail(), tooShort, NullLogger.Instance));
        StringAssert.Contains(weak.Message, "PasswordTooShort");
        Assert.IsFalse(weak.Message.Contains(tooShort + " "), "mensagem não deve repetir a senha");

        await using var ctx = Ctx("Sqlite", db.Connection);
        Assert.AreEqual(0, await ctx.Users.CountAsync(), "nenhum usuário parcial pode ser criado");
    }

    // ---------- login, rotas protegidas e encurtador via HTTP ----------

    [TestMethod]
    [DataRow("Sqlite")]
    [DataRow("Postgres")]
    public async Task Login_AdminSucceeds_WrongPasswordRejected_AdminRoutesProtected_ShortenerWorks(string provider)
    {
        await using var db = await NewMigratedDb(provider);
        var adminEmail = RandomEmail();
        var adminPassword = RandomPassword();
        await using var factory = new AppFactory(db, adminEmail, adminPassword);

        // anônimo: rota Admin redireciona para login
        using (var anon = NewClient(factory))
        {
            Assert.IsTrue(IsLoginRedirect(await anon.GetAsync("/Admin")), "anônimo deve ir para o login");
            Assert.IsTrue(IsLoginRedirect(await anon.GetAsync("/Shopee")), "o painel Shopee exige autenticação");
            Assert.IsTrue(IsLoginRedirect(await anon.GetAsync("/Shortener")), "painel do encurtador exige login");
            // rota pública do encurtador continua pública (404 para código inexistente, sem redirecionar ao login)
            Assert.AreEqual(HttpStatusCode.NotFound, (await anon.GetAsync("/s/inexistente")).StatusCode);
        }

        // senha incorreta é rejeitada (permanece na página de login, sem cookie de sessão)
        using (var bad = NewClient(factory))
        {
            var r = await Login(bad, adminEmail, RandomPassword());
            Assert.AreEqual(HttpStatusCode.OK, r.StatusCode);
            StringAssert.Contains(await r.Content.ReadAsStringAsync(), "Invalid login attempt");
            Assert.IsTrue(IsLoginRedirect(await bad.GetAsync("/Admin")));
        }

        // usuário comum (sem role Admin): autenticado, mas acesso negado ao painel Admin
        var userEmail = RandomEmail();
        var userPassword = RandomPassword();
        using (var scope = factory.Services.CreateScope())
        {
            var um = scope.ServiceProvider.GetRequiredService<UserManager<IdentityUser>>();
            var created = await um.CreateAsync(new IdentityUser { UserName = userEmail, Email = userEmail, EmailConfirmed = true }, userPassword);
            Assert.IsTrue(created.Succeeded);
        }
        using (var common = NewClient(factory))
        {
            Assert.AreEqual(HttpStatusCode.Redirect, (await Login(common, userEmail, userPassword)).StatusCode);
            var denied = await common.GetAsync("/Admin");
            Assert.AreEqual(HttpStatusCode.Redirect, denied.StatusCode);
            StringAssert.Contains(denied.Headers.Location!.ToString(), "AccessDenied");
            var shopeeDenied = await common.GetAsync("/Shopee");
            Assert.AreEqual(HttpStatusCode.Redirect, shopeeDenied.StatusCode);
            StringAssert.Contains(shopeeDenied.Headers.Location!.ToString(), "AccessDenied");
        }

        // administrador: login ok, acesso ao painel, cria e redireciona um link curto
        using var admin = NewClient(factory);
        var ok = await Login(admin, adminEmail, adminPassword);
        Assert.AreEqual(HttpStatusCode.Redirect, ok.StatusCode, "login do admin deve redirecionar");
        Assert.IsFalse(IsLoginRedirect(ok));
        Assert.AreEqual(HttpStatusCode.OK, (await admin.GetAsync("/Admin")).StatusCode);
        var create = await admin.PostAsync("/Shortener/Create", new FormUrlEncodedContent(new Dictionary<string, string> { ["originalUrl"] = "example.com/destino" }));
        Assert.AreEqual(HttpStatusCode.Redirect, create.StatusCode);

        string code;
        await using (var ctx = Ctx(provider, db.Connection))
        {
            var row = await ctx.ShortUrls.AsNoTracking().SingleAsync();
            Assert.AreEqual("https://example.com/destino", row.OriginalUrl);
            code = row.Code;
        }

        var list = await admin.GetAsync("/Shortener");
        Assert.AreEqual(HttpStatusCode.OK, list.StatusCode);
        StringAssert.Contains(await list.Content.ReadAsStringAsync(), code);

        using var anonymous = NewClient(factory);
        var redirect = await anonymous.GetAsync($"/s/{code}");
        Assert.AreEqual(HttpStatusCode.Redirect, redirect.StatusCode);
        Assert.AreEqual("https://example.com/destino", redirect.Headers.Location!.ToString());
        await using (var ctx = Ctx(provider, db.Connection))
            Assert.AreEqual(1, (await ctx.ShortUrls.AsNoTracking().SingleAsync()).ClickCount);
    }
}
