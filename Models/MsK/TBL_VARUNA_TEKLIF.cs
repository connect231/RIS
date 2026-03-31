using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace SOS.Models.MsK;

[Table("TBL_VARUNA_TEKLIF")]
public partial class TBL_VARUNA_TEKLIF
{
    [Key]
    public Guid Id { get; set; }

    [Column(TypeName = "datetime")]
    public DateTime? DeliveryDate { get; set; }

    [Column(TypeName = "datetime")]
    public DateTime? ExpirationDate { get; set; }

    [Column(TypeName = "datetime")]
    public DateTime? FirstCreatedDate { get; set; }

    [Column(TypeName = "datetime")]
    public DateTime? FirstReleaseDate { get; set; }

    [Column(TypeName = "datetime")]
    public DateTime? RevisedDate { get; set; }

    [Column(TypeName = "datetime")]
    public DateTime? ServiceFinishDate { get; set; }

    [Column(TypeName = "datetime")]
    public DateTime? ServiceStartDate { get; set; }

    [StringLength(50)]
    public string? PaymentType { get; set; }

    [StringLength(50)]
    public string? PaymentTypeTime { get; set; }

    [StringLength(50)]
    public string? Status { get; set; }

    [StringLength(50)]
    public string? DeliveryType { get; set; }

    [StringLength(50)]
    public string? DeliveryTypeTime { get; set; }

    [StringLength(50)]
    public string? RevStatus { get; set; }

    [Column(TypeName = "decimal(18, 4)")]
    public decimal? SubTotalAlternativeCurrency { get; set; }

    [StringLength(50)]
    public string? SubTotalDiscountType { get; set; }

    [StringLength(50)]
    public string? QuoteApprovalProcessStatus { get; set; }

    [StringLength(50)]
    public string? QuoteType { get; set; }

    public Guid? OpportunityId { get; set; }

    public Guid? RevisionId { get; set; }

    [StringLength(50)]
    public string? Number { get; set; }

    [StringLength(255)]
    public string? Name { get; set; }

    [Column(TypeName = "decimal(18, 4)")]
    public decimal? SubTotalDiscount { get; set; }

    public Guid? WarehouseId { get; set; }

    public Guid? ProposalOwnerId { get; set; }

    [StringLength(100)]
    public string? AddressIdentifier { get; set; }

    [StringLength(50)]
    public string? DeliveryTime { get; set; }

    [StringLength(512)]
    public string? CustomerOrderNumber { get; set; }

    [StringLength(50)]
    public string? PaymentTime { get; set; }

    public Guid? PersonId { get; set; }

    public Guid? SpecialCodeId { get; set; }

    public Guid? AccountId { get; set; }

    public string? Description { get; set; }

    public int? RevNo { get; set; }

    [Column(TypeName = "decimal(18, 6)")]
    public decimal? AlternativeCurrencyRate { get; set; }

    public bool? IsVATExempt { get; set; }

    public string? TermsAndConditions { get; set; }

    public string? ProductsAndServices { get; set; }

    [StringLength(100)]
    public string? ReferenceCode { get; set; }

    public bool? TransferWithForeignCurrency { get; set; }

    public Guid? ContactId { get; set; }

    [StringLength(255)]
    public string? FirstCreatedByName { get; set; }

    public Guid? TeamId { get; set; }

    public Guid? TeamCreatedById { get; set; }

    [Column(TypeName = "decimal(18, 4)")]
    public decimal? SubTotalDiscountAmount { get; set; }

    [StringLength(100)]
    public string? InRefCode { get; set; }

    public Guid? CompanyId { get; set; }

    [Column(TypeName = "decimal(18, 4)")]
    public decimal? TotalDiscountRate { get; set; }

    [StringLength(50)]
    [Unicode(false)]
    public string? CRMRevNo { get; set; }

    [StringLength(100)]
    public string? PublicationSource { get; set; }

    public string? TermsAndConditions2 { get; set; }

    public string? ProductsAndServices2 { get; set; }

    public Guid? StockId { get; set; }

    [StringLength(512)]
    public string? ItemNo { get; set; }

    public Guid? CrmOrderId { get; set; }

    public bool? OrderWillBeCreate { get; set; }

    public bool? OrderOwnerWillBeChanged { get; set; }

    [StringLength(100)]
    public string? TPOutReferenceCode { get; set; }

    public DateTime? CreatedOn { get; set; }

    [StringLength(255)]
    public string? CreatedBy { get; set; }

    public DateTime? ModifiedOn { get; set; }

    [StringLength(255)]
    public string? ModifiedBy { get; set; }

    public DateTime? DeletedOn { get; set; }

    [StringLength(255)]
    public string? DeletedBy { get; set; }

    public string? Tags { get; set; }

    public Guid? ApprovalCorrelationId { get; set; }

    public Guid? ApprovalRequestedBy { get; set; }

    public DateTime? ApprovalRequestedOn { get; set; }

    public Guid? ApprovalAssignTo { get; set; }

    public DateTime? ApprovalRespondedOn { get; set; }

    [StringLength(50)]
    public string? ApprovalState { get; set; }

    [StringLength(255)]
    public string? ApprovalResponse { get; set; }

    [StringLength(50)]
    public string? CreatedPlatform { get; set; }

    [StringLength(50)]
    public string? ModifiedPlatform { get; set; }

    [StringLength(10)]
    public string? NetSubTotalLocalCurrency_Currency { get; set; }

    [Column(TypeName = "decimal(18, 8)")]
    public decimal? NetSubTotalLocalCurrency_Amount { get; set; }

    public bool? NetSubTotalLocalCurrency_HasValue { get; set; }

    [StringLength(10)]
    public string? TotalNetAmountLocalCurrency_Currency { get; set; }

    [Column(TypeName = "decimal(18, 8)")]
    public decimal? TotalNetAmountLocalCurrency_Amount { get; set; }

    [StringLength(10)]
    public string? TotalAmountWithTaxLocalCurrency_Currency { get; set; }

    [Column(TypeName = "decimal(18, 8)")]
    public decimal? TotalAmountWithTaxLocalCurrency_Amount { get; set; }

    [StringLength(10)]
    public string? TotalProfitAmount_Currency { get; set; }

    [Column(TypeName = "decimal(18, 8)")]
    public decimal? TotalProfitAmount_Amount { get; set; }

    [StringLength(10)]
    public string? NetSubTotalAlternativeCurrency_Currency { get; set; }

    [Column(TypeName = "decimal(18, 8)")]
    public decimal? NetSubTotalAlternativeCurrency_Amount { get; set; }

    [StringLength(10)]
    public string? TotalNetAmountAlternativeCurrency_Currency { get; set; }

    [Column(TypeName = "decimal(18, 8)")]
    public decimal? TotalNetAmountAlternativeCurrency_Amount { get; set; }

    [StringLength(10)]
    public string? TotalAmountWithTaxAlternativeCurrency_Currency { get; set; }

    [Column(TypeName = "decimal(18, 8)")]
    public decimal? TotalAmountWithTaxAlternativeCurrency_Amount { get; set; }

    [StringLength(10)]
    public string? TotalProfitAmountAlternativeCurrency_Currency { get; set; }

    [Column(TypeName = "decimal(18, 8)")]
    public decimal? TotalProfitAmountAlternativeCurrency_Amount { get; set; }

    [StringLength(255)]
    public string? Account_Title { get; set; }

    [StringLength(100)]
    public string? Account_SAPOutReferenceCode { get; set; }
}

