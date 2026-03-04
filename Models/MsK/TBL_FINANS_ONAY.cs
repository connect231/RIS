using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace UniCP.Models.MsK;

[Table("TBL_FINANS_ONAY")]
[Index("CreatedBy", Name = "IX_TBL_FINANS_ONAY_CreatedBy")]
[Index("RevokedBy", Name = "IX_TBL_FINANS_ONAY_RevokedBy")]
public partial class TBL_FINANS_ONAY
{
    [Key]
    public int Id { get; set; }

    [StringLength(50)]
    public string OrderId { get; set; } = null!;

    [StringLength(100)]
    public string? PONumber { get; set; }

    public DateTime CreatedDate { get; set; }

    public int? CreatedBy { get; set; }

    public bool IsRevoked { get; set; }

    public int? RevokedBy { get; set; }

    public DateTime? RevokedDate { get; set; }

    [ForeignKey("CreatedBy")]
    [InverseProperty("TBL_FINANS_ONAYCreatedByNavigations")]
    public virtual TBL_KULLANICI? CreatedByNavigation { get; set; }

    [ForeignKey("RevokedBy")]
    [InverseProperty("TBL_FINANS_ONAYRevokedByNavigations")]
    public virtual TBL_KULLANICI? RevokedByNavigation { get; set; }
}
