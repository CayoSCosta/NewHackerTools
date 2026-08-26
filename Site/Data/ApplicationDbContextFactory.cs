using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace HackerToolbox.Data
{
    public class ApplicationDbContextFactory : IDesignTimeDbContextFactory<ApplicationDbContext>
    {
        public ApplicationDbContext CreateDbContext(string[] args)
        {
            var optionsBuilder = new DbContextOptionsBuilder<ApplicationDbContext>();
            
            // Aponta explicitamente para o arquivo SQLite local em tempo de design
            optionsBuilder.UseSqlite("Data Source=hackerbox.db");

            return new ApplicationDbContext(optionsBuilder.Options);
        }
    }
}