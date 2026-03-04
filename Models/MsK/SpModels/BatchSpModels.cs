using System.ComponentModel.DataAnnotations.Schema;

namespace UniCP.Models.MsK.SpModels
{
    // Fix: Removed inheritance to avoid EF Core Keyless Entity Hierarchy issues (TPH).
    // All properties are copied from base classes.
    // Added ToBase() methods to facilitate mapping back to original types for Controller consumption.

    public class SSP_VARUNA_SIPARIS_COKLU
    {
        public int LNGORTAKPROJEKOD { get; set; } 
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
        public int? Gecikme_Gun { get; set; }
        public int? Satıs_Vadesi { get; set; }
        public DateTime? Tahsil_Tarihi { get; set; }
        public int? Bekleme_Gun { get; set; }
        public decimal? Bekleyen_Bakiye { get; set; }
        public decimal? Fatura_toplam { get; set; }

        public SpVarunaSiparisResult ToBase()
        {
            return new SpVarunaSiparisResult
            {
                LNGKOD = this.LNGORTAKPROJEKOD,
                CreateOrderDate = this.CreateOrderDate,
                OrderId = this.OrderId,
                InvoiceDate = this.InvoiceDate,
                PaymentType = this.PaymentType,
                PaymentTypeTime = this.PaymentTypeTime,
                OrderStatus = this.OrderStatus,
                QuoteId = this.QuoteId,
                AccountId = this.AccountId,
                ProposalOwnerId = this.ProposalOwnerId,
                SubTotalDiscount = this.SubTotalDiscount,
                CompanyId = this.CompanyId,
                IsEligibleForNetsisIntegration = this.IsEligibleForNetsisIntegration,
                SAPOutReferenceCode = this.SAPOutReferenceCode,
                DistributionChannelSapId = this.DistributionChannelSapId,
                SalesDocumentTypeSapId = this.SalesDocumentTypeSapId,
                SalesOrganizationSapId = this.SalesOrganizationSapId,
                CrmSalesOfficeSapId = this.CrmSalesOfficeSapId,
                SalesGroupSapId = this.SalesGroupSapId,
                IsEligibleForSapIntegration = this.IsEligibleForSapIntegration,
                CrmOrderNotes = this.CrmOrderNotes,
                SerialNumber = this.SerialNumber,
                CreatedOn = this.CreatedOn,
                CreatedBy = this.CreatedBy,
                ModifiedOn = this.ModifiedOn,
                ModifiedBy = this.ModifiedBy,
                TotalNetAmount = this.TotalNetAmount,
                TotalAmountWithTax = this.TotalAmountWithTax,
                TotalProfitAmount = this.TotalProfitAmount,
                AccountTitle = this.AccountTitle,
                Durum = this.Durum,
                Gecikme_Gun = this.Gecikme_Gun,
                Satıs_Vadesi = this.Satıs_Vadesi,
                Tahsil_Tarihi = this.Tahsil_Tarihi,
                Bekleme_Gun = this.Bekleme_Gun,
                Bekleyen_Bakiye = this.Bekleyen_Bakiye,
                Fatura_toplam = this.Fatura_toplam
            };
        }
    }

    public class SSP_VARUNA_CHART_DATA_COKLU
    {
        public decimal TOPLAMTUTAR { get; set; }
        public string? URUNADI { get; set; }
        public string? STOKKOD { get; set; }
        public string? GRUP { get; set; }
        public decimal? KDV { get; set; }
        public DateTime? TARIH { get; set; }
        public int LNGKOD { get; set; }

        public SSP_VARUNA_CHART_DATA ToBase()
        {
            return new SSP_VARUNA_CHART_DATA
            {
                TOPLAMTUTAR = this.TOPLAMTUTAR,
                URUNADI = this.URUNADI,
                STOKKOD = this.STOKKOD,
                GRUP = this.GRUP,
                KDV = this.KDV,
                TARIH = this.TARIH
            };
        }
    }

    public class SSP_TFS_GELISTIRME_COKLU
    {
        public int? TFSNO { get; set; } // Made Nullable
        public string? MADDEBASLIK { get; set; }
        public string? MADDEDURUM { get; set; }
        public DateTime? ACILMATARIHI { get; set; }
        public DateTime? DEGISTIRMETARIHI { get; set; }
        public string? ACANKULLANICI { get; set; }
        public string? PROJE { get; set; }
        public string? COST { get; set; }
        public string? SATISDURUMU { get; set; }
        public string? URUN { get; set; }
        public string? MOBIL { get; set; }
        public decimal? YAZILIM_TOPLAMAG { get; set; }
        public decimal? TAMAMLANMA_OARANI { get; set; }
        public string? MUSTERI_SORUMLUSU { get; set; }
        public string? SATIS_SORUMLUSU { get; set; }
        public DateTime? PLANLANAN_PYUAT { get; set; }
        public DateTime? GERCEKLESEN_PYUAT { get; set; }
        public DateTime? PLANLAN_CANLITESLIM { get; set; }
        public DateTime? GERCEKLESEN_CANLITESLIM { get; set; }
        public string? YARATICI { get; set; }
        public int? LNGORTAKPROJEKOD { get; set; } // Made Nullable



        public SSP_TFS_GELISTIRME ToBase()
        {
            return new SSP_TFS_GELISTIRME
            {
                TFSNO = this.TFSNO ?? 0, // Handle Null
                MADDEBASLIK = this.MADDEBASLIK,
                MADDEDURUM = this.MADDEDURUM,
                ACILMATARIHI = this.ACILMATARIHI,
                DEGISTIRMETARIHI = this.DEGISTIRMETARIHI,
                ACANKULLANICI = this.ACANKULLANICI,
                PROJE = this.PROJE,
                COST = this.COST,
                SATISDURUMU = this.SATISDURUMU,
                URUN = this.URUN,
                MOBIL = this.MOBIL,
                YAZILIM_TOPLAMAG = this.YAZILIM_TOPLAMAG,
                TAMAMLANMA_OARANI = this.TAMAMLANMA_OARANI,
                MUSTERI_SORUMLUSU = this.MUSTERI_SORUMLUSU,
                SATIS_SORUMLUSU = this.SATIS_SORUMLUSU,
                PLANLANAN_PYUAT = this.PLANLANAN_PYUAT,
                GERCEKLESEN_PYUAT = this.GERCEKLESEN_PYUAT,
                PLANLAN_CANLITESLIM = this.PLANLAN_CANLITESLIM,
                GERCEKLESEN_CANLITESLIM = this.GERCEKLESEN_CANLITESLIM,
                YARATICI = this.YARATICI
            };
        }
    }

    public class SSP_N4B_TICKETLARI_COKLU
    {
        public int Bildirim_No { get; set; }
        public string? Bildirim_Tipi { get; set; } 
        public string? Bildirim_Durumu { get; set; }
        public string? Gelis_Kanali { get; set; }
        public string? Bildirim_Aciklamasi { get; set; }
        public DateTime Bildirim_Tarihi { get; set; }
        public string? Musteri_Tipi1 { get; set; }
        public string? Firma { get; set; }
        public DateTime Sonislemtarih { get; set; }
        public int Sla { get; set; }
        public decimal? SLA_YD_Cozum_Sure { get; set; }
        public decimal? SLA_YD_Cozum_Kalan_Sure { get; set; }
        public string? Bildirim_Bekletme_Neden { get; set; }
        public int LNGORTAKPROJEKOD { get; set; }

        public SSP_N4B_TICKETLARI ToBase()
        {
            return new SSP_N4B_TICKETLARI
            {
                Bildirim_No = this.Bildirim_No,
                Bildirim_Tipi = this.Bildirim_Tipi,
                Bildirim_Durumu = this.Bildirim_Durumu,
                Gelis_Kanali = this.Gelis_Kanali,
                Bildirim_Aciklamasi = this.Bildirim_Aciklamasi,
                Bildirim_Tarihi = this.Bildirim_Tarihi,
                Musteri_Tipi1 = this.Musteri_Tipi1,
                Firma = this.Firma,
                Sonislemtarih = this.Sonislemtarih,
                Sla = this.Sla,
                SLA_YD_Cozum_Sure = this.SLA_YD_Cozum_Sure,
                SLA_YD_Cozum_Kalan_Sure = this.SLA_YD_Cozum_Kalan_Sure,
                Bildirim_Bekletme_Neden = this.Bildirim_Bekletme_Neden
            };
        }
    }

    public class SSP_N4B_TICKET_DURUM_SAYILARI_COKLU
    {
        public string Durum { get; set; }
        public int Sayi { get; set; }
        public int LNGORTAKPROJEKOD { get; set; }

        public SSP_N4B_TICKET_DURUM_SAYILARI ToBase()
        {
            return new SSP_N4B_TICKET_DURUM_SAYILARI
            {
                Durum = this.Durum,
                Sayi = this.Sayi
            };
        }
    }

    public class SSP_N4B_SLA_ORAN_COKLU
    {
        public int YIL { get; set; }
        public int AY { get; set; }
        public string DONEM { get; set; }
        public decimal UYAN { get; set; }
        public decimal UYMAYAN { get; set; }
        public decimal ORAN { get; set; }
        public int LNGORTAKPROJEKOD { get; set; }

        public SSP_N4B_SLA_ORAN ToBase()
        {
            return new SSP_N4B_SLA_ORAN
            {
                YIL = this.YIL,
                AY = this.AY,
                DONEM = this.DONEM,
                UYAN = this.UYAN,
                UYMAYAN = this.UYMAYAN,
                ORAN = this.ORAN
            };
        }
    }
    
    public class SSP_VARUNA_SIPARIS_DETAY_COKLU
    {
        public int LNGORTAKPROJEKOD { get; set; }
        public DateTime? DeliveryTime { get; set; }
        public DateTime? TransactionDate { get; set; }
        public string? PRODUCTSID { get; set; }
        public string? CrmOrderId { get; set; }
        public string? StockId { get; set; }
        public decimal? Quantity { get; set; }
        public decimal? LineDiscountRate { get; set; }
        public string? StockUnitType { get; set; }
        public decimal? Tax { get; set; }
        public string? PYPSapId { get; set; }
        public DateTime? CreatedOn { get; set; }
        public DateTime? ModifiedOn { get; set; }
        public decimal? UnitPrice { get; set; }
        public decimal? Total { get; set; }
        public decimal? NetLineTotalWithTax { get; set; }
        public string? ProductName { get; set; }
        public string? ItemNo { get; set; }
        public string OrderId { get; set; }

        // Note: Used in Dictionary<string, List<SSP_VARUNA_SIPARIS_DETAY>>, so we need to map to SSP_VARUNA_SIPARIS_DETAY
        public SSP_VARUNA_SIPARIS_DETAY ToBase()
        {
            return new SSP_VARUNA_SIPARIS_DETAY
            {
                LNGKOD = this.LNGORTAKPROJEKOD,
                DeliveryTime = this.DeliveryTime,
                TransactionDate = this.TransactionDate,
                PRODUCTSID = this.PRODUCTSID,
                CrmOrderId = this.CrmOrderId,
                StockId = this.StockId,
                Quantity = this.Quantity,
                LineDiscountRate = this.LineDiscountRate,
                StockUnitType = this.StockUnitType,
                Tax = this.Tax,
                PYPSapId = this.PYPSapId,
                CreatedOn = this.CreatedOn,
                ModifiedOn = this.ModifiedOn,
                UnitPrice = this.UnitPrice,
                Total = this.Total,
                NetLineTotalWithTax = this.NetLineTotalWithTax,
                ProductName = this.ProductName,
                ItemNo = this.ItemNo
            };
        }
    }
}
