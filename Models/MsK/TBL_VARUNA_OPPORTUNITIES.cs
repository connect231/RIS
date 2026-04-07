using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SOS.Models.MsK;

[Table("TBL_VARUNA_OPPORTUNITIES")]
public class TBL_VARUNA_OPPORTUNITIES
{
    [Key]
    [StringLength(64)]
    public string Id { get; set; } = null!;

    [StringLength(512)]
    public string? Name { get; set; }

    [StringLength(100)]
    public string? Type { get; set; }

    [StringLength(100)]
    public string? OpportunityStageName { get; set; }

    [StringLength(100)]
    public string? OpportunityStageNameTr { get; set; }

    [StringLength(64)]
    public string? OpportunityStageId { get; set; }

    [StringLength(100)]
    public string? Source { get; set; }

    [StringLength(100)]
    public string? DealType { get; set; }

    [StringLength(100)]
    public string? DealTypeTR { get; set; }

    [StringLength(100)]
    public string? WageStatus { get; set; }

    [StringLength(50)]
    public string? IsThereDelay { get; set; }

    [StringLength(100)]
    public string? LeadSource { get; set; }

    [StringLength(100)]
    public string? LeadSourceTR { get; set; }

    [StringLength(255)]
    public string? ClosedLostReason { get; set; }

    [StringLength(255)]
    public string? ClosedLostReasonTr { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal? Probability { get; set; }

    [StringLength(100)]
    public string? ProbabilityBand { get; set; }

    [StringLength(64)]
    public string? AccountId { get; set; }

    [StringLength(64)]
    public string? OwnerId { get; set; }

    [StringLength(64)]
    public string? PartnerId { get; set; }

    [StringLength(64)]
    public string? PersonId { get; set; }

    [StringLength(64)]
    public string? CompanyId { get; set; }

    [StringLength(64)]
    public string? PipelineId { get; set; }

    [StringLength(64)]
    public string? ContactId { get; set; }

    [StringLength(64)]
    public string? CustomerRepresentativeId { get; set; }

    [StringLength(64)]
    public string? TeamId { get; set; }

    [StringLength(64)]
    public string? ProductGroupId { get; set; }

    [Column(TypeName = "datetime")]
    public DateTime? ApplyDate { get; set; }

    [Column(TypeName = "datetime")]
    public DateTime? CloseDate { get; set; }

    [Column(TypeName = "datetime")]
    public DateTime? DeliveryDate { get; set; }

    [Column(TypeName = "datetime")]
    public DateTime? FirstCreatedDate { get; set; }

    [Column(TypeName = "datetime")]
    public DateTime? CreatedOn { get; set; }

    [Column(TypeName = "datetime")]
    public DateTime? ModifiedOn { get; set; }

    [Column(TypeName = "datetime")]
    public DateTime? DeletedOn { get; set; }

    [StringLength(255)]
    public string? CreatedBy { get; set; }

    [StringLength(255)]
    public string? ModifiedBy { get; set; }

    [StringLength(255)]
    public string? DeletedBy { get; set; }

    [StringLength(512)]
    public string? Desription { get; set; }

    [StringLength(100)]
    public string? DealStatus { get; set; }

    [StringLength(100)]
    public string? WonLostType { get; set; }

    [StringLength(100)]
    public string? WonLostTypeTr { get; set; }

    [StringLength(10)]
    public string? AmountCurrency { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal? AmountAmount { get; set; }

    [StringLength(500)]
    public string? AccountTitle { get; set; }

    [StringLength(512)]
    public string? Tags { get; set; }
}
