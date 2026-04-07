using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SOS.Models.MsK;

[Table("TBLSOS_CRM_KULLANICI_GECICI")]
public class TBLSOS_CRM_KULLANICI_GECICI
{
    [Key]
    public int Id { get; set; }

    [StringLength(64)]
    public string? CrmUserId { get; set; }

    [Required]
    [StringLength(256)]
    public string Email { get; set; } = null!;

    [Required]
    [StringLength(256)]
    public string AdSoyad { get; set; } = null!;

    public bool Aktif { get; set; } = true;
}
