using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using SOS.DbData;
using SOS.Services;
using Microsoft.EntityFrameworkCore;
using SOS.Models.ViewModels;
using SOS.Models.MsK;
using Microsoft.Extensions.Caching.Memory;

namespace SOS.Controllers
{
    [Authorize]
    public class CockpitController : Controller
    {
        private readonly IDbContextFactory<MskDbContext> _contextFactory;
        private readonly IMemoryCache _cache;
        private readonly ICockpitDataService _cockpitData;
        private const string CACHE_KEY_FATURALAR = "cockpit_faturalar";
        private const string CACHE_KEY_SIPARISLER = "cockpit_siparisler";
        private const string CACHE_KEY_URUNLER = "cockpit_urunler";
        private const string CACHE_KEY_SOZLESMELER = "cockpit_sozlesmeler";
        private const string CACHE_KEY_URUN_MAP = "cockpit_urun_map";
        private const string CACHE_KEY_MUSTERI_MAP = "cockpit_musteri_map";
        private const string CACHE_KEY_HEDEFLER = "cockpit_hedefler";
        private const string CACHE_KEY_VARUNA_TUTAR = "cockpit_varuna_tutar";
        private const string CACHE_KEY_URUN_GRUP_MAP = "cockpit_urun_grup_map"; // StockCode → AnaUrunAd

        private static readonly SemaphoreSlim _cacheLock = new(1, 1);

        public CockpitController(IDbContextFactory<MskDbContext> contextFactory, IMemoryCache cache, ICockpitDataService cockpitData)
        {
            _contextFactory = contextFactory;
            _cache = cache;
            _cockpitData = cockpitData;
        }

        #region Filter Parsing

        private (DateTime start, DateTime end, string filter, int months) ParseFilter(string? filter, string? startDate, string? endDate)
        {
            var now = DateTime.Now;
            var today = now.Date.AddDays(1).AddSeconds(-1); // 23:59:59
            var year = now.Year;
            DateTime start, end;
            int months;
            var fmtP = System.Globalization.CultureInfo.InvariantCulture;
            var style = System.Globalization.DateTimeStyles.None;

            if (!string.IsNullOrEmpty(startDate) && !string.IsNullOrEmpty(endDate)
                && DateTime.TryParseExact(startDate, "yyyy-MM-dd", fmtP, style, out var sd)
                && DateTime.TryParseExact(endDate, "yyyy-MM-dd", fmtP, style, out var ed))
            {
                start = sd.Date;
                end = ed.Date.AddDays(1).AddSeconds(-1);
                months = Math.Max(1, (end.Year - start.Year) * 12 + end.Month - start.Month + 1);
                return (start, end, "range", months);
            }

            switch (filter?.ToLowerInvariant())
            {
                case "ytd":
                    start = new DateTime(year, 1, 1);
                    end = today;
                    months = now.Month;
                    break;
                case "q1":
                    start = new DateTime(year, 1, 1);
                    end = new DateTime(year, 3, 31, 23, 59, 59);
                    months = 3;
                    break;
                case "q2":
                    start = new DateTime(year, 4, 1);
                    end = new DateTime(year, 6, 30, 23, 59, 59);
                    months = 3;
                    break;
                case "q3":
                    start = new DateTime(year, 7, 1);
                    end = new DateTime(year, 9, 30, 23, 59, 59);
                    months = 3;
                    break;
                case "q4":
                    start = new DateTime(year, 10, 1);
                    end = new DateTime(year, 12, 31, 23, 59, 59);
                    months = 3;
                    break;
                case "lastmonth":
                    var lmMonth = now.Month == 1 ? 12 : now.Month - 1;
                    var lmYear = now.Month == 1 ? year - 1 : year;
                    start = new DateTime(lmYear, lmMonth, 1);
                    end = new DateTime(lmYear, lmMonth, DateTime.DaysInMonth(lmYear, lmMonth), 23, 59, 59);
                    months = 1;
                    break;
                default:
                    filter = "month";
                    start = new DateTime(year, now.Month, 1);
                    end = new DateTime(year, now.Month, DateTime.DaysInMonth(year, now.Month), 23, 59, 59);
                    months = 1;
                    break;
            }

            return (start, end, filter ?? "month", months);
        }

        #endregion

        #region Status Helpers

        private static readonly HashSet<string> _negativeDurumSet = new(StringComparer.OrdinalIgnoreCase)
        {
            "İADE", "IADE", "İPTAL", "IPTAL"
        };

        private static bool IsRetDurum(string? durum)
            => !string.IsNullOrWhiteSpace(durum)
               && durum.AsSpan().Trim().Equals("RET".AsSpan(), StringComparison.OrdinalIgnoreCase);

        private static bool IsNegatifDurum(string? durum)
            => !string.IsNullOrWhiteSpace(durum)
               && _negativeDurumSet.Contains(durum.Trim());

        private static bool IsTahsilatOrKrediKarti(string? durum)
        {
            if (string.IsNullOrWhiteSpace(durum)) return false;
            var trimmed = durum.AsSpan().Trim();
            return trimmed.Equals("TAHSİL EDİLDİ".AsSpan(), StringComparison.OrdinalIgnoreCase)
                || trimmed.Equals("KREDİ KARTI".AsSpan(), StringComparison.OrdinalIgnoreCase)
                || trimmed.Equals("KREDI KARTI".AsSpan(), StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsDurumBos(string? durum)
            => string.IsNullOrWhiteSpace(durum);

        #endregion

        #region Cache (Parallel Loading)

        /// <summary>
        /// Cache'den veri yükler. Cold start'ta SemaphoreSlim ile race condition önlenir.
        /// Cache warm ise lock'a girmeden döner (hot path = 0 overhead).
        /// </summary>
        // Public static: hem controller hem CockpitCacheWarmer (BackgroundService) tarafından çağrılır.
        // forceRefresh=true → cache bypass (warmer background refresh için kullanır).
        internal static async Task<(List<VIEW_CP_EXCEL_FATURA> faturalar,
                            Dictionary<string, (string? AccountTitle, string? ProductName, decimal? Quantity)> urunMap,
                            Dictionary<string, string?> musteriMap,
                            List<TBL_VARUNA_SOZLESME> sozlesmeler,
                            Dictionary<int, decimal> hedefler,
                            Dictionary<string, decimal> varunaTutarMap,
                            Dictionary<string, List<(string Grup, decimal TlTutar)>> urunGrupMap)> LoadAllCachedDataAsync(
                            IDbContextFactory<MskDbContext> contextFactory,
                            IMemoryCache cache,
                            bool forceRefresh = false)
        {
            // Hot path — cache warm, lock'a gerek yok
            if (!forceRefresh
                && cache.TryGetValue(CACHE_KEY_FATURALAR, out List<VIEW_CP_EXCEL_FATURA>? cf) && cf != null
                && cache.TryGetValue(CACHE_KEY_URUN_MAP, out Dictionary<string, (string?, string?, decimal?)>? um) && um != null
                && cache.TryGetValue(CACHE_KEY_MUSTERI_MAP, out Dictionary<string, string?>? mm) && mm != null
                && cache.TryGetValue(CACHE_KEY_SOZLESMELER, out List<TBL_VARUNA_SOZLESME>? cs) && cs != null
                && cache.TryGetValue(CACHE_KEY_HEDEFLER, out Dictionary<int, decimal>? ch) && ch != null
                && cache.TryGetValue(CACHE_KEY_VARUNA_TUTAR, out Dictionary<string, decimal>? vt) && vt != null
                && cache.TryGetValue(CACHE_KEY_URUN_GRUP_MAP, out Dictionary<string, List<(string Grup, decimal TlTutar)>>? ug) && ug != null)
            {
                return (cf, um, mm, cs, ch, vt, ug);
            }

            // Cold path — lock ile tek thread DB'ye gider, diğerleri bekler
            await _cacheLock.WaitAsync();
            try
            {
                // Double-check: lock beklerken başka thread doldurmuş olabilir (force refresh'te skip)
                if (!forceRefresh
                    && cache.TryGetValue(CACHE_KEY_FATURALAR, out List<VIEW_CP_EXCEL_FATURA>? cf2) && cf2 != null
                    && cache.TryGetValue(CACHE_KEY_URUN_MAP, out Dictionary<string, (string?, string?, decimal?)>? um2) && um2 != null
                    && cache.TryGetValue(CACHE_KEY_MUSTERI_MAP, out Dictionary<string, string?>? mm2) && mm2 != null
                    && cache.TryGetValue(CACHE_KEY_SOZLESMELER, out List<TBL_VARUNA_SOZLESME>? cs2) && cs2 != null
                    && cache.TryGetValue(CACHE_KEY_HEDEFLER, out Dictionary<int, decimal>? ch2) && ch2 != null
                    && cache.TryGetValue(CACHE_KEY_VARUNA_TUTAR, out Dictionary<string, decimal>? vt2) && vt2 != null
                    && cache.TryGetValue(CACHE_KEY_URUN_GRUP_MAP, out Dictionary<string, List<(string Grup, decimal TlTutar)>>? ug2) && ug2 != null)
                {
                    return (cf2, um2, mm2, cs2, ch2, vt2, ug2);
                }

                // Kendi bağımsız context'i oluştur — scoped context ile çakışma olmaz
                using var db = contextFactory.CreateDbContext();

                // VIEW'da aynı Fatura_No tekrarlayabiliyor — Fatura_No bazlı deduplicate
                var faturalar = (await db.VIEW_CP_EXCEL_FATURAs.AsNoTracking().ToListAsync())
                    .GroupBy(f => f.Fatura_No ?? f.GetHashCode().ToString())
                    .Select(g => g.First())
                    .ToList();

                var siparisler = await db.TBL_VARUNA_SIPARIs.AsNoTracking()
                    .Where(s => s.OrderId != null)
                    .Select(s => new SiparisDto
                    {
                        SerialNumber = s.SerialNumber,
                        OrderId = s.OrderId,
                        AccountTitle = s.AccountTitle,
                        OrderStatus = s.OrderStatus,
                        TotalNetAmount = s.TotalNetAmount,
                        InvoiceDate = s.InvoiceDate,
                        SAPOutReferenceCode = s.SAPOutReferenceCode
                    })
                    .ToListAsync();

                // CrmOrderId + StockCode bazlı dedupe — CLAUDE.md kuralı: aynı sipariş+stock bir satır olmalı
                var urunler = (await db.TBL_VARUNA_SIPARIS_URUNLERIs.AsNoTracking()
                    .Where(u => u.CrmOrderId != null)
                    .Select(u => new UrunDto { CrmOrderId = u.CrmOrderId, ProductName = u.ProductName, StockCode = u.StockCode, Quantity = u.Quantity, Total = u.Total })
                    .ToListAsync())
                    .GroupBy(u => new { u.CrmOrderId, u.StockCode })
                    .Select(g => new UrunDto
                    {
                        CrmOrderId = g.Key.CrmOrderId,
                        StockCode = g.Key.StockCode,
                        ProductName = g.First().ProductName,
                        Quantity = g.Sum(x => x.Quantity ?? 0),
                        Total = g.Sum(x => x.Total ?? 0)
                    })
                    .ToList();

                var sozlesmeler = await db.TBL_VARUNA_SOZLESMEs.AsNoTracking()
                    .Where(s => s.RenewalDate.HasValue)
                    .ToListAsync();

                // Lookup map'leri oluştur
                var urunMap = siparisler
                    .Join(urunler, s => s.OrderId, u => u.CrmOrderId,
                          (s, u) => new { s.SerialNumber, s.AccountTitle, u.ProductName, u.Quantity })
                    .Where(x => x.SerialNumber != null)
                    .GroupBy(x => x.SerialNumber!)
                    .ToDictionary(g => g.Key, g => (g.First().AccountTitle, g.First().ProductName, g.First().Quantity));

                var musteriMap = siparisler
                    .Where(s => s.SerialNumber != null)
                    .GroupBy(s => s.SerialNumber!)
                    .ToDictionary(g => g.Key, g => g.First().AccountTitle);

                // Varuna kalem bazlı net tutar: OrderId → SUM(kalem.Total)
                // İptal kalemleri negatif Total taşır → net toplam Excel ile tutarlı olur
                var kalemToplamByOrder = urunler
                    .GroupBy(u => u.CrmOrderId!)
                    .ToDictionary(g => g.Key, g => g.Sum(u => u.Total ?? 0));

                // SerialNumber → TL tutar map
                // Kalem toplamı varsa: (kalemNet / kalemBrüt) * TotalNetAmount → TL net
                // Kalem toplamı yoksa veya sıfırsa: TotalNetAmount direkt
                var varunaTutarMap = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase);
                foreach (var sip in siparisler.Where(s =>
                    s.SerialNumber != null
                    && s.TotalNetAmount.HasValue && s.TotalNetAmount.Value > 0
                    && string.Equals(s.OrderStatus, "Closed", StringComparison.OrdinalIgnoreCase)))
                {
                    var tna = sip.TotalNetAmount!.Value;

                    // Kalemlerin net toplamı (iptal kalemleri negatif)
                    if (sip.OrderId != null && kalemToplamByOrder.TryGetValue(sip.OrderId, out var kalemNet))
                    {
                        // Kalemlerin brüt toplamı (mutlak değer — oran hesabı için)
                        var kalemBrut = urunler
                            .Where(u => u.CrmOrderId == sip.OrderId && (u.Total ?? 0) > 0)
                            .Sum(u => u.Total ?? 0);

                        if (kalemBrut > 0 && kalemNet < kalemBrut)
                        {
                            // İptal/iade kalemleri var → TL tutarı oranla düşür
                            varunaTutarMap[sip.SerialNumber!] = tna * (kalemNet / kalemBrut);
                        }
                        else
                        {
                            varunaTutarMap[sip.SerialNumber!] = tna;
                        }
                    }
                    else
                    {
                        varunaTutarMap[sip.SerialNumber!] = tna;
                    }
                }

                // Aylık hedefler (Ay → HedefTutar) — 2026 GENEL
                var hedefler = await db.TBLSOS_HEDEF_AYLIKs
                    .AsNoTracking()
                    .Where(h => h.Yil == DateTime.Now.Year && h.Tip == "GENEL" && h.Aktif)
                    .ToDictionaryAsync(h => h.Ay, h => h.HedefTutar);

                // Tahakkuk override map: SapReferansNo + FaturaNo → TahakkukTarihi (dual-key)
                var tahakkukRecords = await db.TBLSOS_FATURA_TAHAKKUKs.AsNoTracking()
                    .Where(t => t.Aktif)
                    .Select(t => new { t.SapReferansNo, t.FaturaNo, t.TahakkukTarihi })
                    .ToListAsync();
                var tahakkukMap = new Dictionary<string, DateTime>(StringComparer.OrdinalIgnoreCase);
                foreach (var r in tahakkukRecords)
                {
                    tahakkukMap[r.SapReferansNo] = r.TahakkukTarihi;       // SAP key (primary)
                    if (!string.IsNullOrEmpty(r.FaturaNo))
                        tahakkukMap[r.FaturaNo] = r.TahakkukTarihi;          // FaturaNo key (compat)
                }

                // İade/Ret faturalarının Varuna karşılığını blacklist'e al
                // VIEW'de İADE/RET olan VE Varuna'da eşleşen fatura → aynı sipariş iptal olmuş
                // O siparişin pozitif tutarını da dip toplamdan çıkarmalıyız
                var iadeRetBlacklist = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (var f in faturalar)
                {
                    if (f.Fatura_No != null
                        && (IsRetDurum(f.Durum) || IsNegatifDurum(f.Durum))
                        && varunaTutarMap.ContainsKey(f.Fatura_No))
                    {
                        iadeRetBlacklist.Add(f.Fatura_No);
                    }
                }
                // Blacklist'teki siparişleri varunaTutarMap'ten çıkar
                foreach (var fn in iadeRetBlacklist)
                    varunaTutarMap.Remove(fn);

                // Her faturaya NetTutar (Varuna KDV hariç), KdvDahilTutar (Excel), VarunaEslesti
                // ve EfektifFaturaTarihi (tahakkuk varsa onu, yoksa Fatura_Tarihi'ni) ata
                foreach (var f in faturalar)
                {
                    var excelTutar = f.Fatura_Toplam ?? 0;
                    f.KdvDahilTutar = excelTutar;
                    if (f.Fatura_No != null && varunaTutarMap.TryGetValue(f.Fatura_No, out var vNet))
                    {
                        f.NetTutar = vNet;
                        f.VarunaEslesti = true;
                    }
                    else
                    {
                        f.NetTutar = excelTutar; // Varuna'da yoksa Excel tutarı fallback
                        f.VarunaEslesti = false;
                    }

                    // Tahakkuk override
                    if (f.Fatura_No != null && tahakkukMap.TryGetValue(f.Fatura_No, out var tahakkukTarihi))
                    {
                        f.EfektifFaturaTarihi = tahakkukTarihi;
                        f.TahakkukVar = true;
                    }
                    else
                    {
                        f.EfektifFaturaTarihi = f.Fatura_Tarihi;
                        f.TahakkukVar = false;
                    }
                }

                // ── Sentetik fatura: Varuna Closed + tahakkuklu ama VIEW'de yok ──
                // Sadece tahakkuk kaydı olan siparişler sentetik olarak eklenir.
                // Tahakkuksuz Varuna siparişleri dahil edilmez (VIEW'e girinceye kadar beklenir).
                var excelFaturaNoSet = new HashSet<string>(
                    faturalar.Where(f => f.Fatura_No != null).Select(f => f.Fatura_No!),
                    StringComparer.OrdinalIgnoreCase);

                var sentetikEklenen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                foreach (var sip in siparisler.Where(s =>
                    s.TotalNetAmount.HasValue && s.TotalNetAmount.Value > 0
                    && string.Equals(s.OrderStatus, "Closed", StringComparison.OrdinalIgnoreCase)))
                {
                    var faturaNo = sip.SerialNumber
                        ?? (!string.IsNullOrEmpty(sip.SAPOutReferenceCode) ? $"SAP:{sip.SAPOutReferenceCode.Trim()}" : null);
                    if (faturaNo == null) continue;

                    // Zaten VIEW'de varsa atla
                    if (excelFaturaNoSet.Contains(faturaNo) || !sentetikEklenen.Add(faturaNo)) continue;

                    // Tahakkuk lookup: SN, SAP no, veya SAP: prefix
                    DateTime? tahakkukOverride = null;
                    if (sip.SerialNumber != null && tahakkukMap.TryGetValue(sip.SerialNumber, out var thDate))
                        tahakkukOverride = thDate;
                    else if (!string.IsNullOrEmpty(sip.SAPOutReferenceCode) && tahakkukMap.TryGetValue(sip.SAPOutReferenceCode.Trim(), out var thDate2))
                        tahakkukOverride = thDate2;
                    else if (tahakkukMap.TryGetValue(faturaNo, out var thDate3))
                        tahakkukOverride = thDate3;

                    // Tahakkuk yoksa sentetik ekleme — VIEW'e girinceye kadar bekle
                    if (!tahakkukOverride.HasValue) continue;

                    var efektifTarih = tahakkukOverride.Value;

                    var sentetik = new VIEW_CP_EXCEL_FATURA
                    {
                        Fatura_No = faturaNo,
                        Fatura_Tarihi = sip.InvoiceDate,
                        Fatura_Toplam = sip.TotalNetAmount,
                        Fatura_Vade_Tarihi = sip.InvoiceDate,
                        Tahsil_Edilen = 0,
                        Bekleyen_Bakiye = sip.TotalNetAmount,
                        Durum = null,
                        NetTutar = sip.TotalNetAmount,
                        KdvDahilTutar = sip.TotalNetAmount,
                        VarunaEslesti = true,
                        MusteriUnvan = sip.AccountTitle,
                        EfektifFaturaTarihi = efektifTarih,
                        TahakkukVar = tahakkukOverride.HasValue
                    };
                    faturalar.Add(sentetik);
                    if (!varunaTutarMap.ContainsKey(faturaNo))
                        varunaTutarMap[faturaNo] = sip.TotalNetAmount.Value;
                }

                // Ürün grup eşleştirme: StockCode → AnaUrunAd
                // NOT: DB'de nadiren duplicate StokKodu olabiliyor (EH.02.018 gibi) — GroupBy ilk kaydı alır
                // AnaUrun null ise (FK bozuksa) kayıt atlanır → kalem ürün kırılımına girmez
                var eslestirmeler = (await db.TBLSOS_URUN_ESLESTIRMEs.AsNoTracking()
                    .Include(e => e.AnaUrun)
                    .ToListAsync())
                    .Where(e => e.AnaUrun != null && !string.IsNullOrEmpty(e.AnaUrun.Ad))
                    .GroupBy(e => e.StokKodu)
                    .ToDictionary(g => g.Key, g => g.First().AnaUrun!.Ad);

                // Fatura_No (SerialNumber) → kalem bazlı ürün grubu TL dağılımı
                // Her kalem için: (kalem.Total / toplamDöviz) * TotalNetAmount → ürün grubuna
                // NOT: TBLSOS_URUN_ESLESTIRME'de bulunmayan StockCode'lar SKIP edilir (UI'da "Diğer" gösterilmez).
                //      Bu durumda ürün kırılımı toplamı, fatura dip toplamından küçük olabilir.
                var urunGrupMap = new Dictionary<string, List<(string Grup, decimal TlTutar)>>();
                var urunByCrmOrder = urunler.Where(u => u.CrmOrderId != null)
                    .GroupBy(u => u.CrmOrderId!).ToDictionary(g => g.Key, g => g.ToList());
                foreach (var siparis in siparisler.Where(s => s.OrderId != null
                    && s.TotalNetAmount.HasValue && s.TotalNetAmount.Value > 0
                    && string.Equals(s.OrderStatus, "Closed", StringComparison.OrdinalIgnoreCase)))
                {
                    // Key: SerialNumber varsa onu, yoksa SAP:xxx
                    var mapKey = siparis.SerialNumber
                        ?? (!string.IsNullOrEmpty(siparis.SAPOutReferenceCode)
                            ? $"SAP:{siparis.SAPOutReferenceCode.Trim()}" : null);
                    if (mapKey == null) continue;
                    if (urunGrupMap.ContainsKey(mapKey)) continue;
                    if (!urunByCrmOrder.TryGetValue(siparis.OrderId!, out var sipUrunleri)) continue;
                    var toplamDoviz = sipUrunleri.Sum(u => u.Total ?? 0);
                    if (toplamDoviz == 0) continue;
                    var kalemler = new List<(string Grup, decimal TlTutar)>();
                    foreach (var u in sipUrunleri)
                    {
                        if (u.StockCode == null || !eslestirmeler.TryGetValue(u.StockCode, out var grup))
                            continue;
                        var tlTutar = (u.Total ?? 0) / toplamDoviz * siparis.TotalNetAmount!.Value;
                        kalemler.Add((grup, tlTutar));
                    }
                    urunGrupMap[mapKey] = kalemler;
                }

                // Cache'e yaz — TTL 15 dk (CacheWarmer her 4 dk'da refresh eder, bu sliding buffer)
                var ttl = TimeSpan.FromMinutes(15);
                cache.Set(CACHE_KEY_FATURALAR, faturalar, ttl);
                cache.Set(CACHE_KEY_SOZLESMELER, sozlesmeler, ttl);
                cache.Set(CACHE_KEY_URUN_MAP, urunMap, ttl);
                cache.Set(CACHE_KEY_MUSTERI_MAP, musteriMap, ttl);
                cache.Set(CACHE_KEY_HEDEFLER, hedefler, ttl);
                cache.Set(CACHE_KEY_VARUNA_TUTAR, varunaTutarMap, ttl);
                cache.Set(CACHE_KEY_URUN_GRUP_MAP, urunGrupMap, ttl);

                return (faturalar, urunMap, musteriMap, sozlesmeler, hedefler, varunaTutarMap, urunGrupMap);
            }
            finally
            {
                _cacheLock.Release();
            }
        }

        private static void MapMusteriUrun(
            IEnumerable<VIEW_CP_EXCEL_FATURA> kayitlar,
            Dictionary<string, (string? AccountTitle, string? ProductName, decimal? Quantity)> urunMap,
            Dictionary<string, string?> musteriMap)
        {
            foreach (var f in kayitlar)
            {
                if (f.Fatura_No == null) continue;
                if (urunMap.TryGetValue(f.Fatura_No, out var urun))
                {
                    f.MusteriUnvan = urun.AccountTitle;
                    f.UrunAdi = urun.ProductName;
                    f.Miktar = urun.Quantity;
                }
                else if (musteriMap.TryGetValue(f.Fatura_No, out var musteri))
                {
                    f.MusteriUnvan = musteri;
                }
            }
        }

        // DTO'lar
        private class SiparisDto
        {
            public string? SerialNumber { get; set; }
            public string? OrderId { get; set; }
            public string? AccountTitle { get; set; }
            public string? OrderStatus { get; set; }
            public decimal? TotalNetAmount { get; set; }
            public DateTime? InvoiceDate { get; set; }
            public string? SAPOutReferenceCode { get; set; }
        }

        private class UrunDto
        {
            public string? CrmOrderId { get; set; }
            public string? ProductName { get; set; }
            public string? StockCode { get; set; }
            public decimal? Quantity { get; set; }
            public decimal? Total { get; set; }
        }

        #endregion

        #region Single-Pass Metrics

        /// <summary>
        /// allFaturalar üzerinde TEK geçişte tüm metrikleri hesaplar.
        /// 15+ ayrı LINQ iterasyonu yerine O(n) tek döngü.
        /// </summary>
        private struct FaturaMetrics
        {
            // Dönem fatura
            public decimal FatToplam;
            public int FatAdet;
            // Dönem tahsilat
            public decimal TahEdilen;      // PAY: Tahsil_Tarihi dönemde → SUM(Tahsil_Edilen)
            public decimal TahBakiye;      // Bekleyen_Bakiye toplamı (vade ≤ dönem sonu)
            public int TahAdet;
            public decimal TahGecmisTahsilat; // Tahsil_Tarihi dönem ÖNCESI → SUM(Tahsil_Edilen)
            // Önceki dönem (trend)
            public decimal PrevFatToplam;
            public decimal PrevTahToplam;
            // CEI Dönem
            public decimal CeiDonemVgBakiye;
            // CEI Haftalık (PAY: Tahsil_Tarihi hafta içi, PAYDA: efektif ≤ hafta sonu bakiye + pay)
            public decimal HaftalikTah;       // PAY: SUM(Tahsil_Edilen) where Tahsil_Tarihi in hafta
            public decimal HaftalikBakiye;    // SUM(Bekleyen_Bakiye) where efektif ≤ hafta sonu & bakiye > 0
            // CEI Aylık
            public decimal AylikTah;
            public decimal AylikBakiye;
            // CEI YTD
            public decimal YtdTahToplam;
            public decimal YtdBakiye;
            public decimal YtdVgBakiye;
            // Legacy 2025
            public decimal Legacy2025Bakiye;
            // Vadesi geçmiş
            public decimal VadesiGecmisAlacak;
            public int VadesiGecmisAdet;
            // Beklenen
            public decimal BeklenenTahsilat;
            public int BeklenenAdet;
            // Fixed cards
            public decimal FixedMonthActual;
            public decimal FixedYTDActual;
            // YTD Fatura
            public decimal YtdFatGerceklesme;
            // Varuna dışı (not için)
            public decimal VarunaDisiToplam;
            public int VarunaDisiAdet;
        }

        private static FaturaMetrics ComputeMetrics(
            List<VIEW_CP_EXCEL_FATURA> allFaturalar,
            DateTime start, DateTime end,
            DateTime prevStart, DateTime prevEnd,
            DateTime donemSonuCei,
            DateTime haftaBaslangic, DateTime haftaSonu,
            DateTime ayBaslangic, DateTime aySonu,
            DateTime ytdStart, DateTime ytdEnd, DateTime bugun,
            DateTime fixedMonthStart, DateTime fixedMonthEnd,
            DateTime fixedYTDStart, DateTime fixedYTDEnd)
        {
            var m = new FaturaMetrics();

            // ── Fatura toplamları: UNIQUE Fatura_No bazında (mükerrer sayım önleme) ──
            // Aynı Fatura_No birden fazla satırda olabilir (farklı Durum ile) → sadece 1 kez say
            var fatNoDonem = new HashSet<string>();
            var fatNoPrev = new HashSet<string>();
            var fatNoYtd = new HashSet<string>();
            var fatNoFixedMonth = new HashSet<string>();
            var fatNoFixedYTD = new HashSet<string>();

            for (int i = 0; i < allFaturalar.Count; i++)
            {
                var f = allFaturalar[i];
                // NetTutar: Varuna KDV hariç (yoksa Excel fallback) — LoadAllCachedDataAsync'te atandı
                var tutar = f.NetTutar ?? 0m;
                var durumBos = IsDurumBos(f.Durum);
                var isTahsilat = IsTahsilatOrKrediKarti(f.Durum);

                // ── İade/İptal/Ret faturalar tamamen atlanır ──
                if (IsRetDurum(f.Durum) || IsNegatifDurum(f.Durum))
                    continue;

                // ── VarunaDışı faturalar dip toplama dahil edilmez ──
                // Sadece Varuna Closed eşleşen faturalar sayılır (sentetik dahil)
                // VarunaDışı ayrı metrikte takip edilir
                if (!f.VarunaEslesti)
                {
                    // VarunaDışı metrikleri (ayrı gösterim)
                    if (f.EfektifFaturaTarihi.HasValue)
                    {
                        var ftVd = f.EfektifFaturaTarihi.Value;
                        var fNoVd = f.Fatura_No ?? $"__vd_{i}";
                        if (ftVd >= start && ftVd <= end && fatNoDonem.Add(fNoVd))
                        {
                            m.VarunaDisiToplam += tutar;
                            m.VarunaDisiAdet++;
                        }
                    }
                    continue;
                }

                var netTutar = tutar;
                // Bakiye: Fatura_Toplam - Tahsil_Edilen (finans mantığı)
                var bakiye = (f.Fatura_Toplam ?? 0) - (f.Tahsil_Edilen ?? 0);

                // ── Fatura tarihi bazlı metrikler (unique Fatura_No bazında) ──
                if (f.EfektifFaturaTarihi.HasValue)
                {
                    var ft = f.EfektifFaturaTarihi.Value;
                    var fNo = f.Fatura_No ?? $"__row_{i}";

                    // Dönem fatura — unique Fatura_No bazında (sadece Varuna eşleşen)
                    if (ft >= start && ft <= end && fatNoDonem.Add(fNo))
                    {
                        m.FatToplam += netTutar;
                        m.FatAdet++;
                    }

                    // Önceki dönem fatura (trend) — unique
                    if (ft >= prevStart && ft <= prevEnd && fatNoPrev.Add(fNo))
                        m.PrevFatToplam += netTutar;

                    // YTD fatura gerçekleşme — unique
                    if (ft >= ytdStart && ft <= end && fatNoYtd.Add(fNo))
                        m.YtdFatGerceklesme += netTutar;

                    // Fixed month — unique
                    if (ft >= fixedMonthStart && ft <= fixedMonthEnd && fatNoFixedMonth.Add(fNo))
                        m.FixedMonthActual += netTutar;

                    // Fixed YTD — unique
                    if (ft >= fixedYTDStart && ft <= fixedYTDEnd && fatNoFixedYTD.Add(fNo))
                        m.FixedYTDActual += netTutar;
                }

                // ── Efektif tarih: Ödeme sözü varsa O, yoksa vade tarihi ──
                // İade/Ret hariç — tahsilat + vadesi geçmiş + beklenen hepsi bu tarihe göre
                // ── Tahsilat hesapları: İADE/RET hariç tüm faturalar ──
                if (!IsNegatifDurum(f.Durum) && !IsRetDurum(f.Durum))
                {
                    var tahsil = f.Tahsil_Edilen ?? 0;
                    var bekleyenBakiye = f.Bekleyen_Bakiye ?? ((f.Fatura_Toplam ?? 0) - tahsil);
                    var tahsilTarihi = f.Tahsil_Tarihi;
                    var vt = f.Fatura_Vade_Tarihi;

                    // ── PAY: Tahsil_Tarihi dönemde → SUM(Tahsil_Edilen) ──
                    if (tahsilTarihi.HasValue)
                    {
                        var tt = tahsilTarihi.Value;
                        // Dönem kartı
                        if (tt >= start && tt <= end) { m.TahEdilen += tahsil; m.TahAdet++; }
                        // Geçmiş dönem tahsilat (dönem başından önce)
                        if (tt < start) m.TahGecmisTahsilat += tahsil;
                        // Önceki dönem (trend)
                        if (tt >= prevStart && tt <= prevEnd) m.PrevTahToplam += tahsil;
                        // Haftalık
                        if (tt >= haftaBaslangic && tt <= haftaSonu) m.HaftalikTah += tahsil;
                        // Aylık
                        if (tt >= ayBaslangic && tt <= aySonu) m.AylikTah += tahsil;
                        // YTD
                        if (tt >= ytdStart && tt <= ytdEnd) m.YtdTahToplam += tahsil;
                    }

                    // ── PAYDA bakiye: Fatura_Vade_Tarihi ≤ dönem sonu & bekleyenBakiye > 0 ──
                    if (vt.HasValue && bekleyenBakiye > 0)
                    {
                        if (vt.Value <= end) m.TahBakiye += bekleyenBakiye;
                        if (vt.Value <= haftaSonu) m.HaftalikBakiye += bekleyenBakiye;
                        if (vt.Value <= aySonu) m.AylikBakiye += bekleyenBakiye;
                        if (vt.Value <= ytdEnd) m.YtdBakiye += bekleyenBakiye;
                    }

                    // ── Vadesi geçmiş / beklenen: Fatura_Vade_Tarihi bazlı, sadece durum boş ──
                    if (f.Fatura_Vade_Tarihi.HasValue && bakiye > 0 && durumBos)
                    {
                        var vd = f.Fatura_Vade_Tarihi.Value;

                        if (vd >= start && vd < bugun) m.CeiDonemVgBakiye += bakiye;
                        if (vd >= ytdStart && vd < bugun) m.YtdVgBakiye += bakiye;
                        if (vd >= new DateTime(2025, 1, 1) && vd < new DateTime(2026, 1, 1)) m.Legacy2025Bakiye += bakiye;

                        if (vd < start) { m.VadesiGecmisAlacak += bakiye; m.VadesiGecmisAdet++; }
                        if (vd > bugun && vd <= end) { m.BeklenenTahsilat += bakiye; m.BeklenenAdet++; }
                    }
                }
            }

            return m;
        }

        #endregion

        #region Actions

        /// <summary>
        /// Cache warmer durumu — UI "güncelleme X dk önce" göstergesi için.
        /// Class-level [Authorize] devralır.
        /// </summary>
        [HttpGet]
        public IActionResult CacheStats([FromServices] SOS.Services.CockpitCacheWarmerState state)
        {
            var now = DateTime.UtcNow;
            int? ageSeconds = state.LastRefreshAt.HasValue
                ? (int)(now - state.LastRefreshAt.Value).TotalSeconds
                : null;
            return Json(new
            {
                lastRefreshAt = state.LastRefreshAt,
                lastRefreshAtLocal = state.LastRefreshAt?.ToLocalTime().ToString("HH:mm:ss"),
                ageSeconds,
                lastRefreshDurationMs = state.LastRefreshDurationMs,
                refreshCount = state.RefreshCount,
                failureCount = state.FailureCount,
                lastError = state.LastError,
                lastErrorAt = state.LastErrorAt
            });
        }

        public async Task<IActionResult> Index(string? filter, string? startDate, string? endDate)
        {
            var (start, end, activeFilter, months) = ParseFilter(filter, startDate, endDate);
            var now = DateTime.Now;
            var bugun = now.Date;
            var today = bugun.AddDays(1).AddSeconds(-1);

            // ══════════════════════════════════════════════════════════════
            // SP'lerden kart verileri + eski cache (ürün kırılımı, CEI için)
            // ══════════════════════════════════════════════════════════════
            var spFaturaTask = _cockpitData.GetFaturaOzetAsync(start, end);
            var spTahsilatTask = _cockpitData.GetTahsilatOzetAsync(start, end);
            var spSozlesmeTask = _cockpitData.GetSozlesmeOzetAsync(start, end);
            var cacheTask = LoadAllCachedDataAsync(_contextFactory, _cache);

            await Task.WhenAll(spFaturaTask, spTahsilatTask, spSozlesmeTask, cacheTask);

            var spFatura = spFaturaTask.Result;
            var spTahsilat = spTahsilatTask.Result;
            var spSozlesme = spSozlesmeTask.Result;
            var (allFaturalar, urunMap, musteriMap, sozlesmeler, hedefler, varunaTutarMap, urunGrupMap) = cacheTask.Result;

            // ══════════════════════════════════════════════════════════════
            // Single-pass: Tüm KPI'lar TEK döngüde hesaplanır
            // ══════════════════════════════════════════════════════════════
            var prevDuration = end - start;
            var prevStart = start.AddDays(-prevDuration.TotalDays);
            var prevEnd = start.AddSeconds(-1);

            // CEI dönem sonu: seçili filtrenin TAM dönem sonu
            // Bu ay → 30 Nisan, Geçen ay → 31 Mart, Q1 → 31 Mart, YTD → bugün
            var donemSonuCei = end;
            if (activeFilter == "month" || activeFilter == "lastmonth")
                donemSonuCei = new DateTime(end.Year, end.Month, DateTime.DaysInMonth(end.Year, end.Month), 23, 59, 59);

            var ayBaslangic = new DateTime(now.Year, now.Month, 1);
            var aySonu = new DateTime(now.Year, now.Month, DateTime.DaysInMonth(now.Year, now.Month), 23, 59, 59);
            var ytdStart = new DateTime(now.Year, 1, 1);

            // Hafta başlangıcı (Pazartesi) ve sonu (Cuma) — 5 iş günü
            var dayOfWeek = bugun.DayOfWeek == DayOfWeek.Sunday ? 6 : (int)bugun.DayOfWeek - 1;
            var haftaBaslangic = bugun.AddDays(-dayOfWeek);
            var haftaSonu = haftaBaslangic.AddDays(4).AddHours(23).AddMinutes(59).AddSeconds(59);

            var fixedMonthStart = ayBaslangic;
            var fixedMonthEnd = aySonu;
            var fixedYTDStart = ytdStart;
            var fixedYTDEnd = today; // Ocak 1 → bugün (Nisan dahil)

            var donemSonu = end;
            if (activeFilter == "month" || activeFilter == "lastmonth")
                donemSonu = new DateTime(end.Year, end.Month, DateTime.DaysInMonth(end.Year, end.Month), 23, 59, 59);

            var m = ComputeMetrics(allFaturalar, start, end, prevStart, prevEnd,
                donemSonuCei, haftaBaslangic, haftaSonu,
                ayBaslangic, aySonu, ytdStart, today, bugun,
                fixedMonthStart, fixedMonthEnd, fixedYTDStart, fixedYTDEnd);

            // Tahsilat kartı: PAYDA = bekleyen bakiye + tahsil edilen (toplam alacak)
            var tahsilEdilecek = m.TahBakiye + m.TahEdilen;  // PAYDA
            var tahsilKalan = m.TahBakiye;                    // Kalan = bekleyen bakiye

            // CEI hesapları
            var ceiDonemTahsilat = m.TahEdilen;  // dönemde gerçek tahsil edilen
            var ceiDonemOran = tahsilEdilecek > 0
                ? Math.Round(ceiDonemTahsilat / tahsilEdilecek * 100, 1) : 0;
            // CEI: PAYDA = bekleyen bakiye + tahsil edilen (dönem sonuna kadar vadesi gelen tüm alacak)
            var haftalikPayda = m.HaftalikBakiye + m.HaftalikTah;
            var aylikPayda = m.AylikBakiye + m.AylikTah;
            var ytdPayda = m.YtdBakiye + m.YtdTahToplam;
            var ceiHaftalikOran = haftalikPayda > 0
                ? Math.Round(m.HaftalikTah / haftalikPayda * 100, 1) : 0;
            var ceiAylikOran = aylikPayda > 0
                ? Math.Round(m.AylikTah / aylikPayda * 100, 1) : 0;
            var ceiYillikOran = ytdPayda > 0
                ? Math.Round(m.YtdTahToplam / ytdPayda * 100, 1) : 0;

            // Hedefler — DB'den ay bazlı (TBLSOS_HEDEF_AYLIK)
            // Helper: belirli ay aralığı için hedef toplamı
            decimal HedefToplam(int ayBas, int aySon) =>
                Enumerable.Range(ayBas, aySon - ayBas + 1).Sum(ay => hedefler.GetValueOrDefault(ay, 0));

            // Dönem hedefi: filtredeki aylar
            var donemBasAy = start.Month;
            var donemSonAy = end.Month;
            var donemHedef = HedefToplam(donemBasAy, donemSonAy);
            var hedefKalan = Math.Max(donemHedef - spFatura.Toplam, 0);
            var hedefYuzde = donemHedef > 0
                ? Math.Round(Math.Min(spFatura.Toplam / donemHedef * 100, 100), 1) : 0;

            // YTD hedef: Ocak → filtrenin bitiş ayı
            var ytdAySayisi = Math.Max(1, end.Month);
            var ytdHedef = HedefToplam(1, end.Month);
            var ytdKalan = Math.Max(ytdHedef - m.YtdFatGerceklesme, 0);

            // Bu ay hedefi
            var fixedMonthTarget = hedefler.GetValueOrDefault(now.Month, 0);
            var fixedMonthPct = fixedMonthTarget > 0 ? Math.Round(m.FixedMonthActual / fixedMonthTarget * 100, 1) : 0;

            // Yıllık hedef: tüm 12 ay toplamı (₺600M)
            var fixedAnnualTarget = HedefToplam(1, 12);
            var fixedAnnualActual = m.FixedYTDActual;
            var fixedAnnualPct = fixedAnnualTarget > 0 ? Math.Round(fixedAnnualActual / fixedAnnualTarget * 100, 1) : 0;

            // Çeyrek hesabı
            var currentQuarter = (now.Month - 1) / 3 + 1;
            var quarterStartMonth = (currentQuarter - 1) * 3 + 1;
            var fixedQuarterTarget = HedefToplam(quarterStartMonth, quarterStartMonth + 2);
            var fixedQuarterMonths = now.Month - quarterStartMonth + 1;

            // Kalan ay
            var remainingMonths = 12 - now.Month;

            // Eski fixedYTD alanlarını annual ile eşle (ViewModel uyumu)
            var fixedYTDTarget = fixedAnnualTarget;
            var fixedYTDActual = fixedAnnualActual;
            var fixedYTDPct = fixedAnnualPct;

            // Sözleşmeler: seçili dönemde RenewalDate olanlar
            var sozDonem = sozlesmeler.Where(s => s.RenewalDate!.Value >= start && s.RenewalDate!.Value <= end).ToList();
            var sozToplam = sozDonem.Sum(s => s.TotalAmount ?? 0);
            var sozArchivedList = sozDonem.Where(s => string.Equals(s.ContractStatus, "Archived", StringComparison.OrdinalIgnoreCase)).ToList();
            var sozArchivedToplam = sozArchivedList.Sum(s => s.TotalAmount ?? 0);
            var sozArchivedAdet = sozArchivedList.Count;

            // Gecikmiş sözleşmeler: RenewalDate < dönem başı, hâlâ Archived değil
            var sozGecikmisList = sozlesmeler.Where(s => s.RenewalDate!.Value < start
                && !string.Equals(s.ContractStatus, "Archived", StringComparison.OrdinalIgnoreCase)).ToList();
            var sozGecikmisToplam = sozGecikmisList.Sum(s => s.TotalAmount ?? 0);
            var sozGecikmiAdet = sozGecikmisList.Count;

            // Ürün grubu kırılımı: SP fatura listesindeki FaturaNo'lar → urunGrupMap'ten kalem dağılımı
            var spFaturalar = await _cockpitData.GetFaturalarAsync(start, end);
            var spFaturaNoSet = new HashSet<string>(spFaturalar.Select(f => f.FaturaNo), StringComparer.OrdinalIgnoreCase);

            var urunKirilimDict = new Dictionary<string, (decimal toplam, int adet)>();
            foreach (var faturaNo in spFaturaNoSet)
            {
                if (urunGrupMap.TryGetValue(faturaNo, out var kalemler))
                {
                    foreach (var (grup, tlTutar) in kalemler)
                    {
                        if (urunKirilimDict.TryGetValue(grup, out var mevcut))
                            urunKirilimDict[grup] = (mevcut.toplam + tlTutar, mevcut.adet + 1);
                        else
                            urunKirilimDict[grup] = (tlTutar, 1);
                    }
                }
            }
            var urunKirilim = urunKirilimDict
                .Select(kv => new { grup = kv.Key, toplam = kv.Value.toplam, adet = kv.Value.adet })
                .OrderByDescending(x => x.toplam)
                .ToList();

            // ══════════════════════════════════════════════════════════════
            // AJAX: Sadece summary JSON döndür (detay listesi YOK)
            // ══════════════════════════════════════════════════════════════
            if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
            {
                return Json(new
                {
                    // Fatura kartı — SP'den
                    faturalarToplam = spFatura.Toplam,
                    faturalarAdet = spFatura.Adet,
                    varunaDisiToplam = m.VarunaDisiToplam,
                    varunaDisiAdet = m.VarunaDisiAdet,
                    // Tahsilat kartı — SP'den
                    tahsilatlarToplam = spTahsilat.BekleyenBakiyeToplam + spTahsilat.TahsilEdilen,
                    tahsilatEdilen = spTahsilat.TahsilEdilen,
                    tahsilatlarAdet = spTahsilat.TahsilAdet,
                    // Sözleşme kartı — SP'den
                    // Büyük tutar = gerçekleşen (Archived yeni sözleşme), payda = hedef (tüm yeni sözleşme)
                    sozlesmelerToplam = spSozlesme.YeniTutar,        // hedef = yeni sözleşmelerin tutarı
                    sozlesmelerAdet = spSozlesme.Toplam,             // toplam eski sözleşme sayısı
                    sozArchivedToplam = spSozlesme.ArchivedTutar,    // gerçekleşen = Archived yeni tutarı
                    sozArchivedAdet = spSozlesme.ArchivedAdet,       // Archived yeni sayısı
                    sozYenilenenAdet = spSozlesme.YenilenenAdet,     // tüm yenilenen sayısı
                    sozBekleyenTutar = spSozlesme.BekleyenTutar,     // yenilenmemiş eski tutar
                    sozBekleyenAdet = spSozlesme.BekleyenAdet,       // yenilenmemiş sayısı
                    sozEskiTutar = spSozlesme.EskiTutar,             // tüm eski sözleşme tutarı
                    urunKirilim,
                    faturalarTrend = m.PrevFatToplam > 0 ? Math.Round((m.FatToplam - m.PrevFatToplam) / m.PrevFatToplam * 100, 1) : 0,
                    tahsilatlarTrend = m.PrevTahToplam > 0 ? Math.Round((m.TahEdilen - m.PrevTahToplam) / m.PrevTahToplam * 100, 1) : 0,
                    prevFaturalarToplam = m.PrevFatToplam,
                    prevTahsilatlarToplam = m.PrevTahToplam,
                    // CEI
                    ceiDonemOran,
                    ceiDonemTahsilat,
                    ceiDonemVadesiGecmis = m.CeiDonemVgBakiye,
                    tahsilEdilecek,
                    tahsilKalan,
                    ceiHaftalikOran,
                    ceiHaftalikTahsilat = m.HaftalikTah,
                    ceiHaftalikToplam = haftalikPayda,
                    haftaBaslangicStr = haftaBaslangic.ToString("dd.MM"),
                    haftaSonuStr = haftaSonu.ToString("dd.MM.yyyy"),
                    ceiAylikOran,
                    ceiAylikTahsilat = m.AylikTah,
                    ceiAylikToplam = aylikPayda,
                    ceiYillikOran,
                    ceiYillikTahsilat = m.YtdTahToplam,
                    ceiYillikToplam = ytdPayda,
                    legacy2025Bakiye = m.Legacy2025Bakiye,
                    // Hedef
                    aylikHedef = donemHedef,
                    hedefGerceklesme = spFatura.Toplam,
                    hedefKalan,
                    hedefYuzde,
                    // Üst kartlar (DB bazlı)
                    fixedCurrentMonthTarget = fixedMonthTarget,
                    fixedCurrentMonthActual = m.FixedMonthActual,
                    fixedCurrentMonthPct = fixedMonthPct,
                    fixedYTDTarget = fixedYTDTarget,
                    fixedYTDActual = fixedYTDActual,
                    fixedYTDPct = fixedYTDPct,
                    fixedQuarterTarget,
                    currentQuarter,
                    // Vadesi geçmiş & beklenen
                    vadesiGecmisAlacak = m.VadesiGecmisAlacak,
                    vadesiGecmisAdet = m.VadesiGecmisAdet,
                    beklenenTahsilat = m.BeklenenTahsilat,
                    beklenenAdet = m.BeklenenAdet,
                    // Dönem/Geçmiş bakiye (NaN fix)
                    tahDonemBakiye = m.TahBakiye,
                    tahGecmisBakiye = m.VadesiGecmisAlacak,
                    tahGecmisAdet = m.VadesiGecmisAdet,
                    tahGecmisTahsilat = m.TahGecmisTahsilat,
                    // Filtre
                    filtreBaslangic = start.ToString("dd.MM.yyyy"),
                    filtreBitis = end.ToString("dd.MM.yyyy"),
                });
            }

            // ══════════════════════════════════════════════════════════════
            // Full page: Detay listelerini hazırla (sadece ilk yükleme)
            // ══════════════════════════════════════════════════════════════
            // Tüm faturalar listede görünür (iade/iptal dahil — UI'da rozetle ayırt edilir)
            var faturalar = allFaturalar
                .Where(f => f.EfektifFaturaTarihi.HasValue && f.EfektifFaturaTarihi.Value >= start && f.EfektifFaturaTarihi.Value <= end)
                .ToList();
            MapMusteriUrun(faturalar, urunMap, musteriMap);

            // Tahsilat listesi: Fatura_Vade_Tarihi dönemde olan faturalar, iade/ret hariç
            var tahsilatlar = allFaturalar
                .Where(f => !IsNegatifDurum(f.Durum) && !IsRetDurum(f.Durum))
                .Where(f => f.Fatura_Vade_Tarihi.HasValue)
                .Where(f => f.Fatura_Vade_Tarihi!.Value >= start && f.Fatura_Vade_Tarihi!.Value <= end)
                .ToList();
            MapMusteriUrun(tahsilatlar, urunMap, musteriMap);

            // Beklenen tahsilat: Fatura_Vade_Tarihi bugün → dönem sonu, bakiye > 0
            var beklenenList = allFaturalar
                .Where(f => {
                    if (!f.Fatura_Vade_Tarihi.HasValue) return false;
                    var bakiye = (f.Fatura_Toplam ?? 0) - (f.Tahsil_Edilen ?? 0);
                    return f.Fatura_Vade_Tarihi.Value > bugun && f.Fatura_Vade_Tarihi.Value <= donemSonu && bakiye > 0;
                })
                .ToList();
            MapMusteriUrun(beklenenList, urunMap, musteriMap);

            // Kümülatif hesaplama — iade/ret faturalar komple atlanır
            var orderedFaturalar = faturalar.OrderBy(f => f.EfektifFaturaTarihi).ToList();
            decimal running = 0;
            foreach (var f in orderedFaturalar)
            {
                if (!IsRetDurum(f.Durum) && !IsNegatifDurum(f.Durum))
                    running += f.NetTutar ?? 0m;
                f.KumulatifToplam = running;
            }

            var vm = new CockpitViewModel
            {
                FaturalarToplam = spFatura.Toplam,
                FaturalarAdet = spFatura.Adet,
                VarunaDisiToplam = m.VarunaDisiToplam,
                VarunaDisiAdet = m.VarunaDisiAdet,
                TahsilatlarToplam = spTahsilat.BekleyenBakiyeToplam + spTahsilat.TahsilEdilen,
                TahsilatlarAdet = spTahsilat.TahsilAdet,
                SozlesmelerToplam = spSozlesme.YeniTutar,
                SozlesmelerAdet = spSozlesme.Toplam,
                SozArchivedToplam = spSozlesme.ArchivedTutar,
                SozArchivedAdet = spSozlesme.ArchivedAdet,
                SozGecikmisToplam = spSozlesme.BekleyenTutar,
                SozGecikmiAdet = spSozlesme.BekleyenAdet,
                FaturalarTrend = m.PrevFatToplam > 0 ? Math.Round((m.FatToplam - m.PrevFatToplam) / m.PrevFatToplam * 100, 1) : 0,
                PrevFaturalarToplam = m.PrevFatToplam,
                PrevTahsilatlarToplam = m.PrevTahToplam,
                TahsilatlarTrend = m.PrevTahToplam > 0 ? Math.Round((m.TahEdilen - m.PrevTahToplam) / m.PrevTahToplam * 100, 1) : 0,
                SozlesmelerTrend = 0,
                AylikHedef = donemHedef,
                HedefTutar = donemHedef,
                HedefGerceklesme = spFatura.Toplam,
                HedefKalan = hedefKalan,
                HedefYuzde = hedefYuzde,
                HedefAySayisi = months,
                YtdHedef = ytdHedef,
                YtdGerceklesme = m.YtdFatGerceklesme,
                YtdKalan = ytdKalan,
                YtdYuzde = ytdHedef > 0 ? Math.Round(Math.Min(m.YtdFatGerceklesme / ytdHedef * 100, 100), 1) : 0,
                AktifFiltre = activeFilter,
                FiltreBaslangic = start,
                FiltreBitis = end,
                FaturaDetaylari = orderedFaturalar,
                TahsilatDetaylari = tahsilatlar.OrderByDescending(f => f.Fatura_Vade_Tarihi).ToList(),
                SozlesmeDetaylari = sozDonem.OrderByDescending(s => s.TotalAmount).ToList(),
                TahsilEdilecek = tahsilEdilecek,
                TahsilatEdilen = m.TahEdilen,
                TahsilKalan = tahsilKalan,
                CeiDonemTahsilat = ceiDonemTahsilat,
                CeiDonemVadesiGecmis = m.CeiDonemVgBakiye,
                CeiDonemOran = ceiDonemOran,
                CeiHaftalikTahsilat = m.HaftalikTah,
                CeiHaftalikToplam = haftalikPayda,
                CeiHaftalikOran = ceiHaftalikOran,
                HaftaBaslangic = haftaBaslangic,
                HaftaSonu = haftaSonu,
                CeiAylikTahsilat = m.AylikTah,
                CeiAylikToplam = aylikPayda,
                CeiAylikOran = ceiAylikOran,
                CeiYillikTahsilat = m.YtdTahToplam,
                CeiYillikToplam = ytdPayda,
                CeiYillikOran = ceiYillikOran,
                Legacy2025Bakiye = m.Legacy2025Bakiye,
                BeklenenTahsilat = m.BeklenenTahsilat,
                BeklenenAdet = m.BeklenenAdet,
                BeklenenDetaylari = beklenenList.OrderBy(f => f.Fatura_Vade_Tarihi).ToList(),
                VadesiGecmisAlacak = m.VadesiGecmisAlacak,
                VadesiGecmisAdet = m.VadesiGecmisAdet,
                TahDonemBakiye = m.TahBakiye,
                TahGecmisTahsilat = m.TahGecmisTahsilat,
                TahGecmisBakiye = m.VadesiGecmisAlacak,
                TahGecmisAdet = m.VadesiGecmisAdet,
                FixedCurrentMonthTarget = fixedMonthTarget,
                FixedCurrentMonthActual = m.FixedMonthActual,
                FixedCurrentMonthPct = fixedMonthPct,
                FixedYTDTarget = fixedYTDTarget,
                FixedYTDActual = m.FixedYTDActual,
                FixedYTDPct = fixedYTDPct,
                FixedQuarterTarget = fixedQuarterTarget,
                RemainingMonths = remainingMonths,
                CurrentQuarter = currentQuarter
            };

            return View(vm);
        }

        /// <summary>
        /// Hiyerarşik detay: Yıl → Ay → Hafta → Gün → Fatura detayları
        /// Tahsilat: + haftalık alınması gereken vs alınan
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> GetDetailTable(string type, string? filter, string? startDate, string? endDate, int page = 1, int pageSize = 50)
        {
            var (start, end, activeFilter, _) = ParseFilter(filter, startDate, endDate);
            var (allFaturalar, urunMap, musteriMap, sozlesmeler, hedefler, varunaTutarMap, urunGrupMap) = await LoadAllCachedDataAsync(_contextFactory, _cache);
            var bugun = DateTime.Now.Date;

            // Hangi seviyeden başla: month/lastmonth → ay, q1-q4 → çeyrek, ytd/range → yıl
            var startLevel = activeFilter switch
            {
                "month" or "lastmonth" => "ay",
                "q1" or "q2" or "q3" or "q4" => "ceyrek",
                _ => "yil"
            };

            // ISO week hesaplama
            static int GetIsoWeek(DateTime d) => System.Globalization.ISOWeek.GetWeekOfYear(d);

            switch (type?.ToLowerInvariant())
            {
                case "faturalar":
                {
                    // SP fatura listesiyle senkron: sadece SP'de olan FaturaNo'lar
                    var spFatList = await _cockpitData.GetFaturalarAsync(start, end);
                    var spFatNoSet = new HashSet<string>(spFatList.Select(f => f.FaturaNo), StringComparer.OrdinalIgnoreCase);

                    var filtered = allFaturalar
                        .Where(f => f.Fatura_No != null && spFatNoSet.Contains(f.Fatura_No))
                        .OrderBy(f => f.EfektifFaturaTarihi)
                        .ToList();
                    MapMusteriUrun(filtered, urunMap, musteriMap);

                    // Kümülatif
                    decimal running = 0;
                    foreach (var f in filtered)
                    {
                        running += f.NetTutar ?? 0m;
                        f.KumulatifToplam = running;
                    }

                    // Net tutar helper (Varuna KDV hariç bazlı)
                    decimal FatNet(IEnumerable<VIEW_CP_EXCEL_FATURA> grp) =>
                        grp.Sum(f => f.NetTutar ?? 0m);
                    // KDV dahil toplam helper
                    decimal FatBrut(IEnumerable<VIEW_CP_EXCEL_FATURA> grp) =>
                        grp.Sum(f => f.KdvDahilTutar ?? 0m);

                    // Hiyerarşi: Yıl → Çeyrek → Ay → Hafta → Gün → Detay
                    var hierarchy = filtered
                        .GroupBy(f => f.EfektifFaturaTarihi!.Value.Year)
                        .OrderBy(y => y.Key)
                        .Select(yGrp => new
                        {
                            yil = yGrp.Key,
                            toplam = FatNet(yGrp),
                            kdvDahilToplam = FatBrut(yGrp),
                            adet = yGrp.Count(),
                            ceyrekler = yGrp
                                .GroupBy(f => (f.EfektifFaturaTarihi!.Value.Month - 1) / 3 + 1)
                                .OrderBy(q => q.Key)
                                .Select(qGrp => new
                                {
                                    ceyrek = qGrp.Key,
                                    label = qGrp.Key + ". Çeyrek",
                                    toplam = FatNet(qGrp),
                                    kdvDahilToplam = FatBrut(qGrp),
                                    adet = qGrp.Count(),
                                    aylar = qGrp
                                        .GroupBy(f => f.EfektifFaturaTarihi!.Value.Month)
                                        .OrderBy(m => m.Key)
                                        .Select(mGrp => new
                                        {
                                            ay = mGrp.Key,
                                            ayAdi = new DateTime(yGrp.Key, mGrp.Key, 1).ToString("MMMM", new System.Globalization.CultureInfo("tr-TR")),
                                            toplam = FatNet(mGrp),
                                            kdvDahilToplam = FatBrut(mGrp),
                                            adet = mGrp.Count(),
                                            haftalar = mGrp
                                                .GroupBy(f => GetIsoWeek(f.EfektifFaturaTarihi!.Value))
                                                .OrderBy(w => w.Key)
                                                .Select(wGrp => new
                                                {
                                                    hafta = wGrp.Key,
                                                    toplam = FatNet(wGrp),
                                                    kdvDahilToplam = FatBrut(wGrp),
                                                    adet = wGrp.Count(),
                                                    gunler = wGrp
                                                        .GroupBy(f => f.EfektifFaturaTarihi!.Value.Date)
                                                        .OrderBy(d => d.Key)
                                                        .Select(dGrp => new
                                                        {
                                                            tarih = dGrp.Key.ToString("dd.MM.yyyy"),
                                                            toplam = FatNet(dGrp),
                                                            kdvDahilToplam = FatBrut(dGrp),
                                                            adet = dGrp.Count(),
                                                            faturalar = dGrp.Select(f => new
                                                            {
                                                                faturaNo = f.Fatura_No,
                                                                musteri = f.MusteriUnvan,
                                                                tutar = (f.NetTutar ?? 0m),
                                                                kdvDahilTutar = (f.KdvDahilTutar ?? 0m),
                                                                kumulatif = f.KumulatifToplam,
                                                                durum = f.Durum?.Trim()?.ToUpper()
                                                            })
                                                        })
                                                })
                                        })
                                })
                        });

                    return Json(new { total = filtered.Count, dipToplam = running, startLevel, hierarchy });
                }
                case "tahsilatlar":
                {
                    // SP_COCKPIT_TAHSILAT ile aynı mantık:
                    // Direkt VIEW'den, Fatura_No dedupe, İade/Ret hariç
                    using var dbTah = _contextFactory.CreateDbContext();
                    var viewFaturalar = (await dbTah.VIEW_CP_EXCEL_FATURAs.AsNoTracking().ToListAsync())
                        .GroupBy(f => f.Fatura_No ?? f.GetHashCode().ToString())
                        .Select(g => g.First())
                        .Where(f => {
                            var d2 = (f.Durum ?? "").Trim();
                            return d2 != "İADE" && d2 != "IADE" && d2 != "İPTAL" && d2 != "IPTAL" && d2 != "RET";
                        })
                        .ToList();

                    // Vade tarihi dönemde olan faturalar — hiyerarşi Fatura_Vade_Tarihi bazlı
                    var vadeDonemdeFaturalar = viewFaturalar
                        .Where(f => f.Fatura_Vade_Tarihi.HasValue
                            && f.Fatura_Vade_Tarihi.Value >= start && f.Fatura_Vade_Tarihi.Value <= end)
                        .ToList();

                    // Tahsil edilenler: Tahsil_Edilen > 0
                    var tahsilEdilenler = vadeDonemdeFaturalar
                        .Where(f => (f.Tahsil_Edilen ?? 0) > 0)
                        .Select(f => new { fatura = f, vadeTarih = f.Fatura_Vade_Tarihi!.Value, odendi = true })
                        .ToList();

                    // Bekleyen bakiye: bakiye > 0
                    var bekleyenler = vadeDonemdeFaturalar
                        .Where(f => (f.Bekleyen_Bakiye ?? ((f.Fatura_Toplam ?? 0) - (f.Tahsil_Edilen ?? 0))) > 0
                            && !tahsilEdilenler.Any(t => t.fatura.Fatura_No == f.Fatura_No))
                        .Select(f => new { fatura = f, vadeTarih = f.Fatura_Vade_Tarihi!.Value, odendi = false })
                        .ToList();

                    var combined = tahsilEdilenler.Concat(bekleyenler)
                        .OrderBy(x => x.vadeTarih)
                        .ToList();

                    // Müşteri bilgisi: VIEW'den Ilgili_Kisi veya Varuna AccountTitle
                    var filteredFaturalar = combined.Select(x => x.fatura).ToList();
                    MapMusteriUrun(filteredFaturalar, urunMap, musteriMap);
                    // Ilgili_Kisi fallback
                    foreach (var f in filteredFaturalar)
                        if (string.IsNullOrEmpty(f.MusteriUnvan)) f.MusteriUnvan = f.Ilgili_Kisi;

                    // Haftalık hedef hesapla
                    var allOpenInvoices = allFaturalar
                        .Where(f => IsDurumBos(f.Durum) && (f.Bekleyen_Bakiye ?? (f.Fatura_Toplam ?? 0) - (f.Tahsil_Edilen ?? 0)) > 0)
                        .ToList();

                    // dipToplam SP'den — kart ile tutarlı
                    var spTahDip = await _cockpitData.GetTahsilatOzetAsync(start, end);
                    var dipToplam = spTahDip.TahsilEdilen;

                    var hierarchy = combined
                        .GroupBy(x => x.vadeTarih.Year)
                        .OrderBy(y => y.Key)
                        .Select(yGrp => new
                        {
                            yil = yGrp.Key,
                            toplam = yGrp.Where(x => x.odendi).Sum(x => x.fatura.Tahsil_Edilen ?? 0),
                            adet = yGrp.Count(),
                            ceyrekler = yGrp
                                .GroupBy(x => (x.vadeTarih.Month - 1) / 3 + 1)
                                .OrderBy(q => q.Key)
                                .Select(qGrp => new
                                {
                                    ceyrek = qGrp.Key,
                                    label = qGrp.Key + ". Çeyrek",
                                    toplam = qGrp.Where(x => x.odendi).Sum(x => x.fatura.Tahsil_Edilen ?? 0),
                                    adet = qGrp.Count(),
                                    aylar = qGrp
                                        .GroupBy(x => x.vadeTarih.Month)
                                        .OrderBy(m => m.Key)
                                        .Select(mGrp => new
                                        {
                                            ay = mGrp.Key,
                                            ayAdi = new DateTime(yGrp.Key, mGrp.Key, 1).ToString("MMMM", new System.Globalization.CultureInfo("tr-TR")),
                                            toplam = mGrp.Where(x => x.odendi).Sum(x => x.fatura.Tahsil_Edilen ?? 0),
                                            adet = mGrp.Count(),
                                            haftalar = mGrp
                                                .GroupBy(x => GetIsoWeek(x.vadeTarih))
                                                .OrderBy(w => w.Key)
                                                .Select(wGrp =>
                                                {
                                                    var anyDate = wGrp.First().vadeTarih;
                                                    var wStart = System.Globalization.ISOWeek.ToDateTime(anyDate.Year, wGrp.Key, DayOfWeek.Monday);
                                                    var wEnd = wStart.AddDays(6);
                                                    var haftaHedef = allOpenInvoices
                                                        .Where(inv => inv.Fatura_Vade_Tarihi.HasValue
                                                            && inv.Fatura_Vade_Tarihi.Value.Date >= wStart
                                                            && inv.Fatura_Vade_Tarihi.Value.Date <= wEnd)
                                                        .Sum(inv => inv.Bekleyen_Bakiye ?? ((inv.Fatura_Toplam ?? 0) - (inv.Tahsil_Edilen ?? 0)));
                                                    var alinan = wGrp.Where(x => x.odendi).Sum(x => x.fatura.Tahsil_Edilen ?? 0);

                                                    return new
                                                    {
                                                        hafta = wGrp.Key,
                                                        alinan,
                                                        alinmasiGereken = haftaHedef,
                                                        adet = wGrp.Count(),
                                                        gunler = wGrp
                                                            .GroupBy(x => x.vadeTarih.Date)
                                                            .OrderBy(d => d.Key)
                                                            .Select(dGrp => new
                                                            {
                                                                tarih = dGrp.Key.ToString("dd.MM.yyyy"),
                                                                toplam = dGrp.Where(x => x.odendi).Sum(x => x.fatura.Tahsil_Edilen ?? 0),
                                                                adet = dGrp.Count(),
                                                                faturalar = dGrp.Select(x => new
                                                                {
                                                                    faturaNo = x.fatura.Fatura_No,
                                                                    musteri = x.fatura.MusteriUnvan ?? x.fatura.Ilgili_Kisi,
                                                                    tahsilEdilen = x.fatura.Tahsil_Edilen ?? 0,
                                                                    bakiye = x.fatura.Bekleyen_Bakiye ?? ((x.fatura.Fatura_Toplam ?? 0) - (x.fatura.Tahsil_Edilen ?? 0)),
                                                                    tutar = x.odendi ? (x.fatura.Tahsil_Edilen ?? 0) : (x.fatura.Bekleyen_Bakiye ?? ((x.fatura.Fatura_Toplam ?? 0) - (x.fatura.Tahsil_Edilen ?? 0))),
                                                                    tarih = x.odendi ? x.fatura.Tahsil_Tarihi?.ToString("dd.MM.yyyy") : x.fatura.Fatura_Vade_Tarihi?.ToString("dd.MM.yyyy"),
                                                                    durum = x.fatura.Durum?.Trim()?.ToUpper(),
                                                                    odendi = x.odendi
                                                                })
                                                            })
                                                    };
                                                })
                                        })
                                })
                        });

                    return Json(new { total = combined.Count, dipToplam, startLevel, hierarchy });
                }
                case "sozlesmeler":
                {
                    // SP'den — FinishDate+1 bazlı, RelatedContractId ile yeni sözleşme
                    var spSozList = await _cockpitData.GetSozlesmelerAsync(start, end);
                    var spOzet = await _cockpitData.GetSozlesmeOzetAsync(start, end);

                    var hierarchy = spSozList
                        .GroupBy(s => (s.Yenilemetarihi ?? s.EskiBitis ?? DateTime.Now).Year)
                        .OrderBy(y => y.Key)
                        .Select(yGrp => new
                        {
                            yil = yGrp.Key,
                            toplam = yGrp.Sum(s => s.EskiTutar ?? 0),
                            adet = yGrp.Count(),
                            archivedAdet = yGrp.Count(s => s.Yenilendi == 1 && string.Equals(s.YeniStatus, "Archived", StringComparison.OrdinalIgnoreCase)),
                            ceyrekler = yGrp
                                .GroupBy(s => ((s.Yenilemetarihi ?? s.EskiBitis ?? DateTime.Now).Month - 1) / 3 + 1)
                                .OrderBy(q => q.Key)
                                .Select(qGrp => new
                                {
                                    ceyrek = qGrp.Key,
                                    label = qGrp.Key + ". Çeyrek",
                                    toplam = qGrp.Sum(s => s.EskiTutar ?? 0),
                                    adet = qGrp.Count(),
                                    archivedAdet = qGrp.Count(s => s.Yenilendi == 1 && string.Equals(s.YeniStatus, "Archived", StringComparison.OrdinalIgnoreCase)),
                                    aylar = qGrp
                                        .GroupBy(s => (s.Yenilemetarihi ?? s.EskiBitis ?? DateTime.Now).Month)
                                        .OrderBy(m => m.Key)
                                        .Select(mGrp => new
                                        {
                                            ay = mGrp.Key,
                                            ayAdi = new DateTime(yGrp.Key, mGrp.Key, 1).ToString("MMMM", new System.Globalization.CultureInfo("tr-TR")),
                                            toplam = mGrp.Sum(s => s.EskiTutar ?? 0),
                                            adet = mGrp.Count(),
                                            archivedAdet = mGrp.Count(s => s.Yenilendi == 1 && string.Equals(s.YeniStatus, "Archived", StringComparison.OrdinalIgnoreCase)),
                                            sozlesmeler = mGrp.OrderByDescending(s => s.EskiTutar).Select(s => new
                                            {
                                                musteri = s.Firma,
                                                baslangic = s.EskiBitis?.AddYears(-1).ToString("dd.MM.yyyy"),
                                                bitis = s.EskiBitis?.ToString("dd.MM.yyyy"),
                                                yenileme = s.Yenilemetarihi?.ToString("dd.MM.yyyy"),
                                                tutar = s.EskiTutar ?? 0,
                                                durum = s.ContractStatus,
                                                yenilendi = s.Yenilendi == 1,
                                                yeniTutar = s.YeniTutar,
                                                yeniDurum = s.YeniStatus,
                                                yeniBitis = s.YeniBitis?.ToString("dd.MM.yyyy")
                                            })
                                        })
                                })
                        });

                    return Json(new {
                        total = spOzet.Toplam,
                        dipToplam = spOzet.YeniTutar,
                        archivedToplam = spOzet.ArchivedTutar,
                        startLevel,
                        hierarchy
                    });
                }
                default:
                    return BadRequest(new { error = "Geçersiz tip" });
            }
        }

        [HttpGet]
        public async Task<IActionResult> GetDailyBreakdown(string type, string? filter, string? startDate, string? endDate)
        {
            var (start, end, _, _) = ParseFilter(filter, startDate, endDate);
            var (allFaturalar, _, _, sozlesmeler, _, _, _) = await LoadAllCachedDataAsync(_contextFactory, _cache);

            switch (type?.ToLowerInvariant())
            {
                case "faturalar":
                {
                    // İade/Ret tamamen atlanır
                    var daily = allFaturalar
                        .Where(f => f.EfektifFaturaTarihi.HasValue && f.EfektifFaturaTarihi.Value >= start && f.EfektifFaturaTarihi.Value <= end
                            && !IsRetDurum(f.Durum) && !IsNegatifDurum(f.Durum))
                        .GroupBy(f => f.EfektifFaturaTarihi!.Value.Date)
                        .Select(g => new
                        {
                            tarih = g.Key.ToString("yyyy-MM-dd"),
                            toplam = g.Sum(x => x.NetTutar ?? 0),
                            adet = g.Count()
                        })
                        .OrderBy(x => x.tarih).ToList();
                    return Json(daily);
                }
                case "tahsilatlar":
                {
                    var daily = allFaturalar
                        .Where(f => f.Fatura_Vade_Tarihi.HasValue && IsTahsilatOrKrediKarti(f.Durum))
                        .Where(f => f.Fatura_Vade_Tarihi!.Value >= start && f.Fatura_Vade_Tarihi!.Value <= end)
                        .GroupBy(f => f.Fatura_Vade_Tarihi!.Value.Date)
                        .Select(g => new { tarih = g.Key.ToString("yyyy-MM-dd"), toplam = g.Sum(x => x.Fatura_Toplam ?? 0), adet = g.Count() })
                        .OrderBy(x => x.tarih).ToList();
                    return Json(daily);
                }
                case "sozlesmeler":
                {
                    var daily = sozlesmeler.Where(s => s.CreatedOn.HasValue)
                        .GroupBy(s => s.CreatedOn!.Value.Date)
                        .Select(g => new { tarih = g.Key.ToString("yyyy-MM-dd"), toplam = g.Sum(x => x.TotalAmount ?? 0), adet = g.Count() })
                        .OrderBy(x => x.tarih).ToList();
                    return Json(daily);
                }
                default:
                    return BadRequest(new { error = "Geçersiz tip" });
            }
        }

        [HttpGet]
        public async Task<IActionResult> GetKalemDetay(string faturaNo)
        {
            if (string.IsNullOrEmpty(faturaNo))
                return BadRequest(new { error = "Fatura no gerekli" });

            using var db = _contextFactory.CreateDbContext();

            // Fatura_No → SerialNumber → OrderId + TotalNetAmount (TL bazlı)
            var siparis = await db.TBL_VARUNA_SIPARIs
                .AsNoTracking()
                .Where(s => s.SerialNumber == faturaNo)
                .Select(s => new { s.OrderId, s.TotalNetAmount })
                .FirstOrDefaultAsync();

            if (siparis?.OrderId == null)
                return Json(new List<object>());

            var kalemler = await db.TBL_VARUNA_SIPARIS_URUNLERIs
                .AsNoTracking()
                .Where(u => u.CrmOrderId == siparis.OrderId)
                .Select(u => new
                {
                    UrunAdi = u.ProductName,
                    StokKodu = u.StockCode,
                    Miktar = u.Quantity,
                    BirimFiyat = u.UnitPrice,
                    DovizToplam = u.Total,
                    KDV = u.Tax
                })
                .ToListAsync();

            // Kalem bazlı TL dağılımı: s.TotalNetAmount'ı kalemlerin döviz oranına göre dağıt
            var dovizGenel = kalemler.Sum(k => k.DovizToplam ?? 0);
            var tlNet = siparis.TotalNetAmount ?? 0;

            var urunler = kalemler.Select(k =>
            {
                var oran = dovizGenel != 0 ? (k.DovizToplam ?? 0) / dovizGenel : 0;
                var tlToplam = tlNet * oran;
                return new
                {
                    k.UrunAdi,
                    k.StokKodu,
                    k.Miktar,
                    BirimFiyat = k.Miktar > 0 ? tlToplam / (decimal)k.Miktar : 0,
                    Toplam = tlToplam,
                    k.KDV
                };
            }).ToList();

            return Json(urunler);
        }

        [HttpGet]
        public async Task<IActionResult> TahsilatCheck(string? filter)
        {
            var (allFaturalar, _, _, _, _, _, _) = await LoadAllCachedDataAsync(_contextFactory, _cache);
            var (start, end, activeFilter, _) = ParseFilter(filter, null, null);

            // Ortak filtre: Fatura_Vade_Tarihi bazlı
            var donemFaturalar = allFaturalar
                .Where(f => f.Fatura_Vade_Tarihi.HasValue)
                .Where(f => f.Fatura_Vade_Tarihi!.Value >= start && f.Fatura_Vade_Tarihi!.Value <= end)
                .Where(f => !new[] { "İADE","IADE","İPTAL","IPTAL","RET" }.Contains(f.Durum?.Trim() ?? "", StringComparer.OrdinalIgnoreCase))
                .ToList();

            // YTD ayrıca
            var ytdStart = new DateTime(DateTime.Now.Year, 1, 1);
            var ytdEnd = DateTime.Now.Date.AddDays(1).AddSeconds(-1);
            var ytdFaturalar = allFaturalar
                .Where(f => f.Fatura_Vade_Tarihi.HasValue)
                .Where(f => f.Fatura_Vade_Tarihi!.Value >= ytdStart && f.Fatura_Vade_Tarihi!.Value <= ytdEnd)
                .Where(f => !new[] { "İADE","IADE","İPTAL","IPTAL","RET" }.Contains(f.Durum?.Trim() ?? "", StringComparer.OrdinalIgnoreCase))
                .ToList();

            // PAYDA alternatif: Fatura_Tarihi bazlı (dönemde kesilen faturalar)
            var faturaTarihiBazli = allFaturalar
                .Where(f => f.EfektifFaturaTarihi.HasValue && f.EfektifFaturaTarihi.Value >= start && f.EfektifFaturaTarihi.Value <= end)
                .Where(f => !new[] { "İADE","IADE","İPTAL","IPTAL","RET" }.Contains(f.Durum?.Trim() ?? "", StringComparer.OrdinalIgnoreCase))
                .ToList();

            var ytdFaturaTarihiBazli = allFaturalar
                .Where(f => f.EfektifFaturaTarihi.HasValue && f.EfektifFaturaTarihi.Value >= ytdStart && f.EfektifFaturaTarihi.Value <= ytdEnd)
                .Where(f => !new[] { "İADE","IADE","İPTAL","IPTAL","RET" }.Contains(f.Durum?.Trim() ?? "", StringComparer.OrdinalIgnoreCase))
                .ToList();

            return Json(new {
                filtre = activeFilter,
                donem = start.ToString("dd.MM.yyyy") + " - " + end.ToString("dd.MM.yyyy"),
                mevcutHesap_VadeTarihi = new {
                    pay = donemFaturalar.Sum(f => f.Tahsil_Edilen ?? 0),
                    payda = donemFaturalar.Sum(f => (f.KdvDahilTutar ?? 0m)),
                    adet = donemFaturalar.Count
                },
                dogruHesap_FaturaTarihi = new {
                    pay = faturaTarihiBazli.Sum(f => f.Tahsil_Edilen ?? 0),
                    payda = faturaTarihiBazli.Sum(f => (f.KdvDahilTutar ?? 0m)),
                    adet = faturaTarihiBazli.Count
                },
                ytdMevcut = new {
                    pay = ytdFaturalar.Sum(f => f.Tahsil_Edilen ?? 0),
                    payda = ytdFaturalar.Sum(f => (f.KdvDahilTutar ?? 0m)),
                    adet = ytdFaturalar.Count
                },
                ytdDogru = new {
                    pay = ytdFaturaTarihiBazli.Sum(f => f.Tahsil_Edilen ?? 0),
                    payda = ytdFaturaTarihiBazli.Sum(f => (f.KdvDahilTutar ?? 0m)),
                    adet = ytdFaturaTarihiBazli.Count
                }
            });
        }

        /* VarunaCheck endpoint kaldırıldı */
        /*
        public async Task<IActionResult> VarunaCheck_REMOVED()
        {
            var (allFaturalar, _, _, _, _, varunaTutarMap, _) = await LoadAllCachedDataAsync(_contextFactory, _cache);
            using var db = _contextFactory.CreateDbContext();

            var statuses = await db.TBL_VARUNA_SIPARIs.Select(s => s.OrderStatus).Distinct().ToListAsync();
            var closedCount = await db.TBL_VARUNA_SIPARIs.CountAsync(s => s.OrderStatus == "Closed");
            var closedWithNet = await db.TBL_VARUNA_SIPARIs.CountAsync(s => s.OrderStatus == "Closed" && s.TotalNetAmount != null && s.TotalNetAmount > 0 && s.SerialNumber != null);

            // Fatura eşleşme
            var faturaNoSet = allFaturalar.Where(f => f.Fatura_No != null).Select(f => f.Fatura_No!).Distinct().ToList();
            var matchedCount = varunaTutarMap.Keys.Count(k => faturaNoSet.Contains(k));

            // Örnek eşleşmeler
            var samples = allFaturalar
                .Where(f => f.Fatura_No != null && varunaTutarMap.ContainsKey(f.Fatura_No))
                .Take(10)
                .Select(f => new {
                    f.Fatura_No,
                    ExcelTutar = f.Fatura_Toplam,
                    VarunaNetTutar = varunaTutarMap[f.Fatura_No!],
                    Fark = (f.Fatura_Toplam ?? 0) - varunaTutarMap[f.Fatura_No!]
                }).ToList();

            // ComputeMetrics ile aynı parametreleri kullanarak test
            var now = DateTime.Now;
            var bugun = now.Date;
            var today = bugun.AddDays(1).AddSeconds(-1);
            var ytdStart = new DateTime(now.Year, 1, 1);

            // YTD faturalar: Varuna'lı vs Varuna'sız karşılaştırma
            var ytdFaturalar = allFaturalar
                .Where(f => f.EfektifFaturaTarihi.HasValue && f.EfektifFaturaTarihi.Value >= ytdStart && f.EfektifFaturaTarihi.Value <= today)
                .ToList();

            decimal ytdExcelToplam = 0, ytdVarunaToplam = 0;
            int varunaKullanilan = 0, excelKullanilan = 0;
            foreach (var f in ytdFaturalar)
            {
                if (IsRetDurum(f.Durum) || IsNegatifDurum(f.Durum)) continue;
                var excelTutar = f.Fatura_Toplam ?? 0;
                decimal vt2 = 0;
                var varunaVar = f.Fatura_No != null && varunaTutarMap.TryGetValue(f.Fatura_No, out vt2);
                var tutar = varunaVar ? vt2 : excelTutar;
                ytdVarunaToplam += tutar;
                ytdExcelToplam += excelTutar;
                if (varunaVar) varunaKullanilan++; else excelKullanilan++;
            }

            return Json(new {
                orderStatuses = statuses,
                closedSiparis = closedCount,
                closedWithTotalNetAmount = closedWithNet,
                varunaMapSize = varunaTutarMap.Count,
                faturaCount = faturaNoSet.Count,
                eslesen = matchedCount,
                ytd = new {
                    excelToplam = ytdExcelToplam,
                    varunaToplam = ytdVarunaToplam,
                    fark = ytdExcelToplam - ytdVarunaToplam,
                    varunaKullanilan,
                    excelKullanilan,
                    toplamFatura = ytdFaturalar.Count
                },
                ornekler = samples
            });
        }
        */

        // ═══ DEBUG: Departman ve Ürün listesi + StockCode eşleşme ═══
        [HttpGet]
        public async Task<IActionResult> GetDepartmanUrunList()
        {
            using var db = _contextFactory.CreateDbContext();

            // 1) Faturalardaki benzersiz Proje (departman) isimleri
            var projeler = await db.VIEW_CP_EXCEL_FATURAs
                .AsNoTracking()
                .Where(f => f.Proje != null && f.Proje != "")
                .Select(f => f.Proje!)
                .Distinct()
                .OrderBy(p => p)
                .ToListAsync();

            // 2) VIEW_ORTAK_PROJE_ISIMLERI (master departman listesi)
            var ortakProjeler = await db.VIEW_ORTAK_PROJE_ISIMLERIs
                .AsNoTracking()
                .Where(p => p.TXTORTAKPROJEADI != null)
                .Select(p => new { p.LNGKOD, p.TXTORTAKPROJEADI, p.DURUM })
                .OrderBy(p => p.TXTORTAKPROJEADI)
                .ToListAsync();

            // 3) Ürün grupları
            var urunGruplari = await db.TBL_VARUNA_URUN_GRUPLAMAs
                .AsNoTracking()
                .Select(u => new { u.LNGKOD, u.TXTURUNGRUP, u.TXTURUNMASK, u.TXTKOD })
                .OrderBy(u => u.TXTURUNGRUP)
                .ToListAsync();

            // 4) Sipariş ürünleri (StockCode + ProductName)
            var siparisUrunleri = await db.TBL_VARUNA_SIPARIS_URUNLERIs
                .AsNoTracking()
                .Where(u => u.StockCode != null && u.StockCode != "")
                .Select(u => new { u.StockCode, u.ProductName })
                .Distinct()
                .OrderBy(u => u.StockCode)
                .ToListAsync();

            // 5) StockCode → Ürün grubu mask eşleştirme
            var gruplama = urunGruplari
                .Where(g => g.TXTURUNMASK != null && g.TXTURUNMASK != "")
                .OrderByDescending(g => (g.TXTURUNMASK ?? "").Length)
                .ToList();

            var stockMapping = siparisUrunleri.Select(u =>
            {
                var match = gruplama.FirstOrDefault(g =>
                    u.StockCode!.Trim().StartsWith(g.TXTURUNMASK!.Trim(), StringComparison.OrdinalIgnoreCase));
                return new
                {
                    StockCode = (u.StockCode ?? "").Trim(),
                    ProductName = (u.ProductName ?? "").Trim(),
                    MatchedMask = match?.TXTURUNMASK?.Trim(),
                    UrunGrubu = match?.TXTURUNGRUP?.Trim(),
                    Matched = match != null
                };
            }).ToList();

            return Json(new
            {
                faturaDepartmanlari = projeler,
                masterDepartmanlar = ortakProjeler,
                urunGruplari = urunGruplari,
                stockMapping = new
                {
                    toplam = stockMapping.Count,
                    eslesenAdet = stockMapping.Count(s => s.Matched),
                    eslesmeyenAdet = stockMapping.Count(s => !s.Matched),
                    eslesen = stockMapping.Where(s => s.Matched)
                        .GroupBy(s => s.UrunGrubu)
                        .Select(g => new
                        {
                            urunGrubu = g.Key,
                            adet = g.Count(),
                            ornekler = g.Take(5).Select(x => new { x.StockCode, x.ProductName, x.MatchedMask })
                        }),
                    eslesmeyen = stockMapping.Where(s => !s.Matched)
                        .Select(x => new { x.StockCode, x.ProductName })
                }
            });
        }

        [HttpGet]
        public async Task<IActionResult> GetUrunKategoriExcel()
        {
            using var db = _contextFactory.CreateDbContext();

            var siparisUrunleri = await db.TBL_VARUNA_SIPARIS_URUNLERIs
                .AsNoTracking()
                .Where(u => u.StockCode != null && u.StockCode != "")
                .Select(u => new { StockCode = u.StockCode!.Trim(), ProductName = (u.ProductName ?? "").Trim() })
                .Distinct()
                .OrderBy(u => u.StockCode)
                .ToListAsync();

            var gruplama = await db.TBL_VARUNA_URUN_GRUPLAMAs
                .AsNoTracking()
                .Where(g => g.TXTURUNMASK != null && g.TXTURUNMASK != "")
                .OrderByDescending(g => g.TXTURUNMASK!.Length)
                .ToListAsync();

            // Güncel kategori map (UH → E-Dönüşüm)
            var kategoriMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["EY"] = "Enroute", ["EH"] = "Enroute", ["EYS"] = "Enroute",
                ["SH"] = "Stokbar", ["SH.01"] = "Stokbar", ["SY"] = "Stokbar",
                ["QY"] = "Quest", ["QH"] = "Quest", ["QMH"] = "Quest", ["QYS"] = "Quest",
                ["CDY"] = "ServiceCore", ["CDH"] = "ServiceCore",
                ["VY"] = "Varuna", ["VH"] = "Varuna",
                ["OH"] = "Hosting", ["WPH"] = "Hosting", ["WPY"] = "Hosting",
                ["SM"] = "E-Dönüşüm", ["SMY"] = "E-Dönüşüm", ["SMH"] = "E-Dönüşüm", ["UH"] = "E-Dönüşüm",
                ["PP"] = "BFG",
                ["zzzUH"] = "E-Dönüşüm",
            };

            var rows = siparisUrunleri.Select(u =>
            {
                var maskMatch = gruplama.FirstOrDefault(g =>
                    u.StockCode.StartsWith(g.TXTURUNMASK!.Trim(), StringComparison.OrdinalIgnoreCase));
                var mask = maskMatch?.TXTURUNMASK?.Trim() ?? "";
                var grupTip = maskMatch?.TXTURUNGRUP?.Trim() ?? "";
                kategoriMap.TryGetValue(mask, out var anaUrun);
                return new { AnaUrun = anaUrun ?? "Eşleşmedi", Mask = mask, YazilimHizmet = grupTip, StokKodu = u.StockCode, UrunAdi = u.ProductName };
            })
            .OrderBy(r => r.AnaUrun).ThenBy(r => r.Mask).ThenBy(r => r.StokKodu)
            .ToList();

            // CSV üret
            var sb = new System.Text.StringBuilder();
            sb.AppendLine("Ana Ürün\tMask\tYazılım/Hizmet\tStok Kodu\tÜrün Açıklaması");
            foreach (var r in rows)
                sb.AppendLine($"{r.AnaUrun}\t{r.Mask}\t{r.YazilimHizmet}\t{r.StokKodu}\t{r.UrunAdi}");

            var bytes = System.Text.Encoding.UTF8.GetPreamble().Concat(System.Text.Encoding.UTF8.GetBytes(sb.ToString())).ToArray();
            return File(bytes, "text/csv; charset=utf-8", "urun_kategori_eslestirme.csv");
        }

        [HttpGet]
        public async Task<IActionResult> SapLookup(string sapNos)
        {
            using var db = _contextFactory.CreateDbContext();
            var sapList = sapNos.Split(',').Select(s => s.Trim()).Where(s => !string.IsNullOrEmpty(s)).ToList();

            var siparisler = await db.TBL_VARUNA_SIPARIs.AsNoTracking()
                .Where(s => s.SAPOutReferenceCode != null)
                .Select(s => new { s.OrderId, s.SerialNumber, s.SAPOutReferenceCode, s.OrderStatus, s.InvoiceDate, s.AccountTitle, s.TotalNetAmount })
                .ToListAsync();

            var results = new List<object>();
            foreach (var sap in sapList)
            {
                var matches = siparisler.Where(s => s.SAPOutReferenceCode != null && s.SAPOutReferenceCode.Trim().Contains(sap)).ToList();
                results.Add(new { sap, eslesen = matches.Count, detay = matches.Select(m => new { m.OrderId, m.SerialNumber, m.SAPOutReferenceCode, m.OrderStatus, m.InvoiceDate, m.AccountTitle, m.TotalNetAmount }).ToList() });
            }
            return Json(results);
        }

        /// <summary>
        /// VIEW'de olup Varuna'da olmayan faturaları Varuna'ya ekler (SAP bazlı).
        /// </summary>
        [HttpPost]
        public async Task<IActionResult> AddMissingVaruna([FromBody] List<MissingSiparisDto> items)
        {
            if (items == null || items.Count == 0)
                return Json(new { ok = false, error = "Liste boş" });
            using var db = _contextFactory.CreateDbContext();
            int eklenen = 0;
            foreach (var item in items)
            {
                // Zaten var mı kontrol
                var exists = await db.TBL_VARUNA_SIPARIs.AnyAsync(s =>
                    s.SAPOutReferenceCode == item.SapNo || s.SerialNumber == item.SerialNumber);
                if (exists) continue;

                db.TBL_VARUNA_SIPARIs.Add(new Models.MsK.TBL_VARUNA_SIPARI
                {
                    OrderId = item.OrderId ?? Guid.NewGuid().ToString(),
                    SerialNumber = item.SerialNumber,
                    SAPOutReferenceCode = item.SapNo,
                    OrderStatus = "Closed",
                    TotalNetAmount = item.TotalNetAmount,
                    AccountTitle = item.AccountTitle,
                    InvoiceDate = item.InvoiceDate,
                    CreateOrderDate = item.InvoiceDate,
                    CreatedOn = DateTime.Now
                });
                eklenen++;
            }
            await db.SaveChangesAsync();
            // Cache invalidate
            _cache.Remove(CACHE_KEY_FATURALAR);
            _cache.Remove(CACHE_KEY_VARUNA_TUTAR);
            return Json(new { ok = true, eklenen });
        }
        public class MissingSiparisDto
        {
            public string? OrderId { get; set; }
            public string SerialNumber { get; set; } = "";
            public string SapNo { get; set; } = "";
            public decimal TotalNetAmount { get; set; }
            public string? AccountTitle { get; set; }
            public DateTime? InvoiceDate { get; set; }
        }

        [HttpGet]
        public async Task<IActionResult> TestSpFatura(string? startDate, string? endDate)
        {
            var sd = startDate ?? "2026-03-01";
            var ed = endDate ?? "2026-03-31";
            using var db = _contextFactory.CreateDbContext();
            var rows = await db.Database.SqlQueryRaw<SpFaturaRow>(
                "EXEC SP_COCKPIT_FATURA @p0, @p1", DateTime.Parse(sd), DateTime.Parse(ed)).ToListAsync();
            var toplam = rows.Sum(r => r.NetTutar);
            var (_, _, _, _, _, _, ugm) = await LoadAllCachedDataAsync(_contextFactory, _cache);
            var eslesmeyen = rows.Where(r => !ugm.ContainsKey(r.FaturaNo)).ToList();
            return Json(new { satir = rows.Count, toplam,
                urunEslesen = rows.Count - eslesmeyen.Count,
                urunEslesmeyen = eslesmeyen.Count,
                eslesemeyenToplam = eslesmeyen.Sum(r => r.NetTutar),
                eslesemeyenDetay = eslesmeyen.OrderByDescending(r => r.NetTutar).Take(15).Select(r => new { r.FaturaNo, r.NetTutar, r.Firma, r.IsSentetik })
            });
        }
        public class SpFaturaRow
        {
            public string FaturaNo { get; set; } = "";
            public DateTime EfektifTarih { get; set; }
            public decimal NetTutar { get; set; }
            public string? Firma { get; set; }
            public int VarunaEslesti { get; set; }
            public int TahakkukVar { get; set; }
            public int IsSentetik { get; set; }
        }

        [HttpGet]
        public async Task<IActionResult> TestSpTahsilat(string? startDate, string? endDate)
        {
            var sd = startDate ?? "2026-03-01";
            var ed = endDate ?? "2026-03-31";
            using var db = _contextFactory.CreateDbContext();
            var rows = await db.Database.SqlQueryRaw<SpTahsilatRow>(
                "EXEC SP_COCKPIT_TAHSILAT @p0, @p1", DateTime.Parse(sd), DateTime.Parse(ed)).ToListAsync();
            return Json(rows.FirstOrDefault());
        }
        public class SpTahsilatRow
        {
            public decimal TahsilEdilen { get; set; }
            public int TahsilAdet { get; set; }
            public decimal BekleyenBakiyeToplam { get; set; }
            public decimal VadesiGelenToplam { get; set; }
            public int VadesiGelenAdet { get; set; }
        }

        [HttpGet]
        public async Task<IActionResult> DbColumns(string table)
        {
            using var db = _contextFactory.CreateDbContext();
            var cols = await db.Database.SqlQueryRaw<ColInfo>(
                "SELECT COLUMN_NAME AS Name, DATA_TYPE AS DataType, ORDINAL_POSITION AS Pos FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = {0} ORDER BY ORDINAL_POSITION", table).ToListAsync();
            return Json(cols);
        }
        public class ColInfo { public string Name { get; set; } = ""; public string DataType { get; set; } = ""; public int Pos { get; set; } }

        [HttpGet]
        public async Task<IActionResult> SozlesmeAnaliz()
        {
            using var db = _contextFactory.CreateDbContext();
            var raw = await db.Database.SqlQueryRaw<SozlesmeRaw>(@"
                SELECT
                    s.Id, s.ContractNo, s.ContractName, s.ContractStatus,
                    s.AccountTitle, s.RenewalDate, s.TotalAmount, s.TotalAmountLocal,
                    s.RelatedContractId, s.StartDate, s.FinishDate,
                    r.ContractNo AS RelatedContractNo,
                    r.ContractStatus AS RelatedContractStatus,
                    r.AccountTitle AS RelatedAccountTitle,
                    r.TotalAmount AS RelatedTotalAmount,
                    r.RenewalDate AS RelatedRenewalDate
                FROM TBL_VARUNA_SOZLESME s
                LEFT JOIN TBL_VARUNA_SOZLESME r ON r.Id = s.RelatedContractId
                WHERE s.RenewalDate IS NOT NULL AND YEAR(s.RenewalDate) >= 2025
                ORDER BY s.RenewalDate DESC
            ").ToListAsync();

            return Json(new {
                toplam = raw.Count,
                relatedDolu = raw.Count(r => r.RelatedContractId != null),
                relatedNull = raw.Count(r => r.RelatedContractId == null),
                detay = raw
            });
        }
        public class SozlesmeRaw
        {
            public Guid? Id { get; set; }
            public string? ContractNo { get; set; }
            public string? ContractName { get; set; }
            public string? ContractStatus { get; set; }
            public string? AccountTitle { get; set; }
            public DateTime? RenewalDate { get; set; }
            public decimal? TotalAmount { get; set; }
            public decimal? TotalAmountLocal { get; set; }
            public Guid? RelatedContractId { get; set; }
            public DateTime? StartDate { get; set; }
            public DateTime? FinishDate { get; set; }
            public string? RelatedContractNo { get; set; }
            public string? RelatedContractStatus { get; set; }
            public string? RelatedAccountTitle { get; set; }
            public decimal? RelatedTotalAmount { get; set; }
            public DateTime? RelatedRenewalDate { get; set; }
        }

        [HttpGet]
        public async Task<IActionResult> TestSpSozlesme(string? startDate, string? endDate)
        {
            var sd = startDate ?? "2026-03-01";
            var ed = endDate ?? "2026-03-31";
            using var db = _contextFactory.CreateDbContext();
            var rows = await db.Database.SqlQueryRaw<SpSozlesmeRow>(
                "EXEC SP_COCKPIT_SOZLESME @p0, @p1", DateTime.Parse(sd), DateTime.Parse(ed)).ToListAsync();
            var yenilenen = rows.Where(r => r.Yenilendi == 1).ToList();
            var bekleyen = rows.Where(r => r.Yenilendi == 0).ToList();
            return Json(new {
                toplam = rows.Count,
                yenilenenAdet = yenilenen.Count,
                yenilenenEskiTutar = yenilenen.Sum(r => r.EskiTutar ?? 0),
                yenilenenYeniTutar = yenilenen.Sum(r => r.YeniTutar ?? 0),
                bekleyenAdet = bekleyen.Count,
                bekleyenTutar = bekleyen.Sum(r => r.EskiTutar ?? 0),
                detay = rows.Select(r => new {
                    r.Firma, r.EskiTutar, r.Yenilendi, r.YeniTutar, r.YeniStatus,
                    eskiBitis = r.EskiBitis?.ToString("dd.MM.yyyy")
                })
            });
        }
        public class SpSozlesmeRow
        {
            public Guid? Id { get; set; }
            public string? ContractNo { get; set; }
            public string? ContractName { get; set; }
            public string? ContractStatus { get; set; }
            public string? Firma { get; set; }
            public decimal? EskiTutar { get; set; }
            public decimal? EskiTutarLocal { get; set; }
            public DateTime? EskiBitis { get; set; }
            public DateTime? Yenilemetarihi { get; set; }
            public int Yenilendi { get; set; }
            public string? YeniContractNo { get; set; }
            public string? YeniStatus { get; set; }
            public decimal? YeniTutar { get; set; }
            public decimal? YeniTutarLocal { get; set; }
            public DateTime? YeniBaslangic { get; set; }
            public DateTime? YeniBitis { get; set; }
        }

        [HttpGet]
        public async Task<IActionResult> ViewLookup(string faturaNo)
        {
            using var db = _contextFactory.CreateDbContext();
            var f = await db.VIEW_CP_EXCEL_FATURAs.AsNoTracking()
                .Where(x => x.Fatura_No == faturaNo)
                .FirstOrDefaultAsync();
            if (f == null) return Json(new { ok = false });
            return Json(new {
                ok = true, faturaNo = f.Fatura_No, faturaTarihi = f.Fatura_Tarihi,
                faturaToplam = f.Fatura_Toplam, dovizTutar = f.Doviz_Tutar,
                ilgiliKisi = f.Ilgili_Kisi, saticiAdi = f.Satici_Adi,
                proje = f.Proje, durum = f.Durum, vadeTarihi = f.Fatura_Vade_Tarihi,
                tahsilEdilen = f.Tahsil_Edilen, bekleyenBakiye = f.Bekleyen_Bakiye
            });
        }

        [HttpGet]
        public async Task<IActionResult> FaturaAnaliz(string? filter)
        {
            var (allFaturalar2, _, _, _, _, _, _) = await LoadAllCachedDataAsync(_contextFactory, _cache);
            var (s2, e2, _, _) = ParseFilter(filter, null, null);

            var donemTum = allFaturalar2
                .Where(f => f.EfektifFaturaTarihi.HasValue && f.EfektifFaturaTarihi.Value >= s2 && f.EfektifFaturaTarihi.Value <= e2)
                .ToList();

            var pozitif = donemTum.Where(f => !IsRetDurum(f.Durum) && !IsNegatifDurum(f.Durum)).ToList();
            var negatif = donemTum.Where(f => IsRetDurum(f.Durum) || IsNegatifDurum(f.Durum)).ToList();
            var varunaDisi = donemTum.Where(f => !f.VarunaEslesti).ToList();
            var varunaDisiPoz = varunaDisi.Where(f => !IsRetDurum(f.Durum) && !IsNegatifDurum(f.Durum)).ToList();
            var varunaDisiNeg = varunaDisi.Where(f => IsRetDurum(f.Durum) || IsNegatifDurum(f.Durum)).ToList();

            // Excel Fatura_Tarihi Mart olanlar (tahakkuk override'sız)
            var excelMart = allFaturalar2
                .Where(f => f.Fatura_Tarihi.HasValue && f.Fatura_Tarihi.Value.Month == s2.Month && f.Fatura_Tarihi.Value.Year == s2.Year)
                .ToList();

            // Tahakkuk ile Mart'a GELEN (orijinal tarihi Mart dışı)
            var tahakkukGelen = donemTum.Where(f => f.TahakkukVar && f.Fatura_Tarihi.HasValue
                && !(f.Fatura_Tarihi.Value >= s2 && f.Fatura_Tarihi.Value <= e2)).ToList();
            // Tahakkuk ile Mart'tan ÇIKAN (orijinal tarihi Mart ama efektif başka)
            var tahakkukCikan = excelMart.Where(f => f.TahakkukVar && f.EfektifFaturaTarihi.HasValue
                && !(f.EfektifFaturaTarihi.Value >= s2 && f.EfektifFaturaTarihi.Value <= e2)).ToList();

            return Json(new {
                donem = s2.ToString("dd.MM.yyyy") + " - " + e2.ToString("dd.MM.yyyy"),
                toplamKayit = donemTum.Count,
                pozitifAdet = pozitif.Count,
                pozitifToplam = pozitif.Sum(f => f.NetTutar ?? 0),
                negatifAdet = negatif.Count,
                negatifToplam = negatif.Sum(f => f.NetTutar ?? 0),
                netToplam = pozitif.Sum(f => f.NetTutar ?? 0) - negatif.Sum(f => f.NetTutar ?? 0),
                negatifDetay = negatif.Select(f => new { f.Fatura_No, f.Durum, netTutar = f.NetTutar, f.EfektifFaturaTarihi, f.VarunaEslesti }).ToList(),
                varunaDisi = new {
                    toplam = varunaDisi.Count,
                    pozitif = varunaDisiPoz.Count,
                    pozitifTutar = varunaDisiPoz.Sum(f => f.NetTutar ?? 0),
                    negatif = varunaDisiNeg.Count,
                    negatifTutar = varunaDisiNeg.Sum(f => f.NetTutar ?? 0),
                    detay = varunaDisi.Select(f => new { f.Fatura_No, f.Durum, netTutar = f.NetTutar, f.Fatura_Tarihi, f.EfektifFaturaTarihi, f.VarunaEslesti }).ToList()
                },
                excelFaturaTarihiMart = excelMart.Count,
                excelFaturaTarihiMartToplam = excelMart.Where(f => !IsRetDurum(f.Durum) && !IsNegatifDurum(f.Durum)).Sum(f => f.NetTutar ?? 0),
                tahakkukGelen = tahakkukGelen.Select(f => new { f.Fatura_No, f.Fatura_Tarihi, f.EfektifFaturaTarihi, netTutar = f.NetTutar }).ToList(),
                tahakkukCikan = tahakkukCikan.Select(f => new { f.Fatura_No, f.Fatura_Tarihi, f.EfektifFaturaTarihi, netTutar = f.NetTutar }).ToList()
            });
        }

        [HttpGet]
        public async Task<IActionResult> FaturaDebug(string? filter)
        {
            var (allFaturalar, _, _, _, _, varunaTutarMap, urunGrupMap) = await LoadAllCachedDataAsync(_contextFactory, _cache);
            var (start, end, activeFilter, _) = ParseFilter(filter, null, null);
            using var db = _contextFactory.CreateDbContext();

            // Dönemdeki Varuna eşleşen faturalar
            var donemFaturalar = allFaturalar
                .Where(f => f.VarunaEslesti && f.EfektifFaturaTarihi.HasValue
                    && f.EfektifFaturaTarihi.Value >= start && f.EfektifFaturaTarihi.Value <= end
                    && !IsRetDurum(f.Durum))
                .ToList();

            // Her fatura için: TotalNetAmount (sipariş başlığı) vs kalem bazlı toplam
            var siparisler = await db.TBL_VARUNA_SIPARIs.AsNoTracking()
                .Where(s => s.SerialNumber != null && s.OrderId != null && s.OrderStatus == "Closed")
                .Select(s => new { s.SerialNumber, s.OrderId, s.TotalNetAmount })
                .ToListAsync();
            var sipMap = siparisler.GroupBy(s => s.SerialNumber!).ToDictionary(g => g.Key, g => g.First());

            var urunler = await db.TBL_VARUNA_SIPARIS_URUNLERIs.AsNoTracking()
                .Where(u => u.CrmOrderId != null)
                .Select(u => new { u.CrmOrderId, u.Total })
                .ToListAsync();
            var kalemToplam = urunler.GroupBy(u => u.CrmOrderId!)
                .ToDictionary(g => g.Key, g => g.Sum(u => u.Total ?? 0));

            var farklar = new List<object>();
            decimal toplamTNA = 0, toplamKalem = 0;
            foreach (var f in donemFaturalar)
            {
                if (f.Fatura_No == null || !sipMap.TryGetValue(f.Fatura_No, out var sip)) continue;
                var tna = sip.TotalNetAmount ?? 0;
                var kt = sip.OrderId != null && kalemToplam.TryGetValue(sip.OrderId, out var k) ? k : 0;
                toplamTNA += tna;
                toplamKalem += kt;
                if (Math.Abs(tna - kt) > 1000)
                {
                    farklar.Add(new { faturaNo = f.Fatura_No, orderId = sip.OrderId, totalNetAmount = tna, kalemToplam = kt, fark = tna - kt });
                }
            }

            return Json(new {
                filtre = activeFilter,
                donem = start.ToString("dd.MM.yyyy") + " - " + end.ToString("dd.MM.yyyy"),
                fatAdet = donemFaturalar.Count,
                toplamTotalNetAmount = toplamTNA,
                toplamKalemBazli = toplamKalem,
                fark = toplamTNA - toplamKalem,
                farkliSiparisler = farklar.Count,
                ornekFarklar = farklar.OrderByDescending(x => ((dynamic)x).fark).Take(15)
            });
        }

        #endregion
    }
}
