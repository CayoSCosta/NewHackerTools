using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace HackerToolbox.Data
{
    public class ApplicationDbContextFactory : IDesignTimeDbContextFactory<ApplicationDbContext>
    {
        public ApplicationDbContext CreateDbContext(string[] args)
        {
            var optionsBuilder = new DbContextOptionsBuilder<ApplicationDbContext>();

            // Provider explícito em design-time: HBX_DB_PROVIDER (Sqlite|Postgres, padrão Sqlite)
            // e HBX_DB_CONNECTION (obrigatória para Postgres; para Sqlite o padrão é o arquivo local).
            var provider = Environment.GetEnvironmentVariable("HBX_DB_PROVIDER");
            var connection = Environment.GetEnvironmentVariable("HBX_DB_CONNECTION");
            DatabaseProviderConfigurator.Configure(optionsBuilder, provider, connection);

            return new ApplicationDbContext(optionsBuilder.Options);
        }
    }
}