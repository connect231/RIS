using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace UniCP.Models.MsK;

public partial class AIServiceLog
{
    [Key]
    public int Id { get; set; }

    public int UserId { get; set; }

    public string? PromptSnippet { get; set; }

    public int PromptTokens { get; set; }

    public int CompletionTokens { get; set; }

    [Column(TypeName = "decimal(18, 4)")]
    public decimal Cost { get; set; }

    public DateTime Timestamp { get; set; }

    public string ModelName { get; set; } = null!;
}
