using System;

namespace UniCP.Models.MsK.SpModels
{
    public class SSP_TFS_GELISTIRME
    {
        public int TFSNO { get; set; }
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
    }
}
