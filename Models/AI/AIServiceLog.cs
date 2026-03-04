using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace UniCP.Models.AI;

public class AIServiceLog
{
    [Key]
    public int Id { get; set; }

    public int UserId { get; set; } // AppUser uses int Key

    public string? PromptSnippet { get; set; }
    
    public int PromptTokens { get; set; }
    
    public int CompletionTokens { get; set; }
    
    [Column(TypeName = "decimal(18,4)")]
    public decimal Cost { get; set; }
    
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;

    public string ModelName { get; set; } = string.Empty;
}
