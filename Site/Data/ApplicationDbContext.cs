using HackerToolbox.Models;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace HackerToolbox.Data
{
    public class ApplicationDbContext : IdentityDbContext
    {
        public DbSet<ShortUrl> ShortUrls { get; set; }
        
        // Construtor principal usado pela aplicação em tempo de execução
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
        }

        // Construtor vazio / protegido opcional para ajudar o EF Core tools em design-time se necessário
        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            if (!optionsBuilder.IsConfigured)
            {
                // Fallback de segurança para o SQLite na raiz do projeto durante o comando 'dotnet ef'
                optionsBuilder.UseSqlite("Data Source=hackerbox.db");
            }
        }
    }
}