using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.Identity.Client;

namespace UniCP.Models.MsK.SpModels
{
    public class SpVarunaSiparisResult
    {
        public int LNGKOD { get; set; }
        public DateTime? CreateOrderDate { get; set; }
        public string? OrderId { get; set; }
        public DateTime? InvoiceDate { get; set; }
        public string? PaymentType { get; set; }
        public string? PaymentTypeTime { get; set; }
        public string? OrderStatus { get; set; }
        public string? QuoteId { get; set; }
        public string? AccountId { get; set; }
        public string? ProposalOwnerId { get; set; }
        public string? SubTotalDiscount { get; set; }
        public string? CompanyId { get; set; }
        public bool IsEligibleForNetsisIntegration { get; set; }

        public string? SAPOutReferenceCode { get; set; }
        public string? DistributionChannelSapId { get; set; }
        public string? SalesDocumentTypeSapId { get; set; }
        public string? SalesOrganizationSapId { get; set; }

        public string? CrmSalesOfficeSapId { get; set; }
        public string? SalesGroupSapId { get; set; }

        public bool IsEligibleForSapIntegration { get; set; }
        public string? CrmOrderNotes { get; set; }
        public string? SerialNumber { get; set; }
        public DateTime? CreatedOn { get; set; }
        public string? CreatedBy { get; set; }
        public DateTime? ModifiedOn { get; set; }

        public string? ModifiedBy { get; set; }
        public decimal? TotalNetAmount { get; set; }
        public decimal? TotalAmountWithTax { get; set; }
        public decimal? TotalProfitAmount { get; set; }
        public string? AccountTitle { get; set; }
        public string? Durum { get; set; }
        
        // New columns that were causing issues when mapped to a Table
        public int? Gecikme_Gun { get; set; }
        public int? Satıs_Vadesi { get; set; }
        public DateTime? Tahsil_Tarihi { get; set; }
        public int? Bekleme_Gun { get; set; }

        public decimal? Bekleyen_Bakiye { get; set; }
        public decimal? Fatura_toplam { get; set; }
    }
}
