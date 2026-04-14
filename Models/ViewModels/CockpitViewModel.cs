
using SOS.Models.MsK;

namespace SOS.Models.ViewModels
{
    public class CockpitViewModel
    {
        // Faturalar (İade + NULL/Boş)
        public decimal FaturalarToplam { get; set; }
        public int FaturalarAdet { get; set; }
        public decimal VarunaDisiToplam { get; set; }
        public int VarunaDisiAdet { get; set; }

        // Tahsilatlar (Tahsil Edildi)
        public decimal TahsilatlarToplam { get; set; }
        public int TahsilatlarAdet { get; set; }

        // Sözleşmeler
        public decimal SozlesmelerToplam { get; set; }  // PAYDA: tüm yeni sözleşme tutarı
        public int SozlesmelerAdet { get; set; }
        public decimal SozFaturalandiToplam { get; set; } // PAY: faturalanmış sözleşme tutarı
        public int SozFaturalandiAdet { get; set; }
        public decimal SozArchivedToplam { get; set; }  // İmzalanan (Archived) tutar
        public int SozArchivedAdet { get; set; }
        public decimal SozGecikmisToplam { get; set; }
        public int SozGecikmiAdet { get; set; }
        public decimal SozKismiFatToplam { get; set; }
        public int SozKismiFatAdet { get; set; }
        public decimal SozFesihToplam { get; set; }
        public int SozFesihAdet { get; set; }
        public List<string> SozFesihFirmalar { get; set; } = new();

        // Trend verileri (önceki dönem karşılaştırma)
        public decimal FaturalarTrend { get; set; }
        public decimal TahsilatlarTrend { get; set; }
        public decimal SozlesmelerTrend { get; set; }

        // Önceki dönem tutarları (silik gösterim)
        public decimal PrevFaturalarToplam { get; set; }
        public decimal PrevTahsilatlarToplam { get; set; }

        // Hedef (dönem bazlı dinamik)
        public decimal AylikHedef { get; set; }
        public decimal HedefTutar { get; set; }  // = AylikHedef (dönem hedefi, view'da kullanılır)
        public decimal HedefGerceklesme { get; set; }
        public decimal HedefKalan { get; set; }
        public decimal HedefYuzde { get; set; }
        public int HedefAySayisi { get; set; }

        // YTD Hedef
        public decimal YtdHedef { get; set; }
        public decimal YtdGerceklesme { get; set; }
        public decimal YtdKalan { get; set; }
        public decimal YtdYuzde { get; set; }

        // Filtre bilgileri
        public string AktifFiltre { get; set; } = "ytd";
        public DateTime FiltreBaslangic { get; set; }
        public DateTime FiltreBitis { get; set; }

        // Tahsilat detay (dönem bazlı)
        public decimal TahsilEdilecek { get; set; }  // PAYDA = VadeToplam + GecmisBakiye
        public decimal TahsilatEdilen { get; set; }  // PAY = Tahsil_Tarihi bazlı Tahsil_Edilen
        public decimal TahsilKalan { get; set; }     // PAYDA - PAY
        public decimal TahDonemBakiye { get; set; }     // Bekleyen bakiye (vade ≤ dönem sonu)
        public decimal TahGecmisBakiye { get; set; }  // Vadesi geçmiş bakiye
        public int TahGecmisAdet { get; set; }        // Vadesi geçmiş fatura sayısı
        public decimal TahGecmisTahsilat { get; set; } // Geçmiş dönem tahsilat (Tahsil_Tarihi < dönem başı)

        // CEI Dönem (seçilen filtre başlangıcı → bugün)
        public decimal CeiDonemTahsilat { get; set; }
        public decimal CeiDonemVadesiGecmis { get; set; }
        public decimal CeiDonemOran { get; set; }

        // CEI Haftalık (mevcut takvim haftası)
        public decimal CeiHaftalikTahsilat { get; set; }
        public decimal CeiHaftalikToplam { get; set; }
        public decimal CeiHaftalikOran { get; set; }
        public DateTime HaftaBaslangic { get; set; }
        public DateTime HaftaSonu { get; set; }

        // CEI Aylık (mevcut takvim ayı)
        public decimal CeiAylikTahsilat { get; set; }
        public decimal CeiAylikToplam { get; set; }
        public decimal CeiAylikOran { get; set; }

        // CEI Yıllık (YTD — 01.01.2026 → bugün)
        public decimal CeiYillikTahsilat { get; set; }
        public decimal CeiYillikToplam { get; set; }
        public decimal CeiYillikOran { get; set; }

        // 2025 yılından kalan bakiye
        public decimal Legacy2025Bakiye { get; set; }

        // Beklenen tahsilat (dönem kalanında)
        public decimal BeklenenTahsilat { get; set; }
        public int BeklenenAdet { get; set; }
        public List<VIEW_CP_EXCEL_FATURA> BeklenenDetaylari { get; set; } = new();

        // Vadesi Geçmiş Alacak (01.01.2025'ten itibaren)
        public decimal VadesiGecmisAlacak { get; set; }
        public int VadesiGecmisAdet { get; set; }

        // Detay listeleri
        public List<VIEW_CP_EXCEL_FATURA> FaturaDetaylari { get; set; } = new();
        public List<VIEW_CP_EXCEL_FATURA> TahsilatDetaylari { get; set; } = new();
        public List<TBL_VARUNA_SOZLESME> SozlesmeDetaylari { get; set; } = new();
        // İzole üst kartlar (global filtrelerden bağımsız)
        public decimal FixedCurrentMonthTarget { get; set; }
        public decimal FixedCurrentMonthActual { get; set; }
        public decimal FixedCurrentMonthPct { get; set; }
        public decimal FixedYTDTarget { get; set; }  // Yıllık hedef (₺600M)
        public decimal FixedYTDActual { get; set; }  // YTD gerçekleşen
        public decimal FixedYTDPct { get; set; }
        public decimal FixedQuarterTarget { get; set; }
        public int RemainingMonths { get; set; }
        public int CurrentQuarter { get; set; }

        // Geçen hafta (CEI kartı — SSR'dan JS'e aktarılır)
        public decimal GecenHaftaTah { get; set; }
        public decimal GecenHaftaBakiye { get; set; }
        public string GecenHaftaBaslangicStr { get; set; } = "";
        public string GecenHaftaSonuStr { get; set; } = "";

        // Ürün kırılımı (SSR'dan JS'e aktarılır — redundant AJAX önlenir)
        public List<UrunKirilimItem> UrunKirilim { get; set; } = new();
    }

    public class UrunKirilimItem
    {
        public string Grup { get; set; } = "";
        public decimal Toplam { get; set; }
        public int Adet { get; set; }
    }
}
