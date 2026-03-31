using SOS.Models.MsK;

namespace SOS.Models.ViewModels
{
    public class CockpitViewModel
    {
        // Faturalar (İade + NULL/Boş)
        public decimal FaturalarToplam { get; set; }
        public int FaturalarAdet { get; set; }

        // Tahsilatlar (Tahsil Edildi)
        public decimal TahsilatlarToplam { get; set; }
        public int TahsilatlarAdet { get; set; }

        // Sözleşmeler (Archived)
        public decimal SozlesmelerToplam { get; set; }
        public int SozlesmelerAdet { get; set; }

        // Trend verileri (önceki dönem karşılaştırma)
        public decimal FaturalarTrend { get; set; }
        public decimal TahsilatlarTrend { get; set; }
        public decimal SozlesmelerTrend { get; set; }

        // Hedef (dönem bazlı dinamik)
        public decimal AylikHedef { get; set; }
        public decimal HedefGerceklesme { get; set; }
        public decimal HedefKalan { get; set; }
        public decimal HedefYuzde { get; set; }
        public int HedefAySayisi { get; set; }

        // Filtre bilgileri
        public string AktifFiltre { get; set; } = "ytd";
        public DateTime FiltreBaslangic { get; set; }
        public DateTime FiltreBitis { get; set; }

        // Detay listeleri
        public List<VIEW_CP_EXCEL_FATURA> FaturaDetaylari { get; set; } = new();
        public List<VIEW_CP_EXCEL_FATURA> TahsilatDetaylari { get; set; } = new();
        public List<TBL_VARUNA_SOZLESME> SozlesmeDetaylari { get; set; } = new();
    }
}
