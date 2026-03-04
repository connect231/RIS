using System.ComponentModel.DataAnnotations;

namespace UniCP.Models.Kullanici.Role;

public class RoleEditModel
{
    public int Id { get; set; }

    [Required]
    [StringLength(30)]
    [Display(Name = "Role Adı")]
    public string RoleAdi { get; set; } = null!;
}