using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace UniCP.Models.MsK;

public partial class InventoryAccountProduct
{
    [Key]
    public int LNGKOD { get; set; }

    public Guid? Id { get; set; }

    public DateOnly? StartDate { get; set; }

    public DateOnly? FinishDate { get; set; }

    public DateOnly? InvInstalledDate { get; set; }

    public DateOnly? InvPurchaseDate { get; set; }

    [StringLength(50)]
    public string? Status { get; set; }

    [StringLength(50)]
    public string? StatusCrm { get; set; }

    [StringLength(50)]
    public string? FinancialProductType { get; set; }

    [StringLength(50)]
    public string? PFApplicaitonType { get; set; }

    public Guid? AccountId { get; set; }

    public Guid? StockId { get; set; }

    public Guid? ContactId { get; set; }

    public Guid? ContractId { get; set; }

    public Guid? CompanyId { get; set; }

    public Guid? ProductGroupId { get; set; }

    public Guid? ProductSubGroupId { get; set; }

    public Guid? QuoteId { get; set; }

    public Guid? CrmOrderID { get; set; }

    public int? Amount { get; set; }

    [StringLength(150)]
    public string? Domain { get; set; }

    [StringLength(100)]
    public string? PosAccountNo { get; set; }

    [StringLength(100)]
    public string? CardNo { get; set; }

    public Guid? InvSerialId { get; set; }

    public bool? InvOutOfWarehouseSerial { get; set; }

    [StringLength(100)]
    public string? InvOutOfWarehouseSerialCode { get; set; }

    public bool? IsSentToTsm { get; set; }

    [StringLength(10)]
    public string? PriceCurrency { get; set; }

    [Column(TypeName = "decimal(18, 4)")]
    public decimal? PriceAmount { get; set; }

    [StringLength(10)]
    public string? VatCurrency { get; set; }

    [Column(TypeName = "decimal(18, 4)")]
    public decimal? VatAmount { get; set; }

    [StringLength(10)]
    public string? TotalListPriceCurrency { get; set; }

    [Column(TypeName = "decimal(18, 4)")]
    public decimal? TotalListPriceAmount { get; set; }

    [StringLength(10)]
    public string? TotalPackagePriceCurrency { get; set; }

    [Column(TypeName = "decimal(18, 4)")]
    public decimal? TotalPackagePriceAmount { get; set; }

    [StringLength(10)]
    public string? TotalPackageVatCurrency { get; set; }

    [Column(TypeName = "decimal(18, 4)")]
    public decimal? TotalPackageVatAmount { get; set; }

    [StringLength(10)]
    public string? InstallCountryCode { get; set; }

    [StringLength(100)]
    public string? InstallState { get; set; }

    [StringLength(100)]
    public string? InstallProvince { get; set; }

    [StringLength(100)]
    public string? InstallDistrict { get; set; }

    [StringLength(150)]
    public string? InstallPlaceName { get; set; }

    [StringLength(100)]
    public string? InstallSubdivision1 { get; set; }

    [StringLength(100)]
    public string? InstallSubdivision2 { get; set; }

    [StringLength(100)]
    public string? InstallSubdivision3 { get; set; }

    [StringLength(100)]
    public string? InstallSubdivision4 { get; set; }

    [StringLength(20)]
    public string? InstallPostalCode { get; set; }

    [StringLength(500)]
    public string? InstallAddress { get; set; }

    [StringLength(100)]
    public string? InstallCity { get; set; }

    public double? InstallLongitude { get; set; }

    public double? InstallLatitude { get; set; }

    [StringLength(50)]
    public string? StockCode { get; set; }

    [StringLength(250)]
    public string? StockName { get; set; }

    [StringLength(50)]
    public string? TPOutReferenceCode { get; set; }

    [StringLength(50)]
    public string? SAPOutReferenceCode { get; set; }

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
}
