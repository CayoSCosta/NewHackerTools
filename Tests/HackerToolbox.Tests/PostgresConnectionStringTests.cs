using HackerToolbox.Data;
using Npgsql;

namespace HackerToolbox.Tests;

// Senhas descartáveis com caracteres que quebrariam uma concatenação ingênua de connection string.
[TestClass]
public class PostgresConnectionStringTests
{
    private static readonly string[] SpecialPasswords =
    [
        "pa ss;word=1",
        "semi;colon;;end",
        "with 'single' and \"double\" quotes",
        "  leading and trailing  ",
        "equals=sign=here",
        "Host=evil;Username=x",
        "unicode-çãõ-€-日本",
        "back\\slash{brace}"
    ];

    private static Func<string, string?> Settings(string password, string? file = null) => k => k switch
    {
        "Host" => "db",
        "Port" => "5432",
        "Database" => "my db",
        "Username" => "hbx user",
        "Password" => file == null ? password : null,
        "PasswordFile" => file,
        _ => null
    };

    [TestMethod]
    public void Build_RoundTripsSpecialPasswords_ThroughNpgsqlParser()
    {
        foreach (var pw in SpecialPasswords)
        {
            var parsed = new NpgsqlConnectionStringBuilder(PostgresConnectionString.Build(Settings(pw))!);
            Assert.AreEqual(pw, parsed.Password);
            Assert.AreEqual("db", parsed.Host);
            Assert.AreEqual("my db", parsed.Database);
            Assert.AreEqual("hbx user", parsed.Username);
            Assert.AreEqual(5432, parsed.Port);
        }
    }

    [TestMethod]
    public void Build_ReadsPasswordFile_TrimmingOnlyTrailingNewlines()
    {
        var file = Path.Combine(Path.GetTempPath(), $"hbx-secret-{Guid.NewGuid():N}");
        try
        {
            foreach (var pw in SpecialPasswords)
            {
                File.WriteAllText(file, pw + "\r\n");
                var parsed = new NpgsqlConnectionStringBuilder(PostgresConnectionString.Build(Settings("", file))!);
                Assert.AreEqual(pw, parsed.Password, "espaços e símbolos devem ser preservados; só a quebra de linha final sai");
            }
        }
        finally { File.Delete(file); }
    }

    [TestMethod]
    public void Build_WithoutHost_ReturnsNull_AndInvalidConfigFailsClearly()
    {
        Assert.IsNull(PostgresConnectionString.Build(_ => null));

        var both = Assert.Throws<InvalidOperationException>(() =>
            PostgresConnectionString.Build(k => k is "Host" or "Database" or "Username" ? "x" : k is "Password" ? "p" : k is "PasswordFile" ? "f" : null));
        StringAssert.Contains(both.Message, "PasswordFile");

        var noPassword = Assert.Throws<InvalidOperationException>(() =>
            PostgresConnectionString.Build(k => k is "Host" or "Database" or "Username" ? "x" : null));
        StringAssert.Contains(noPassword.Message, "Senha");

        var missingFile = Assert.Throws<InvalidOperationException>(() =>
            PostgresConnectionString.Build(Settings("", Path.Combine(Path.GetTempPath(), "nao-existe-" + Guid.NewGuid().ToString("N")))));
        StringAssert.Contains(missingFile.Message, "não encontrado");
    }

    [TestMethod]
    public async Task Postgres_ConnectsWithSpecialCharacterPasswords()
    {
        var baseCs = Environment.GetEnvironmentVariable("HBX_TEST_PG_CONNECTION");
        if (string.IsNullOrWhiteSpace(baseCs))
            Assert.Inconclusive("HBX_TEST_PG_CONNECTION não definida: teste PostgreSQL não executado.");
        var admin = new NpgsqlConnectionStringBuilder(baseCs);

        await using var conn = new NpgsqlConnection(admin.ConnectionString);
        await conn.OpenAsync();
        foreach (var pw in SpecialPasswords)
        {
            var role = "hbx_t_" + Guid.NewGuid().ToString("N")[..12];
            // format(%I/%L) faz o quoting correto de identificador e literal no servidor
            await using (var q = new NpgsqlCommand("select format('create role %I login password %L', @r, @p)", conn))
            {
                q.Parameters.AddWithValue("r", role);
                q.Parameters.AddWithValue("p", pw);
                var ddl = (string)(await q.ExecuteScalarAsync())!;
                await using var create = new NpgsqlCommand(ddl, conn);
                await create.ExecuteNonQueryAsync();
            }
            try
            {
                var cs = PostgresConnectionString.Build(k => k switch
                {
                    "Host" => admin.Host,
                    "Port" => admin.Port.ToString(),
                    "Database" => admin.Database,
                    "Username" => role,
                    "Password" => pw,
                    _ => null
                })!;
                await using var c2 = new NpgsqlConnection(cs);
                await c2.OpenAsync();
                await using var who = new NpgsqlCommand("select current_user", c2);
                Assert.AreEqual(role, (string)(await who.ExecuteScalarAsync())!);
            }
            finally
            {
                NpgsqlConnection.ClearAllPools();
                await using var drop = new NpgsqlCommand($"drop role if exists \"{role}\"", conn);
                await drop.ExecuteNonQueryAsync();
            }
        }
    }
}
