using System.ComponentModel.DataAnnotations;

namespace HackerToolbox.Models
{
    public class ShortUrl
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public string OriginalUrl { get; set; }

        [Required]
        [StringLength(10)]
        public string Code { get; set; } // O código encurtado (ex: a7X9b)

        public int ClickCount { get; set; } = 0; // Contador de cliques

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}