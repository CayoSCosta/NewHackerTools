using HackerToolbox.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace HackerToolbox.Migrations.Postgres
{
    // Usada apenas pelo 'dotnet ef' neste projeto. Sempre PostgreSQL, sem fallback implícito.
    // A connection string vem de HBX_DB_CONNECTION (nunca versionada).
    public class PostgresDesignTimeFactory : IDesignTimeDbContextFactory<ApplicationDbContext>
    {
        public ApplicationDbContext CreateDbContext(string[] args)
        {
            // HBX_PG_HOST/PORT/DATABASE/USERNAME + HBX_PG_PASSWORD_FILE (ou HBX_PG_PASSWORD) montam a conexão com escaping
            // correto; HBX_DB_CONNECTION (string completa) continua aceita como alternativa.
            var connection = PostgresConnectionString.Build(k => Environment.GetEnvironmentVariable(
                    "HBX_PG_" + (k == "PasswordFile" ? "PASSWORD_FILE" : k.ToUpperInvariant())))
                ?? Environment.GetEnvironmentVariable("HBX_DB_CONNECTION");
            var optionsBuilder = new DbContextOptionsBuilder<ApplicationDbContext>();
            DatabaseProviderConfigurator.Configure(optionsBuilder, DatabaseProviderConfigurator.Postgres, connection);
            return new ApplicationDbContext(optionsBuilder.Options);
        }
    }
}
