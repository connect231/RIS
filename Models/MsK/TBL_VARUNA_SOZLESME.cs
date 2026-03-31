using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace SOS.Models.MsK;

[Table("TBL_VARUNA_SOZLESME")]
public partial class TBL_VARUNA_SOZLESME
{
    [Key]
    public int LNGKOD { get; set; }

    public Guid? Id { get; set; }

    [Column(TypeName = "datetime")]
    public DateTime? StartDate { get; set; }

    [Column(TypeName = "datetime")]
    public DateTime? FinishDate { get; set; }

    [Column(TypeName = "datetime")]
    public DateTime? RenewalDate { get; set; }

    [StringLength(50)]
    public string? ContractNo { get; set; }

    [StringLength(50)]
    public string? ContractType { get; set; }

    [StringLength(100)]
    public string? ContractStatus { get; set; }

    public Guid? AccountId { get; set; }

    [StringLength(50)]
    public string? AccountCode { get; set; }

    [StringLength(250)]
    public string? AccountTitle { get; set; }

    public Guid? SalesRepresentativeId { get; set; }

    public Guid? CompanyId { get; set; }

    public Guid? ProductId { get; set; }

    public int? InvoiceNumber { get; set; }

    public Guid? InvoiceStatusId { get; set; }

    public int? InvoiceDueDate { get; set; }

    [Column(TypeName = "decimal(18, 8)")]
    public decimal? StampTaxRate { get; set; }

    [Column(TypeName = "decimal(18, 2)")]
    public decimal? StampTaxAmount { get; set; }

    public bool? IsLateInterestApply { get; set; }

    public int? LateInterestContractYear { get; set; }

    public bool? IsAutoExtending { get; set; }

    [StringLength(10)]
    public string? TotalAmountCurrency { get; set; }

    [Column(TypeName = "decimal(18, 2)")]
    public decimal? TotalAmount { get; set; }

    [StringLength(10)]
    public string? TotalAmountLocalCurrency { get; set; }

    [Column(TypeName = "decimal(18, 2)")]
    public decimal? TotalAmountLocal { get; set; }

    [StringLength(10)]
    public string? RemainingBalanceCurrency { get; set; }

    [Column(TypeName = "decimal(18, 2)")]
    public decimal? RemainingBalance { get; set; }

    [StringLength(500)]
    public string? ContractUrl { get; set; }

    public Guid? ApprovalCorrelationId { get; set; }

    [StringLength(150)]
    public string? ApprovalRequestedBy { get; set; }

    public DateTime? ApprovalRequestedOn { get; set; }

    [StringLength(150)]
    public string? ApprovalAssignTo { get; set; }

    public DateTime? ApprovalRespondedOn { get; set; }

    [StringLength(50)]
    public string? ApprovalState { get; set; }

    [StringLength(250)]
    public string? ApprovalResponse { get; set; }

    public DateTime? CreatedOn { get; set; }

    [StringLength(150)]
    public string? CreatedBy { get; set; }

    [StringLength(50)]
    public string? CreatedPlatform { get; set; }

    public DateTime? ModifiedOn { get; set; }

    [StringLength(150)]
    public string? ModifiedBy { get; set; }

    [StringLength(50)]
    public string? ModifiedPlatform { get; set; }

    public DateTime? DeletedOn { get; set; }

    [StringLength(150)]
    public string? DeletedBy { get; set; }

    [StringLength(250)]
    public string? Tags { get; set; }

    [StringLength(1024)]
    public string? ContractName { get; set; }

    /// <summary>
    /// Portal'daki VIEW_ORTAK_PROJE_ISIMLERI.LNGKOD ile eþleþen firma kodu.
    /// String tabanlý AccountTitle eþleþtirmesini ortadan kaldýrýr.
    /// DatabaseMigrationService tarafýndan kolon oluþturulur, admin tarafýndan doldurulur.
    /// </summary>
    public int? LNGORTAKFIRMAKOD { get; set; }
}

