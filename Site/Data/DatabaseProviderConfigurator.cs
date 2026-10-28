using Microsoft.EntityFrameworkCore;

namespace HackerToolbox.Data
{
    public static class DatabaseProviderConfigurator
    {
        public const string Sqlite = "Sqlite";
        public const string Postgres = "Postgres";

        // Assembly separado que contém as migrations PostgreSQL (projeto Migrations.Postgres).
        public const string PostgresMigrationsAssembly = "HackerToolbox.Migrations.Postgres";

        public const string DefaultSqliteConnection = "Data Source=hackerbox.db";

        public static string NormalizeProvider(string? provider)
        {
            if (string.IsNullOrWhiteSpace(provider) || provider.Equals(Sqlite, StringComparison.OrdinalIgnoreCase))
                return Sqlite;
            if (provider.Equals(Postgres, StringComparison.OrdinalIgnoreCase) ||
                provider.Equals("PostgreSQL", StringComparison.OrdinalIgnoreCase))
                return Postgres;
            throw new InvalidOperationException(
                $"Database:Provider inválido: '{provider}'. Valores aceitos: {Sqlite}, {Postgres}.");
        }

        // Falha cedo (e com mensagem clara) se a publicação não trouxe o assembly de migrations PostgreSQL.
        // O assembly não consta no deps.json do web, então é carregado pelo caminho para que o EF o encontre pelo nome.
        private static void EnsurePostgresMigrationsAssemblyAvailable()
        {
            if (AppDomain.CurrentDomain.GetAssemblies().Any(a => a.GetName().Name == PostgresMigrationsAssembly))
                return;

            var path = Path.Combine(AppContext.BaseDirectory, PostgresMigrationsAssembly + ".dll");
            if (!File.Exists(path))
                throw new InvalidOperationException(
                    $"Assembly de migrations '{PostgresMigrationsAssembly}' não encontrado em '{AppContext.BaseDirectory}'. Publique o projeto web com 'dotnet publish' (que o inclui).");

            System.Reflection.Assembly.LoadFrom(path);
        }

        public static void Configure(DbContextOptionsBuilder options, string? provider, string? connectionString)
        {
            switch (NormalizeProvider(provider))
            {
                case Postgres:
                    if (string.IsNullOrWhiteSpace(connectionString))
                        throw new InvalidOperationException(
                            "O provider Postgres exige uma connection string explícita (ConnectionStrings:DefaultConnection).");
                    options.UseNpgsql(connectionString, npgsql => npgsql.MigrationsAssembly(PostgresMigrationsAssembly));
                    EnsurePostgresMigrationsAssemblyAvailable();
                    break;
                default:
                    options.UseSqlite(string.IsNullOrWhiteSpace(connectionString) ? DefaultSqliteConnection : connectionString);
                    break;
            }
        }
    }
}
