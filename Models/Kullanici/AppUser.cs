using Microsoft.AspNetCore.Identity;

namespace UniCP.Models.Kullanici;

public class AppUser : IdentityUser<int>
{
    public string AdSoyad { get; set; } = null!;
    [System.ComponentModel.DataAnnotations.Schema.Column(TypeName = "decimal(18,2)")]
    public decimal TokenBalance { get; set; } = 0;
}