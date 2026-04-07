using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SOS.Models.MsK;

[Table("TBLSOS_VARUNA_FIRSAT_ODATA")]
public class TBLSOS_VARUNA_FIRSAT_ODATA
{
    [Key]
    [StringLength(64)]
    public string Id { get; set; } = null!;

    [StringLength(512)]
    public string? Name { get; set; }

    [StringLength(100)]
    public string? OpportunityStageName { get; set; }

    [StringLength(100)]
    public string? DealType { get; set; }

    [StringLength(64)]
    public string? OwnerId { get; set; }

    [StringLength(64)]
    public string? AccountId { get; set; }

    [StringLength(64)]
    public string? CompanyId { get; set; }

    [Column(TypeName = "datetime")]
    public DateTime? CloseDate { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal? Probability { get; set; }

    [StringLength(10)]
    public string? AmountCurrency { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal? AmountValue { get; set; }

    public bool? AmountHasValue { get; set; }

    [StringLength(100)]
    public string? Source { get; set; }

    public DateTime SyncTarihi { get; set; }
}
