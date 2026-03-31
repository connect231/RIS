using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace SOS.Models.MsK;

[Table("TBL_VARUNA_TEKLIF_URUNLERI")]
public partial class TBL_VARUNA_TEKLIF_URUNLERI
{
    [Key]
    public Guid Id { get; set; }

    [Column(TypeName = "datetime")]
    public DateTime? DeliveryTime { get; set; }

    [Column(TypeName = "datetime")]
    public DateTime? TransactionDate { get; set; }

    public Guid? QuoteId { get; set; }

    public Guid? StockId { get; set; }

    [StringLength(50)]
    public string? LineDiscountType { get; set; }

    [Column(TypeName = "decimal(18, 4)")]
    public decimal? Quantity { get; set; }

    [Column(TypeName = "decimal(18, 4)")]
    public decimal? LineDiscountRate { get; set; }

    public string? Description { get; set; }

    public Guid? StockUnitTypeIdentifier { get; set; }

    [StringLength(50)]
    public string? StockUnitType { get; set; }

    [Column(TypeName = "decimal(18, 4)")]
    public decimal? Tax { get; set; }

    [Column(TypeName = "decimal(18, 4)")]
    public decimal? ProfitRate { get; set; }

    [Column(TypeName = "decimal(18, 6)")]
    public decimal? CurrencyRate { get; set; }

    [Column(TypeName = "decimal(18, 4)")]
    public decimal? ComissionRate { get; set; }

    [StringLength(50)]
    public string? ItemNo { get; set; }

    [StringLength(100)]
    public string? PYPSapId { get; set; }

    [StringLength(100)]
    public string? StorageLocationSapId { get; set; }

    [StringLength(100)]
    public string? ProductionLocationSapId { get; set; }

    [Column(TypeName = "decimal(18, 4)")]
    public decimal? UnorderedQuantity { get; set; }

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
    public string? LineDiscountAmount_Currency { get; set; }

    [Column(TypeName = "decimal(18, 8)")]
    public decimal? LineDiscountAmount_Amount { get; set; }

    [StringLength(10)]
    public string? UnitPrice_Currency { get; set; }

    [Column(TypeName = "decimal(18, 8)")]
    public decimal? UnitPrice_Amount { get; set; }

    [StringLength(10)]
    public string? PurchasingPrice_Currency { get; set; }

    [Column(TypeName = "decimal(18, 8)")]
    public decimal? PurchasingPrice_Amount { get; set; }

    [StringLength(10)]
    public string? Total_Currency { get; set; }

    [Column(TypeName = "decimal(18, 8)")]
    public decimal? Total_Amount { get; set; }

    [StringLength(10)]
    public string? NetLineSubTotal_Currency { get; set; }

    [Column(TypeName = "decimal(18, 8)")]
    public decimal? NetLineSubTotal_Amount { get; set; }

    [StringLength(10)]
    public string? TotalProfitAmountLocal_Currency { get; set; }

    [Column(TypeName = "decimal(18, 8)")]
    public decimal? TotalProfitAmountLocal_Amount { get; set; }

    [StringLength(10)]
    public string? UnitProfitAmountLocal_Currency { get; set; }

    [Column(TypeName = "decimal(18, 8)")]
    public decimal? UnitProfitAmountLocal_Amount { get; set; }

    [StringLength(10)]
    public string? NetLineTotalAmount_Currency { get; set; }

    [Column(TypeName = "decimal(18, 8)")]
    public decimal? NetLineTotalAmount_Amount { get; set; }

    [StringLength(10)]
    public string? NetLineTotalWithTax_Currency { get; set; }

    [Column(TypeName = "decimal(18, 8)")]
    public decimal? NetLineTotalWithTax_Amount { get; set; }

    [StringLength(10)]
    public string? NetLineTotalWithTaxLocal_Currency { get; set; }

    [Column(TypeName = "decimal(18, 8)")]
    public decimal? NetLineTotalWithTaxLocal_Amount { get; set; }

    [StringLength(10)]
    public string? NetLineSubTotalLocal_Currency { get; set; }

    [Column(TypeName = "decimal(18, 8)")]
    public decimal? NetLineSubTotalLocal_Amount { get; set; }

    [StringLength(10)]
    public string? NetLineTotalAmountLocal_Currency { get; set; }

    [Column(TypeName = "decimal(18, 8)")]
    public decimal? NetLineTotalAmountLocal_Amount { get; set; }

    [StringLength(10)]
    public string? ProfitAfterSubtotalDiscountLocal_Currency { get; set; }

    [Column(TypeName = "decimal(18, 8)")]
    public decimal? ProfitAfterSubtotalDiscountLocal_Amount { get; set; }

    [StringLength(512)]
    public string? StockType { get; set; }

    [StringLength(256)]
    public string? StockCode { get; set; }

    [StringLength(512)]
    public string? StockName { get; set; }

    [Column(TypeName = "decimal(18, 8)")]
    public decimal? StockSalesVatValue { get; set; }
}

