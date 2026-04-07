using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using SOS.DbData;
using SOS.Models.ViewModels;
using SOS.Models.MsK;

namespace SOS.Controllers
{
    // Raw SQL result DTO
    public class FirsatUrunGrupDto
    {
        public string? UrunGrubu { get; set; }
        public int Adet { get; set; }
        public decimal Tutar { get; set; }
    }

    public class FirsatMusteriDto
    {
        public string? Musteri { get; set; }
        public int Adet { get; set; }
        public decimal Tutar { get; set; }
    }

    public class MusteriDto { public string? Name { get; set; } public string? Musteri { get; set; } }

    [Authorize]
    public class FirsatAnalizController : Controller
    {
        private readonly IDbContextFactory<MskDbContext> _contextFactory;
        private readonly IMemoryCache _cache;
        private static readonly TimeSpan CacheTTL = TimeSpan.FromMinutes(5);
        private static readonly SemaphoreSlim _cacheLock = new(1, 1);

        // Cache keys
        private const string CACHE_KEY_URUN_ESLESTIRME = "firsat_urun_eslestirme";
        private const string CACHE_KEY_ANA_URUNLER = "firsat_ana_urunler";

        // REAL Status values from database (English strings, not numeric)
        // Accepted=662, Draft=199, Presented=163, Closed=69, Reject=45, Denied=38, InReview=11, Approved=7, PartiallyOrdered=5
        private static readonly string[] WonStatuses = { "Accepted", "Approved", "PartiallyOrdered" };
        private static readonly string[] LostStatuses = { "Reject", "Denied", "Closed" };
        private static readonly string[] OpenStatuses = { "Draft", "Presented", "InReview" };
        // Pipeline = open (not won, not lost)
        private static readonly string[] PipelineStatuses = { "Draft", "Presented", "InReview" };

        // Siparis statuses (from DB: Open, Closed, Canceled)
        private static readonly string[] SiparisClosedStatuses = { "Closed" };
        private static readonly string[] SiparisCancelledStatuses = { "Canceled" };

        public FirsatAnalizController(IDbContextFactory<MskDbContext> contextFactory, IMemoryCache cache)
        {
            _contextFactory = contextFactory;
            _cache = cache;
        }

        #region ParseFilter

        private (DateTime start, DateTime end, string filter, int months) ParseFilter(string? filter, string? startDate, string? endDate)
        {
            var now = DateTime.Now;
            var today = now.Date.AddHours(23).AddMinutes(59).AddSeconds(59);
            var year = now.Year;
            DateTime start;
            DateTime end2;
            int months;
            var fmtP = System.Globalization.CultureInfo.InvariantCulture;
            var style = System.Globalization.DateTimeStyles.None;

            if (!string.IsNullOrEmpty(startDate) && !string.IsNullOrEmpty(endDate)
                && DateTime.TryParseExact(startDate, "yyyy-MM-dd", fmtP, style, out var sd)
                && DateTime.TryParseExact(endDate, "yyyy-MM-dd", fmtP, style, out var ed))
            {
                filter = "range";
                start = sd.Date;
                end2 = ed.Date.AddHours(23).AddMinutes(59).AddSeconds(59);
                months = Math.Max(1, (end2.Year - start.Year) * 12 + end2.Month - start.Month + 1);
                return (start, end2, filter, months);
            }

            switch (filter?.ToLowerInvariant())
            {
                case "ytd":
                    start = new DateTime(year, 1, 1);
                    end2 = today;
                    months = now.Month;
                    break;
                case "q1":
                    start = new DateTime(year, 1, 1);
                    end2 = new DateTime(year, 3, 31, 23, 59, 59);
                    if (end2 > today) end2 = today;
                    months = 3;
                    break;
                case "q2":
                    start = new DateTime(year, 4, 1);
                    end2 = new DateTime(year, 6, 30, 23, 59, 59);
                    if (end2 > today) end2 = today;
                    months = 3;
                    break;
                case "q3":
                    start = new DateTime(year, 7, 1);
                    end2 = new DateTime(year, 9, 30, 23, 59, 59);
                    if (end2 > today) end2 = today;
                    months = 3;
                    break;
                case "q4":
                    start = new DateTime(year, 10, 1);
                    end2 = new DateTime(year, 12, 31, 23, 59, 59);
                    if (end2 > today) end2 = today;
                    months = 3;
                    break;
                case "lastmonth":
                    var lmMonth = now.Month == 1 ? 12 : now.Month - 1;
                    var lmYear = now.Month == 1 ? year - 1 : year;
                    start = new DateTime(lmYear, lmMonth, 1);
                    end2 = new DateTime(lmYear, lmMonth, DateTime.DaysInMonth(lmYear, lmMonth), 23, 59, 59);
                    months = 1;
                    break;
                case "all":
                    filter = "all";
                    start = new DateTime(2020, 1, 1);
                    end2 = today;
                    months = (today.Year - 2020) * 12 + today.Month;
                    break;
                case "week":
                    var weekStart = today.AddDays(-(int)today.DayOfWeek + 1);
                    if (weekStart > today) weekStart = weekStart.AddDays(-7);
                    start = weekStart.Date;
                    end2 = today;
                    months = 1;
                    break;
                default: // "month" veya null → Bu ay
                    filter = "month";
                    start = new DateTime(year, now.Month, 1);
                    end2 = new DateTime(year, now.Month, DateTime.DaysInMonth(year, now.Month), 23, 59, 59);
                    months = 1;
                    break;
            }

            return (start, end2, filter ?? "month", months);
        }

        #endregion

        #region Status Helpers

        private static string StatusToTurkishStage(string? status) => status switch
        {
            "Draft" => "Taslak",
            "InReview" => "Incelemede",
            "Presented" => "Sunuldu",
            "Approved" => "Onaylandi",
            "Accepted" => "Kabul Edildi",
            "PartiallyOrdered" => "Kismen Siparis",
            "Reject" => "Reddedildi",
            "Denied" => "Reddedildi",
            "Closed" => "Kapatildi",
            _ => status ?? "Bilinmiyor"
        };

        private static string StatusToColor(string? status) => status switch
        {
            "Draft" => "#94a3b8",
            "InReview" => "#f59e0b",
            "Presented" => "#818cf8",
            "Approved" => "#60a5fa",
            "Accepted" => "#10b981",
            "PartiallyOrdered" => "#22c55e",
            "Reject" => "#ef4444",
            "Denied" => "#f87171",
            "Closed" => "#6b7280",
            _ => "#cbd5e1"
        };

        private static string StatusToIcon(string? status) => status switch
        {
            "Draft" => "bi-file-earmark",
            "InReview" => "bi-hourglass-split",
            "Presented" => "bi-send",
            "Approved" or "Accepted" or "PartiallyOrdered" => "bi-check-circle",
            "Reject" or "Denied" => "bi-x-circle",
            "Closed" => "bi-lock",
            _ => "bi-question-circle"
        };

        private static string SiparisStatusToTurkish(string? status) => status switch
        {
            "Open" => "Acik",
            "Closed" => "Kapali",
            "Canceled" => "Iptal",
            _ => status ?? "Bilinmiyor"
        };

        private static string SiparisStatusToColor(string? status) => status switch
        {
            "Open" => "#3b82f6",
            "Closed" => "#22c55e",
            "Cancelled" => "#ef4444",
            "Processing" => "#f59e0b",
            "Invoiced" => "#10b981",
            _ => "#94a3b8"
        };

        #endregion

        /// <summary>
        /// Converts email like "begum.hayta@accounts.univera.com.tr" to "Begüm Hayta"
        /// </summary>
        private static string EmailToDisplayName(string? email)
        {
            if (string.IsNullOrEmpty(email)) return "Bilinmiyor";
            var local = email.Split('@')[0]; // begum.hayta
            var parts = local.Split('.');
            return string.Join(" ", parts.Select(p =>
                System.Globalization.CultureInfo.GetCultureInfo("tr-TR").TextInfo.ToTitleCase(p)));
        }

        #region Product Mapping (TBLSOS_ANA_URUN + TBLSOS_URUN_ESLESTIRME)

        /// <summary>
        /// Loads StockCode -> AnaUrunAd mapping from TBLSOS_URUN_ESLESTIRME + TBLSOS_ANA_URUN.
        /// Cached for 5 minutes.
        /// </summary>
        private async Task<Dictionary<string, string>> GetUrunEslestirmeMapAsync()
        {
            if (_cache.TryGetValue(CACHE_KEY_URUN_ESLESTIRME, out Dictionary<string, string>? cached) && cached != null)
                return cached;

            await _cacheLock.WaitAsync();
            try
            {
                if (_cache.TryGetValue(CACHE_KEY_URUN_ESLESTIRME, out cached) && cached != null)
                    return cached;

                using var db = _contextFactory.CreateDbContext();
                var map = await db.TBLSOS_URUN_ESLESTIRMEs.AsNoTracking()
                    .Include(e => e.AnaUrun)
                    .Where(e => e.AnaUrun != null)
                    .GroupBy(e => e.StokKodu)
                    .Select(g => new { StokKodu = g.Key, AnaUrunAd = g.First().AnaUrun!.Ad })
                    .ToDictionaryAsync(x => x.StokKodu, x => x.AnaUrunAd);

                _cache.Set(CACHE_KEY_URUN_ESLESTIRME, map, CacheTTL);
                return map;
            }
            finally
            {
                _cacheLock.Release();
            }
        }

        /// <summary>
        /// Loads all active TBLSOS_ANA_URUN records. Cached for 5 minutes.
        /// </summary>
        private async Task<List<TBLSOS_ANA_URUN>> GetAnaUrunlerAsync()
        {
            if (_cache.TryGetValue(CACHE_KEY_ANA_URUNLER, out List<TBLSOS_ANA_URUN>? cached) && cached != null)
                return cached;

            using var db = _contextFactory.CreateDbContext();
            var list = await db.TBLSOS_ANA_URUNs.AsNoTracking()
                .Where(u => u.Aktif)
                .OrderBy(u => u.Sira)
                .ToListAsync();

            _cache.Set(CACHE_KEY_ANA_URUNLER, list, CacheTTL);
            return list;
        }

        /// <summary>
        /// Given a StockCode, resolve to AnaUrun.Ad using the eslestirme map.
        /// Returns "Diger" if no match.
        /// </summary>
        private static string ResolveProductGroup(string? stockCode, Dictionary<string, string> eslestirmeMap)
        {
            if (string.IsNullOrEmpty(stockCode)) return "Diger";
            return eslestirmeMap.TryGetValue(stockCode, out var ad) ? ad : "Diger";
        }

        #endregion

        #region Filtered Queryables

        /// <summary>
        /// Base filtered teklifler query: non-deleted.
        /// NO date filter on fırsatlar/teklifler — pipeline always shows ALL open records.
        /// Date filter only applies to siparişler and trend charts.
        /// Optionally filtered by person (CreatedBy) and product (via TBLSOS_URUN_ESLESTIRME).
        /// </summary>
        private IQueryable<TBL_VARUNA_TEKLIF> GetFilteredTeklifler(MskDbContext db, DateTime start, DateTime end, string? person, string? product)
        {
            var q = db.TBL_VARUNA_TEKLIFs.AsNoTracking()
                .Where(t => t.DeletedOn == null);

            if (!string.IsNullOrEmpty(person))
                q = q.Where(t => t.CreatedBy == person);

            if (!string.IsNullOrEmpty(product))
            {
                // product = AnaUrunId (int) or AnaUrun.Kod
                // Find all StokKodu values that belong to this AnaUrun
                var matchingStockCodes = db.TBLSOS_URUN_ESLESTIRMEs.AsNoTracking()
                    .Where(e => e.AnaUrun != null && (e.AnaUrun.Kod == product || e.AnaUrunId.ToString() == product))
                    .Select(e => e.StokKodu);

                var teklifIdsWithProduct = db.TBL_VARUNA_TEKLIF_URUNLERIs.AsNoTracking()
                    .Where(u => u.DeletedOn == null && u.StockCode != null && matchingStockCodes.Contains(u.StockCode))
                    .Select(u => u.QuoteId)
                    .Distinct();

                q = q.Where(t => teklifIdsWithProduct.Contains(t.Id));
            }

            return q;
        }

        private IQueryable<TBL_VARUNA_SIPARI> GetFilteredSiparisler(MskDbContext db, DateTime start, DateTime end)
        {
            return db.TBL_VARUNA_SIPARIs.AsNoTracking()
                .Where(s => s.CreateOrderDate.HasValue
                    && s.CreateOrderDate.Value >= start
                    && s.CreateOrderDate.Value <= end);
        }

        #endregion

        // ===================================================================
        // GET /FirsatAnaliz/Index
        // ===================================================================
        public IActionResult Index(string? filter, string? startDate, string? endDate)
        {
            var (start, end, activeFilter, _) = ParseFilter(filter, startDate, endDate);

            var vm = new FirsatAnalizViewModel
            {
                AktifFiltre = activeFilter,
                FiltreBaslangic = start,
                FiltreBitis = end
            };

            return View(vm);
        }

        // ===================================================================
        // DEBUG: Tüm alanların doluluk oranı ve örnek değerler
        // (class-level [Authorize] devralır)
        // ===================================================================
        [HttpGet]
        public async Task<IActionResult> TestKpi(string? filter)
        {
            var (start, end, f, _) = ParseFilter(filter ?? "all", null, null);
            using var db = _contextFactory.CreateDbContext();
            var teklifler = GetFilteredTeklifler(db, start, end, null, null);
            var totalCount = await teklifler.CountAsync();
            var openList = new[] { "Draft", "Presented", "InReview" };
            var openCount = await teklifler.Where(t => t.Status != null && openList.Contains(t.Status)).CountAsync();
            var openSum = await teklifler.Where(t => t.Status != null && openList.Contains(t.Status)).SumAsync(t => t.TotalNetAmountLocalCurrency_Amount ?? 0m);
            var wonCount = await teklifler.Where(t => t.Status != null && WonStatuses.Contains(t.Status)).CountAsync();
            var wonSum = await teklifler.Where(t => t.Status != null && WonStatuses.Contains(t.Status)).SumAsync(t => t.TotalNetAmountLocalCurrency_Amount ?? 0m);
            return Json(new { filter = f, start, end, totalCount, openCount, openSum, wonCount, wonSum });
        }

        [HttpGet]
        public async Task<IActionResult> FieldAudit()
        {
            using var db = _contextFactory.CreateDbContext();
            var total = await db.TBL_VARUNA_TEKLIFs.AsNoTracking().Where(t => t.DeletedOn == null).CountAsync();

            // Her alanın doluluk oranı
            var fields = new {
                total,
                Status = await db.TBL_VARUNA_TEKLIFs.AsNoTracking().Where(t => t.DeletedOn == null && t.Status != null).CountAsync(),
                CreatedBy = await db.TBL_VARUNA_TEKLIFs.AsNoTracking().Where(t => t.DeletedOn == null && t.CreatedBy != null).CountAsync(),
                CreatedOn = await db.TBL_VARUNA_TEKLIFs.AsNoTracking().Where(t => t.DeletedOn == null && t.CreatedOn != null).CountAsync(),
                FirstCreatedByName = await db.TBL_VARUNA_TEKLIFs.AsNoTracking().Where(t => t.DeletedOn == null && t.FirstCreatedByName != null).CountAsync(),
                FirstCreatedDate = await db.TBL_VARUNA_TEKLIFs.AsNoTracking().Where(t => t.DeletedOn == null && t.FirstCreatedDate != null).CountAsync(),
                ModifiedBy = await db.TBL_VARUNA_TEKLIFs.AsNoTracking().Where(t => t.DeletedOn == null && t.ModifiedBy != null).CountAsync(),
                ModifiedOn = await db.TBL_VARUNA_TEKLIFs.AsNoTracking().Where(t => t.DeletedOn == null && t.ModifiedOn != null).CountAsync(),
                Account_Title = await db.TBL_VARUNA_TEKLIFs.AsNoTracking().Where(t => t.DeletedOn == null && t.Account_Title != null).CountAsync(),
                Name = await db.TBL_VARUNA_TEKLIFs.AsNoTracking().Where(t => t.DeletedOn == null && t.Name != null).CountAsync(),
                OpportunityId = await db.TBL_VARUNA_TEKLIFs.AsNoTracking().Where(t => t.DeletedOn == null && t.OpportunityId != null).CountAsync(),
                ProposalOwnerId = await db.TBL_VARUNA_TEKLIFs.AsNoTracking().Where(t => t.DeletedOn == null && t.ProposalOwnerId != null).CountAsync(),
                PersonId = await db.TBL_VARUNA_TEKLIFs.AsNoTracking().Where(t => t.DeletedOn == null && t.PersonId != null).CountAsync(),
                TeamId = await db.TBL_VARUNA_TEKLIFs.AsNoTracking().Where(t => t.DeletedOn == null && t.TeamId != null).CountAsync(),
                AccountId = await db.TBL_VARUNA_TEKLIFs.AsNoTracking().Where(t => t.DeletedOn == null && t.AccountId != null).CountAsync(),
                CrmOrderId = await db.TBL_VARUNA_TEKLIFs.AsNoTracking().Where(t => t.DeletedOn == null && t.CrmOrderId != null).CountAsync(),
                TotalNetAmount = await db.TBL_VARUNA_TEKLIFs.AsNoTracking().Where(t => t.DeletedOn == null && t.TotalNetAmountLocalCurrency_Amount != null && t.TotalNetAmountLocalCurrency_Amount > 0).CountAsync(),
                ExpirationDate = await db.TBL_VARUNA_TEKLIFs.AsNoTracking().Where(t => t.DeletedOn == null && t.ExpirationDate != null).CountAsync(),
                Number = await db.TBL_VARUNA_TEKLIFs.AsNoTracking().Where(t => t.DeletedOn == null && t.Number != null).CountAsync(),
                StockId = await db.TBL_VARUNA_TEKLIFs.AsNoTracking().Where(t => t.DeletedOn == null && t.StockId != null).CountAsync(),
            };

            // Status dağılımı
            var statuses = await db.TBL_VARUNA_TEKLIFs.AsNoTracking()
                .Where(t => t.DeletedOn == null)
                .GroupBy(t => t.Status)
                .Select(g => new { status = g.Key, count = g.Count(), sumNet = g.Sum(x => x.TotalNetAmountLocalCurrency_Amount ?? 0m) })
                .OrderByDescending(x => x.count).ToListAsync();

            // CreatedBy kişiler (email → isim dönüşümü test)
            var persons = await db.TBL_VARUNA_TEKLIFs.AsNoTracking()
                .Where(t => t.DeletedOn == null && t.CreatedBy != null)
                .GroupBy(t => t.CreatedBy)
                .Select(g => new { email = g.Key, count = g.Count(), pipeline = g.Where(x => x.Status == "Draft" || x.Status == "Presented" || x.Status == "InReview").Sum(x => x.TotalNetAmountLocalCurrency_Amount ?? 0m) })
                .OrderByDescending(x => x.count).Take(15).ToListAsync();

            // Ürün kalemleri - hangi tablo, kaç kayıt
            var teklifUrunCount = await db.TBL_VARUNA_TEKLIF_URUNLERIs.AsNoTracking().Where(u => u.DeletedOn == null).CountAsync();
            var teklifUrunSample = await db.TBL_VARUNA_TEKLIF_URUNLERIs.AsNoTracking()
                .Where(u => u.DeletedOn == null && u.StockCode != null)
                .Select(u => new { u.StockCode, u.StockName, u.Total_Amount, u.QuoteId })
                .Take(5).ToListAsync();

            // TBLSOS eşleştirme
            var eslestirmeCount = await db.TBLSOS_URUN_ESLESTIRMEs.AsNoTracking().CountAsync();
            var anaUrunler = await db.TBLSOS_ANA_URUNs.AsNoTracking().Where(u => u.Aktif).OrderBy(u => u.Sira).ToListAsync();
            var eslestirmeSample = await db.TBLSOS_URUN_ESLESTIRMEs.AsNoTracking()
                .Include(e => e.AnaUrun).Take(10)
                .Select(e => new { e.StokKodu, e.Mask, e.LisansTipi, AnaUrun = e.AnaUrun != null ? e.AnaUrun.Ad : null })
                .ToListAsync();

            // Sipariş bilgileri
            var siparisTotal = await db.TBL_VARUNA_SIPARIs.AsNoTracking().CountAsync();
            var siparisStatuses = await db.TBL_VARUNA_SIPARIs.AsNoTracking()
                .GroupBy(s => s.OrderStatus).Select(g => new { status = g.Key, count = g.Count(), sum = g.Sum(x => x.TotalNetAmount ?? 0m) })
                .OrderByDescending(x => x.count).ToListAsync();

            // 5 örnek teklif - TÜM önemli alanlar
            var samples = await db.TBL_VARUNA_TEKLIFs.AsNoTracking()
                .Where(t => t.DeletedOn == null && t.TotalNetAmountLocalCurrency_Amount > 0)
                .OrderByDescending(t => t.TotalNetAmountLocalCurrency_Amount)
                .Take(5)
                .Select(t => new {
                    t.Id, t.Number, t.Name, t.Status, t.Account_Title,
                    t.CreatedBy, t.ModifiedBy, t.CreatedOn, t.ModifiedOn,
                    t.FirstCreatedByName, t.FirstCreatedDate,
                    t.ProposalOwnerId, t.PersonId, t.TeamId, t.AccountId,
                    t.TotalNetAmountLocalCurrency_Amount,
                    t.TotalAmountWithTaxLocalCurrency_Amount,
                    t.TotalProfitAmount_Amount,
                    t.CrmOrderId, t.OpportunityId, t.ExpirationDate, t.StockId
                }).ToListAsync();

            return Json(new { fields, statuses, persons, teklifUrunCount, teklifUrunSample, eslestirmeCount, anaUrunler, eslestirmeSample, siparisTotal, siparisStatuses, samples });
        }

        // ===================================================================
        // GET /FirsatAnaliz/GetKpiSummary
        // ===================================================================
        [HttpGet]
        public async Task<IActionResult> GetKpiSummary(string? filter, string? startDate, string? endDate, string? person, string? product)
        {
            var (start, end, _, _) = ParseFilter(filter, startDate, endDate);

            using var db = _contextFactory.CreateDbContext();
            var teklifler = GetFilteredTeklifler(db, start, end, person, product);

            // Pipeline: Status IN open (1,2,3,6) = active pipeline
            var activeTeklifler = teklifler.Where(t => t.Status != null && OpenStatuses.Contains(t.Status));
            var pipelineToplam = await activeTeklifler.SumAsync(t => t.TotalNetAmountLocalCurrency_Amount ?? 0m);
            var aktifFirsatAdet = await activeTeklifler.CountAsync();

            // Trend: compare current period vs previous period of same duration
            var duration = end - start;
            var prevStart = start.AddDays(-duration.TotalDays);
            var prevEnd = start.AddSeconds(-1);
            var prevTeklifler = db.TBL_VARUNA_TEKLIFs.AsNoTracking()
                .Where(t => t.DeletedOn == null
                    && t.CreatedOn.HasValue
                    && t.CreatedOn.Value >= prevStart
                    && t.CreatedOn.Value <= prevEnd);
            if (!string.IsNullOrEmpty(person))
                prevTeklifler = prevTeklifler.Where(t => t.CreatedBy == person);

            var prevPipeline = await prevTeklifler
                .Where(t => t.Status != null && OpenStatuses.Contains(t.Status))
                .SumAsync(t => t.TotalNetAmountLocalCurrency_Amount ?? 0m);
            var aktifFirsatTrend = prevPipeline > 0
                ? Math.Round((pipelineToplam - prevPipeline) / prevPipeline * 100, 1)
                : 0m;

            // Acik teklifler: Status IN ('1','2','3','6')
            var acikTeklifAdet = aktifFirsatAdet; // same as pipeline count
            var acikTeklifToplam = pipelineToplam;

            // Siparisler
            var siparisler = GetFilteredSiparisler(db, start, end);
            var acikSiparisler = siparisler.Where(s => s.OrderStatus != null
                && !SiparisClosedStatuses.Contains(s.OrderStatus));
            var acikSiparisAdet = await acikSiparisler.CountAsync();
            var acikSiparisToplam = await acikSiparisler.SumAsync(s => s.TotalNetAmount ?? 0m);

            var kapaliSiparisler = siparisler.Where(s => s.OrderStatus != null
                && (s.OrderStatus == "Closed" || s.OrderStatus == "Completed"));
            var kapaliSiparisAdet = await kapaliSiparisler.CountAsync();
            var kapaliSiparisToplam = await kapaliSiparisler.SumAsync(s => s.TotalNetAmount ?? 0m);

            // Kazanma oranlari
            var wonCount = await teklifler.Where(t => t.Status != null && WonStatuses.Contains(t.Status)).CountAsync();
            var lostCount = await teklifler.Where(t => t.Status != null && LostStatuses.Contains(t.Status)).CountAsync();
            var kazanmaOraniCount = (wonCount + lostCount) > 0
                ? Math.Round((decimal)wonCount / (wonCount + lostCount) * 100, 1)
                : 0m;

            var wonRevenue = await teklifler
                .Where(t => t.Status != null && WonStatuses.Contains(t.Status))
                .SumAsync(t => t.TotalNetAmountLocalCurrency_Amount ?? 0m);
            var lostRevenue = await teklifler
                .Where(t => t.Status != null && LostStatuses.Contains(t.Status))
                .SumAsync(t => t.TotalNetAmountLocalCurrency_Amount ?? 0m);
            var kazanmaOraniRevenue = (wonRevenue + lostRevenue) > 0
                ? Math.Round(wonRevenue / (wonRevenue + lostRevenue) * 100, 1)
                : 0m;

            var ortAnlasma = aktifFirsatAdet > 0
                ? Math.Round(pipelineToplam / aktifFirsatAdet, 2)
                : 0m;

            return Json(new FirsatKpiDto(
                PipelineToplam: pipelineToplam,
                AktifFirsatAdet: aktifFirsatAdet,
                AktifFirsatTrend: aktifFirsatTrend,
                AcikTeklifAdet: acikTeklifAdet,
                AcikTeklifToplam: acikTeklifToplam,
                AcikSiparisAdet: acikSiparisAdet,
                AcikSiparisToplam: acikSiparisToplam,
                KapaliSiparisAdet: kapaliSiparisAdet,
                KapaliSiparisToplam: kapaliSiparisToplam,
                KazanmaOraniCount: kazanmaOraniCount,
                KazanmaOraniRevenue: kazanmaOraniRevenue,
                OrtAnlasma: ortAnlasma
            ));
        }

        // ===================================================================
        // GET /FirsatAnaliz/GetFunnelData
        // ===================================================================
        [HttpGet]
        public async Task<IActionResult> GetFunnelData(string? filter, string? startDate, string? endDate, string? person, string? product)
        {
            var (start, end, _, _) = ParseFilter(filter, startDate, endDate);

            using var db = _contextFactory.CreateDbContext();
            var teklifler = GetFilteredTeklifler(db, start, end, person, product);

            // Stage 1: Toplam Firsat - ALL non-deleted teklifler in period
            var firsatCount = await teklifler.CountAsync();
            var firsatValue = await teklifler.SumAsync(t => t.TotalNetAmountLocalCurrency_Amount ?? 0m);

            // Stage 2: Acik Pipeline - Status IN (1,2,3,6)
            var acikPipeline = teklifler.Where(t => t.Status != null && OpenStatuses.Contains(t.Status));
            var acikCount = await acikPipeline.CountAsync();
            var acikValue = await acikPipeline.SumAsync(t => t.TotalNetAmountLocalCurrency_Amount ?? 0m);

            // Stage 3: Sunuldu - Status = '6' specifically
            var sunuldu = teklifler.Where(t => t.Status == "6");
            var sunulduCount = await sunuldu.CountAsync();
            var sunulduValue = await sunuldu.SumAsync(t => t.TotalNetAmountLocalCurrency_Amount ?? 0m);

            // Stage 4: Kazanilan - Status IN (4,7,10)
            var wonTeklifler = teklifler.Where(t => t.Status != null && WonStatuses.Contains(t.Status));
            var wonCount = await wonTeklifler.CountAsync();
            var wonValue = await wonTeklifler.SumAsync(t => t.TotalNetAmountLocalCurrency_Amount ?? 0m);

            // Stage 5: Siparis Olusan - COUNT TBL_VARUNA_SIPARI linked via CrmOrderId
            var teklifCrmOrderIds = await teklifler
                .Where(t => t.CrmOrderId != null)
                .Select(t => t.CrmOrderId!.Value.ToString())
                .ToListAsync();

            var teklifIds = await teklifler.Select(t => t.Id.ToString()).ToListAsync();

            var linkedSiparisler = db.TBL_VARUNA_SIPARIs.AsNoTracking()
                .Where(s => (s.QuoteId != null && teklifIds.Contains(s.QuoteId))
                    || (s.OrderId != null && teklifCrmOrderIds.Contains(s.OrderId)));
            var siparisCount = await linkedSiparisler.CountAsync();
            var siparisValue = await linkedSiparisler.SumAsync(s => s.TotalNetAmount ?? 0m);

            var stages = new List<FunnelStageDto>
            {
                new("Toplam Firsat", firsatCount, firsatValue, 100m, "#3b82f6"),
                new("Acik Pipeline", acikCount, acikValue,
                    firsatCount > 0 ? Math.Round((decimal)acikCount / firsatCount * 100, 1) : 0m, "#8b5cf6"),
                new("Sunuldu", sunulduCount, sunulduValue,
                    acikCount > 0 ? Math.Round((decimal)sunulduCount / acikCount * 100, 1) : 0m, "#f59e0b"),
                new("Kazanilan", wonCount, wonValue,
                    firsatCount > 0 ? Math.Round((decimal)wonCount / firsatCount * 100, 1) : 0m, "#22c55e"),
                new("Siparis Olusan", siparisCount, siparisValue,
                    wonCount > 0 ? Math.Round((decimal)siparisCount / wonCount * 100, 1) : 0m, "#10b981")
            };

            return Json(stages);
        }

        // ===================================================================
        // GET /FirsatAnaliz/GetStatusBreakdown?type=firsatlar|teklifler|siparisler
        // ===================================================================
        [HttpGet]
        public async Task<IActionResult> GetStatusBreakdown(string type, string? filter, string? startDate, string? endDate, string? person, string? product)
        {
            var (start, end, _, _) = ParseFilter(filter, startDate, endDate);

            using var db = _contextFactory.CreateDbContext();

            switch (type?.ToLowerInvariant())
            {
                case "firsatlar":
                case "teklifler":
                {
                    var teklifler = await GetFilteredTeklifler(db, start, end, person, product)
                        .GroupBy(t => t.Status ?? "0")
                        .Select(g => new { Status = g.Key, Count = g.Count(), Total = g.Sum(t => t.TotalNetAmountLocalCurrency_Amount ?? 0m) })
                        .ToListAsync();

                    var items = teklifler.Select(g => new StatusBreakdownDto(
                        StatusName: StatusToTurkishStage(g.Status),
                        Count: g.Count,
                        TotalValue: g.Total,
                        Color: StatusToColor(g.Status),
                        Icon: StatusToIcon(g.Status)
                    )).OrderByDescending(i => i.TotalValue).ToList();

                    var group = new StatusBreakdownGroupDto(
                        GroupTitle: type == "firsatlar" ? "Firsat Durumlari" : "Teklif Durumlari",
                        GrandTotal: items.Sum(i => i.TotalValue),
                        GrandCount: items.Sum(i => i.Count),
                        Items: items
                    );
                    return Json(group);
                }
                case "siparisler":
                {
                    var siparisler = await GetFilteredSiparisler(db, start, end)
                        .GroupBy(s => s.OrderStatus ?? "Bilinmiyor")
                        .Select(g => new { Status = g.Key, Count = g.Count(), Total = g.Sum(s => s.TotalNetAmount ?? 0m) })
                        .ToListAsync();

                    var items = siparisler.Select(g => new StatusBreakdownDto(
                        StatusName: SiparisStatusToTurkish(g.Status),
                        Count: g.Count,
                        TotalValue: g.Total,
                        Color: SiparisStatusToColor(g.Status),
                        Icon: "fas fa-shopping-cart"
                    )).OrderByDescending(i => i.TotalValue).ToList();

                    var group = new StatusBreakdownGroupDto(
                        GroupTitle: "Siparis Durumlari",
                        GrandTotal: items.Sum(i => i.TotalValue),
                        GrandCount: items.Sum(i => i.Count),
                        Items: items
                    );
                    return Json(group);
                }
                default:
                    return BadRequest(new { error = "Gecersiz tip. Kullanilabilir: firsatlar, teklifler, siparisler" });
            }
        }

        // ===================================================================
        // GET /FirsatAnaliz/GetChartData?chartType=trend|product|customer
        // ===================================================================
        [HttpGet]
        public async Task<IActionResult> GetChartData(string chartType, string? filter, string? startDate, string? endDate, string? person, string? product)
        {
            var (start, end, _, _) = ParseFilter(filter, startDate, endDate);
            var cacheKey = $"FirsatChart_{chartType}_{start:yyyyMMdd}_{end:yyyyMMdd}_{person}_{product}";

            if (_cache.TryGetValue(cacheKey, out ChartResponseDto? cached) && cached != null)
                return Json(cached);

            ChartResponseDto result;

            switch (chartType?.ToLowerInvariant())
            {
                case "trend":
                {
                    // Last 6 months from end date
                    var trendStart = end.AddMonths(-5);
                    trendStart = new DateTime(trendStart.Year, trendStart.Month, 1);

                    var labels = new List<string>();
                    var pipelineData = new List<decimal>();
                    var wonData = new List<decimal>();
                    var siparisData = new List<decimal>();

                    using var db = _contextFactory.CreateDbContext();

                    for (int i = 0; i < 6; i++)
                    {
                        var monthStart = trendStart.AddMonths(i);
                        var monthEnd = new DateTime(monthStart.Year, monthStart.Month,
                            DateTime.DaysInMonth(monthStart.Year, monthStart.Month), 23, 59, 59);

                        labels.Add(monthStart.ToString("MMM yyyy", new System.Globalization.CultureInfo("tr-TR")));

                        var monthTeklifler = db.TBL_VARUNA_TEKLIFs.AsNoTracking()
                            .Where(t => t.DeletedOn == null
                                && t.CreatedOn.HasValue
                                && t.CreatedOn.Value >= monthStart
                                && t.CreatedOn.Value <= monthEnd);

                        if (!string.IsNullOrEmpty(person))
                            monthTeklifler = monthTeklifler.Where(t => t.CreatedBy == person);

                        pipelineData.Add(await monthTeklifler
                            .Where(t => t.Status != null && OpenStatuses.Contains(t.Status))
                            .SumAsync(t => t.TotalNetAmountLocalCurrency_Amount ?? 0m));

                        wonData.Add(await monthTeklifler
                            .Where(t => t.Status != null && WonStatuses.Contains(t.Status))
                            .SumAsync(t => t.TotalNetAmountLocalCurrency_Amount ?? 0m));

                        siparisData.Add(await db.TBL_VARUNA_SIPARIs.AsNoTracking()
                            .Where(s => s.CreateOrderDate.HasValue
                                && s.CreateOrderDate.Value >= monthStart
                                && s.CreateOrderDate.Value <= monthEnd)
                            .SumAsync(s => s.TotalNetAmount ?? 0m));
                    }

                    result = new ChartResponseDto(
                        Labels: labels.ToArray(),
                        Datasets: new List<ChartDatasetDto>
                        {
                            new("Pipeline", pipelineData.ToArray(), "rgba(59,130,246,0.2)", "#3b82f6"),
                            new("Kazanilan", wonData.ToArray(), "rgba(34,197,94,0.2)", "#22c55e"),
                            new("Siparis", siparisData.ToArray(), "rgba(245,158,11,0.2)", "#f59e0b")
                        }
                    );
                    break;
                }
                case "product":
                {
                    // USE TBLSOS_ANA_URUN + TBLSOS_URUN_ESLESTIRME for product grouping
                    var eslestirmeMap = await GetUrunEslestirmeMapAsync();

                    using var db = _contextFactory.CreateDbContext();

                    var teklifUrunleri = await db.TBL_VARUNA_TEKLIF_URUNLERIs.AsNoTracking()
                        .Where(u => u.DeletedOn == null && u.QuoteId != null)
                        .Select(u => new { u.QuoteId, u.StockCode, Total = u.NetLineTotalAmountLocal_Amount ?? 0m })
                        .ToListAsync();

                    // Filter by date range via teklifler
                    var teklifIdsInRange = await GetFilteredTeklifler(db, start, end, person, product)
                        .Select(t => t.Id)
                        .ToListAsync();

                    var teklifIdSet = teklifIdsInRange.ToHashSet();

                    var grouped = teklifUrunleri
                        .Where(u => u.QuoteId.HasValue && teklifIdSet.Contains(u.QuoteId.Value))
                        .Select(u => new { GrupAdi = ResolveProductGroup(u.StockCode, eslestirmeMap), u.Total })
                        .GroupBy(x => x.GrupAdi)
                        .Select(g => new { Grup = g.Key, Total = g.Sum(x => x.Total) })
                        .OrderByDescending(x => x.Total)
                        .ToList();

                    // Top 5 + Diger
                    var top5 = grouped.Take(5).ToList();
                    var diger = grouped.Skip(5).Sum(x => x.Total);

                    var productLabels = top5.Select(x => x.Grup).ToList();
                    var productValues = top5.Select(x => x.Total).ToList();
                    if (diger > 0)
                    {
                        productLabels.Add("Diger");
                        productValues.Add(diger);
                    }

                    var colors = new[] { "#3b82f6", "#8b5cf6", "#f59e0b", "#10b981", "#ef4444", "#6b7280" };

                    result = new ChartResponseDto(
                        Labels: productLabels.ToArray(),
                        Datasets: new List<ChartDatasetDto>
                        {
                            new("Urun Grubu", productValues.ToArray(),
                                string.Join(",", colors.Take(productLabels.Count)),
                                string.Join(",", colors.Take(productLabels.Count)))
                        }
                    );
                    break;
                }
                case "customer":
                {
                    using var db = _contextFactory.CreateDbContext();

                    var customerData = await GetFilteredTeklifler(db, start, end, person, product)
                        .Where(t => t.Account_Title != null)
                        .GroupBy(t => t.Account_Title!)
                        .Select(g => new { Customer = g.Key, Total = g.Sum(t => t.TotalNetAmountLocalCurrency_Amount ?? 0m) })
                        .OrderByDescending(x => x.Total)
                        .Take(10)
                        .ToListAsync();

                    result = new ChartResponseDto(
                        Labels: customerData.Select(c => c.Customer).ToArray(),
                        Datasets: new List<ChartDatasetDto>
                        {
                            new("Musteri Pipeline", customerData.Select(c => c.Total).ToArray(),
                                "rgba(59,130,246,0.6)", "#3b82f6")
                        }
                    );
                    break;
                }
                default:
                    return BadRequest(new { error = "Gecersiz chartType. Kullanilabilir: trend, product, customer" });
            }

            _cache.Set(cacheKey, result, CacheTTL);
            return Json(result);
        }

        // ===================================================================
        // GET /FirsatAnaliz/GetLeaderboard
        // ===================================================================
        [HttpGet]
        public async Task<IActionResult> GetLeaderboard(string? filter, string? startDate, string? endDate)
        {
            var (start, end, _, _) = ParseFilter(filter, startDate, endDate);
            var cacheKey = $"FirsatLeaderboard_{start:yyyyMMdd}_{end:yyyyMMdd}";

            if (_cache.TryGetValue(cacheKey, out List<LeaderboardEntryDto>? cached) && cached != null)
                return Json(cached);

            using var db = _contextFactory.CreateDbContext();

            var teklifler = await db.TBL_VARUNA_TEKLIFs.AsNoTracking()
                .Where(t => t.DeletedOn == null
                    && t.CreatedOn.HasValue
                    && t.CreatedOn.Value >= start
                    && t.CreatedOn.Value <= end
                    && t.CreatedBy != null)
                .Select(t => new
                {
                    t.CreatedBy,
                    t.Status,
                    Amount = t.TotalNetAmountLocalCurrency_Amount ?? 0m
                })
                .ToListAsync();

            var leaderboard = teklifler
                .GroupBy(t => t.CreatedBy!)
                .Select(g =>
                {
                    var pipeline = g.Where(t => t.Status != null && OpenStatuses.Contains(t.Status)).Sum(t => t.Amount);
                    var totalDeals = g.Count();
                    var wonDeals = g.Count(t => t.Status != null && WonStatuses.Contains(t.Status));
                    var lostDeals = g.Count(t => t.Status != null && LostStatuses.Contains(t.Status));
                    var winRate = (wonDeals + lostDeals) > 0
                        ? Math.Round((decimal)wonDeals / (wonDeals + lostDeals) * 100, 1)
                        : 0m;
                    var avgDealSize = totalDeals > 0 ? Math.Round(pipeline / totalDeals, 2) : 0m;

                    return new { Name = g.Key, Pipeline = pipeline, TotalDeals = totalDeals, WonDeals = wonDeals, WinRate = winRate, AvgDealSize = avgDealSize };
                })
                .OrderByDescending(x => x.Pipeline)
                .Take(10)
                .Select((x, i) => new LeaderboardEntryDto(
                    Rank: i + 1,
                    Name: x.Name,
                    PipelineValue: x.Pipeline,
                    TotalDeals: x.TotalDeals,
                    WonDeals: x.WonDeals,
                    WinRate: x.WinRate,
                    AvgDealSize: x.AvgDealSize
                ))
                .ToList();

            _cache.Set(cacheKey, leaderboard, CacheTTL);
            return Json(leaderboard);
        }

        // ===================================================================
        // GET /FirsatAnaliz/GetRiskAlerts
        // ===================================================================
        [HttpGet]
        public async Task<IActionResult> GetRiskAlerts(string? filter, string? startDate, string? endDate, string? person)
        {
            var now = DateTime.Now;
            var alerts = new List<RiskAlertDto>();

            using var db = _contextFactory.CreateDbContext();

            // Base query for open teklifler (no date filter -- risks are global)
            var openTeklifler = db.TBL_VARUNA_TEKLIFs.AsNoTracking()
                .Where(t => t.DeletedOn == null
                    && t.Status != null
                    && OpenStatuses.Contains(t.Status));

            if (!string.IsNullOrEmpty(person))
                openTeklifler = openTeklifler.Where(t => t.CreatedBy == person);

            // 1. CRITICAL: Stale opportunities - ModifiedOn < 30 days ago AND still open
            var staleDate = now.AddDays(-30);
            var staleOpps = await openTeklifler
                .Where(t => t.ModifiedOn.HasValue && t.ModifiedOn.Value < staleDate)
                .Select(t => new { t.TotalNetAmountLocalCurrency_Amount })
                .ToListAsync();
            if (staleOpps.Count > 0)
            {
                alerts.Add(new RiskAlertDto(
                    Type: "stale_opportunity",
                    Severity: "critical",
                    Title: "Hareketsiz Firsatlar",
                    Message: $"30 gunden fazla suredir guncellenmeyen {staleOpps.Count} acik firsat bulunuyor.",
                    Count: staleOpps.Count,
                    Value: staleOpps.Sum(t => t.TotalNetAmountLocalCurrency_Amount ?? 0m),
                    Icon: "fas fa-exclamation-triangle"
                ));
            }

            // 2. WARNING: Expired quotes - ExpirationDate < today AND open
            var expiredQuotes = await openTeklifler
                .Where(t => t.ExpirationDate.HasValue && t.ExpirationDate.Value < now.Date)
                .Select(t => new { t.TotalNetAmountLocalCurrency_Amount })
                .ToListAsync();
            if (expiredQuotes.Count > 0)
            {
                alerts.Add(new RiskAlertDto(
                    Type: "expired_quote",
                    Severity: "warning",
                    Title: "Suresi Dolmus Teklifler",
                    Message: $"Gecerlilik suresi dolmus {expiredQuotes.Count} acik teklif var.",
                    Count: expiredQuotes.Count,
                    Value: expiredQuotes.Sum(t => t.TotalNetAmountLocalCurrency_Amount ?? 0m),
                    Icon: "fas fa-clock"
                ));
            }

            // 3. WARNING: Expiring soon - ExpirationDate < today+7 AND open AND not yet expired
            var soonDate = now.Date.AddDays(7);
            var expiringSoon = await openTeklifler
                .Where(t => t.ExpirationDate.HasValue
                    && t.ExpirationDate.Value >= now.Date
                    && t.ExpirationDate.Value < soonDate)
                .Select(t => new { t.TotalNetAmountLocalCurrency_Amount })
                .ToListAsync();
            if (expiringSoon.Count > 0)
            {
                alerts.Add(new RiskAlertDto(
                    Type: "expiring_soon",
                    Severity: "warning",
                    Title: "Suresi Dolmak Uzere Olan Teklifler",
                    Message: $"7 gun icinde suresi dolacak {expiringSoon.Count} teklif var.",
                    Count: expiringSoon.Count,
                    Value: expiringSoon.Sum(t => t.TotalNetAmountLocalCurrency_Amount ?? 0m),
                    Icon: "fas fa-hourglass-half"
                ));
            }

            // 4. INFO: Aging orders - CreateOrderDate < 45 days ago AND open
            var agingDate = now.AddDays(-45);
            var agingOrders = await db.TBL_VARUNA_SIPARIs.AsNoTracking()
                .Where(s => s.CreateOrderDate.HasValue
                    && s.CreateOrderDate.Value < agingDate
                    && s.OrderStatus != null
                    && s.OrderStatus == "Open")
                .Select(s => new { s.TotalNetAmount })
                .ToListAsync();
            if (agingOrders.Count > 0)
            {
                alerts.Add(new RiskAlertDto(
                    Type: "aging_order",
                    Severity: "info",
                    Title: "Yaslanan Siparisler",
                    Message: $"45 gunden eski {agingOrders.Count} acik siparis bulunuyor.",
                    Count: agingOrders.Count,
                    Value: agingOrders.Sum(s => s.TotalNetAmount ?? 0m),
                    Icon: "fas fa-info-circle"
                ));
            }

            return Json(alerts);
        }

        // ===================================================================
        // GET /FirsatAnaliz/GetDetail?type=&status=&page=1&pageSize=20
        // ===================================================================
        [HttpGet]
        public async Task<IActionResult> GetDetail(string type, string? status, int page = 1, int pageSize = 20,
            string? filter = null, string? startDate = null, string? endDate = null, string? person = null, string? product = null)
        {
            var (start, end, _, _) = ParseFilter(filter, startDate, endDate);
            page = Math.Max(1, page);
            pageSize = Math.Clamp(pageSize, 1, 100);

            using var db = _contextFactory.CreateDbContext();

            switch (type?.ToLowerInvariant())
            {
                case "firsatlar":
                case "teklifler":
                {
                    var q = GetFilteredTeklifler(db, start, end, person, product);
                    if (!string.IsNullOrEmpty(status))
                        q = q.Where(t => t.Status == status);

                    var totalCount = await q.CountAsync();
                    var rows = await q
                        .OrderByDescending(t => t.CreatedOn)
                        .Skip((page - 1) * pageSize)
                        .Take(pageSize)
                        .Select(t => new DetailRowDto(
                            t.Id.ToString(),
                            t.Number ?? "-",
                            t.Account_Title ?? "-",
                            t.Name ?? "-",
                            StatusToTurkishStage(t.Status),
                            t.TotalNetAmountLocalCurrency_Amount ?? 0m,
                            t.TotalProfitAmount_Amount,
                            t.CreatedOn,
                            t.CreatedBy ?? "-",
                            StatusToColor(t.Status)
                        ))
                        .ToListAsync();

                    return Json(new DetailResponseDto(rows, totalCount, page, pageSize));
                }
                case "siparisler":
                {
                    var q = GetFilteredSiparisler(db, start, end);
                    if (!string.IsNullOrEmpty(status))
                        q = q.Where(s => s.OrderStatus == status);

                    var totalCount = await q.CountAsync();
                    var rows = await q
                        .OrderByDescending(s => s.CreateOrderDate)
                        .Skip((page - 1) * pageSize)
                        .Take(pageSize)
                        .Select(s => new DetailRowDto(
                            s.LNGKOD.ToString(),
                            s.SerialNumber ?? "-",
                            s.AccountTitle ?? "-",
                            "-",
                            SiparisStatusToTurkish(s.OrderStatus),
                            s.TotalNetAmount ?? 0m,
                            s.TotalProfitAmount,
                            s.CreateOrderDate,
                            s.CreatedBy ?? "-",
                            SiparisStatusToColor(s.OrderStatus)
                        ))
                        .ToListAsync();

                    return Json(new DetailResponseDto(rows, totalCount, page, pageSize));
                }
                default:
                    return BadRequest(new { error = "Gecersiz tip" });
            }
        }

        // ===================================================================
        // GET /FirsatAnaliz/GetFilterOptions
        // ===================================================================
        [HttpGet]
        public async Task<IActionResult> GetFilterOptions()
        {
            using var db = _contextFactory.CreateDbContext();

            var kisiler = await db.TBL_VARUNA_TEKLIFs.AsNoTracking()
                .Where(t => t.DeletedOn == null && t.CreatedBy != null)
                .Select(t => t.CreatedBy!)
                .Distinct()
                .OrderBy(n => n)
                .ToListAsync();

            // Use TBLSOS_ANA_URUN for product filter options
            var anaUrunler = await GetAnaUrunlerAsync();

            return Json(new
            {
                kisiler = kisiler.Select(k => new FilterOption(k, k)),
                urunler = anaUrunler.Select(u => new FilterOption(u.Kod, u.Ad))
            });
        }

        // ===================================================================
        // GET /FirsatAnaliz/GetProductPerformance
        // ===================================================================
        [HttpGet]
        public async Task<IActionResult> GetProductPerformance(string? filter, string? startDate, string? endDate, string? person)
        {
            var (start, end, _, _) = ParseFilter(filter, startDate, endDate);

            // Use TBLSOS_ANA_URUN + TBLSOS_URUN_ESLESTIRME
            var eslestirmeMap = await GetUrunEslestirmeMapAsync();

            using var db = _contextFactory.CreateDbContext();

            // Teklif IDs + statuses in range
            var teklifIdsInRange = await db.TBL_VARUNA_TEKLIFs.AsNoTracking()
                .Where(t => t.DeletedOn == null
                    && t.CreatedOn.HasValue
                    && t.CreatedOn.Value >= start
                    && t.CreatedOn.Value <= end
                    && (string.IsNullOrEmpty(person) || t.CreatedBy == person))
                .Select(t => new { t.Id, t.Status })
                .ToListAsync();

            var teklifIdSet = teklifIdsInRange.Select(t => t.Id).ToHashSet();
            var teklifStatusMap = teklifIdsInRange.ToDictionary(t => t.Id, t => t.Status);

            var teklifUrunleri = await db.TBL_VARUNA_TEKLIF_URUNLERIs.AsNoTracking()
                .Where(u => u.DeletedOn == null && u.QuoteId != null)
                .Select(u => new
                {
                    u.QuoteId,
                    u.StockCode,
                    Total = u.NetLineTotalAmountLocal_Amount ?? 0m,
                    Profit = u.TotalProfitAmountLocal_Amount ?? 0m
                })
                .ToListAsync();

            var filteredUrunler = teklifUrunleri
                .Where(u => u.QuoteId.HasValue && teklifIdSet.Contains(u.QuoteId.Value))
                .Select(u =>
                {
                    var grupAdi = ResolveProductGroup(u.StockCode, eslestirmeMap);
                    var status = teklifStatusMap.GetValueOrDefault(u.QuoteId!.Value);
                    return new
                    {
                        GrupAdi = grupAdi,
                        u.Total,
                        u.Profit,
                        IsWon = status != null && WonStatuses.Contains(status),
                        IsLost = status != null && LostStatuses.Contains(status),
                        IsDecided = status != null && (WonStatuses.Contains(status) || LostStatuses.Contains(status))
                    };
                })
                .ToList();

            // Siparis urunleri in range
            var siparislerInRange = await db.TBL_VARUNA_SIPARIs.AsNoTracking()
                .Where(s => s.CreateOrderDate.HasValue
                    && s.CreateOrderDate.Value >= start
                    && s.CreateOrderDate.Value <= end)
                .Select(s => new { s.OrderId })
                .ToListAsync();

            var siparisOrderIds = siparislerInRange.Select(s => s.OrderId).Where(o => o != null).ToHashSet();

            var siparisUrunleri = await db.TBL_VARUNA_SIPARIS_URUNLERIs.AsNoTracking()
                .Where(u => u.CrmOrderId != null)
                .Select(u => new { u.CrmOrderId, u.StockCode, Total = u.Total ?? 0m })
                .ToListAsync();

            var filteredSiparisUrunleri = siparisUrunleri
                .Where(u => siparisOrderIds.Contains(u.CrmOrderId))
                .Select(u => new { GrupAdi = ResolveProductGroup(u.StockCode, eslestirmeMap), u.Total })
                .GroupBy(x => x.GrupAdi)
                .ToDictionary(g => g.Key, g => new { Count = g.Count(), Total = g.Sum(x => x.Total) });

            var productPerformance = filteredUrunler
                .GroupBy(x => x.GrupAdi)
                .Select(g =>
                {
                    var teklifCount = g.Count();
                    var teklifAmount = g.Sum(x => x.Total);
                    var wonCount = g.Count(x => x.IsWon);
                    var decidedCount = g.Count(x => x.IsDecided);
                    var winRate = decidedCount > 0 ? Math.Round((decimal)wonCount / decidedCount * 100, 1) : 0m;
                    var profitMargin = teklifAmount > 0
                        ? Math.Round(g.Sum(x => x.Profit) / teklifAmount * 100, 1)
                        : 0m;

                    filteredSiparisUrunleri.TryGetValue(g.Key, out var sipData);

                    return new
                    {
                        urunGrubu = g.Key,
                        teklifAdet = teklifCount,
                        teklifTutar = teklifAmount,
                        siparisAdet = sipData?.Count ?? 0,
                        siparisTutar = sipData?.Total ?? 0m,
                        kazanmaOrani = winRate,
                        karMarji = profitMargin
                    };
                })
                .OrderByDescending(x => x.teklifTutar)
                .ToList();

            return Json(productPerformance);
        }

        // ===================================================================
        // GET /FirsatAnaliz/GetPersonScorecard?person=X
        // ===================================================================
        [HttpGet]
        public async Task<IActionResult> GetPersonScorecard(string person, string? filter, string? startDate, string? endDate)
        {
            if (string.IsNullOrEmpty(person))
                return BadRequest(new { error = "person parametresi gerekli" });

            var (start, end, _, _) = ParseFilter(filter, startDate, endDate);

            using var db = _contextFactory.CreateDbContext();

            // All teklifler for this person in date range
            var personTeklifler = await db.TBL_VARUNA_TEKLIFs.AsNoTracking()
                .Where(t => t.DeletedOn == null
                    && t.CreatedBy == person
                    && t.CreatedOn.HasValue
                    && t.CreatedOn.Value >= start
                    && t.CreatedOn.Value <= end)
                .ToListAsync();

            // Funnel metrics
            var totalFirsat = personTeklifler.Count;
            var totalPipeline = personTeklifler.Sum(t => t.TotalNetAmountLocalCurrency_Amount ?? 0m);
            var activeCount = personTeklifler.Count(t => t.Status != null && OpenStatuses.Contains(t.Status));
            var activePipeline = personTeklifler.Where(t => t.Status != null && OpenStatuses.Contains(t.Status)).Sum(t => t.TotalNetAmountLocalCurrency_Amount ?? 0m);
            var wonCount = personTeklifler.Count(t => t.Status != null && WonStatuses.Contains(t.Status));
            var wonValue = personTeklifler.Where(t => t.Status != null && WonStatuses.Contains(t.Status)).Sum(t => t.TotalNetAmountLocalCurrency_Amount ?? 0m);
            var lostCount = personTeklifler.Count(t => t.Status != null && LostStatuses.Contains(t.Status));
            var winRate = (wonCount + lostCount) > 0
                ? Math.Round((decimal)wonCount / (wonCount + lostCount) * 100, 1)
                : 0m;
            var avgDealSize = activeCount > 0 ? Math.Round(activePipeline / activeCount, 2) : 0m;

            // Monthly trend (6 months)
            var trendStart = end.AddMonths(-5);
            trendStart = new DateTime(trendStart.Year, trendStart.Month, 1);
            var monthlyTrend = new List<object>();

            for (int i = 0; i < 6; i++)
            {
                var ms = trendStart.AddMonths(i);
                var me = new DateTime(ms.Year, ms.Month, DateTime.DaysInMonth(ms.Year, ms.Month), 23, 59, 59);
                var monthData = personTeklifler.Where(t => t.CreatedOn >= ms && t.CreatedOn <= me).ToList();

                monthlyTrend.Add(new
                {
                    ay = ms.ToString("MMM yyyy", new System.Globalization.CultureInfo("tr-TR")),
                    firsatAdet = monthData.Count,
                    pipeline = monthData.Where(t => t.Status != null && OpenStatuses.Contains(t.Status)).Sum(t => t.TotalNetAmountLocalCurrency_Amount ?? 0m),
                    kazanilan = monthData.Where(t => t.Status != null && WonStatuses.Contains(t.Status)).Sum(t => t.TotalNetAmountLocalCurrency_Amount ?? 0m)
                });
            }

            // Open deals list
            var openDeals = personTeklifler
                .Where(t => t.Status != null && OpenStatuses.Contains(t.Status))
                .OrderByDescending(t => t.TotalNetAmountLocalCurrency_Amount ?? 0m)
                .Take(20)
                .Select(t => new
                {
                    id = t.Id.ToString(),
                    teklifNo = t.Number ?? "-",
                    musteriAdi = t.Account_Title ?? "-",
                    ad = t.Name ?? "-",
                    tutar = t.TotalNetAmountLocalCurrency_Amount ?? 0m,
                    durum = StatusToTurkishStage(t.Status),
                    tarih = t.CreatedOn,
                    sonGuncelleme = t.ModifiedOn
                })
                .ToList();

            // Customer distribution
            var customerDist = personTeklifler
                .Where(t => t.Account_Title != null)
                .GroupBy(t => t.Account_Title!)
                .Select(g => new { musteri = g.Key, adet = g.Count(), tutar = g.Sum(t => t.TotalNetAmountLocalCurrency_Amount ?? 0m) })
                .OrderByDescending(x => x.tutar)
                .Take(10)
                .ToList();

            // Product performance using TBLSOS_ANA_URUN + TBLSOS_URUN_ESLESTIRME
            var eslestirmeMap = await GetUrunEslestirmeMapAsync();
            var teklifIds = personTeklifler.Select(t => t.Id).ToHashSet();

            var teklifUrunleri = await db.TBL_VARUNA_TEKLIF_URUNLERIs.AsNoTracking()
                .Where(u => u.DeletedOn == null && u.QuoteId != null)
                .Select(u => new { u.QuoteId, u.StockCode, Total = u.NetLineTotalAmountLocal_Amount ?? 0m })
                .ToListAsync();

            var personUrunler = teklifUrunleri
                .Where(u => u.QuoteId.HasValue && teklifIds.Contains(u.QuoteId.Value))
                .Select(u => new { GrupAdi = ResolveProductGroup(u.StockCode, eslestirmeMap), u.Total })
                .GroupBy(x => x.GrupAdi)
                .Select(g => new { urunGrubu = g.Key, adet = g.Count(), tutar = g.Sum(x => x.Total) })
                .OrderByDescending(x => x.tutar)
                .ToList();

            return Json(new
            {
                kisi = person,
                funnel = new
                {
                    toplamFirsat = totalFirsat,
                    toplamPipeline = totalPipeline,
                    aktifAdet = activeCount,
                    aktifPipeline = activePipeline,
                    kazanilanAdet = wonCount,
                    kazanilanTutar = wonValue,
                    kaybedilenAdet = lostCount,
                    kazanmaOrani = winRate,
                    ortAnlasma = avgDealSize
                },
                aylikTrend = monthlyTrend,
                acikAnlasmalar = openDeals,
                musteriDagilimi = customerDist,
                urunPerformansi = personUrunler
            });
        }

        // ═══════════════════════════════════════════════════════════════════
        // OPPORTUNITIES BAZLI ANALİZ ENDPOİNTLERİ
        // OwnerId = Satış Temsilcisi, TBLSOS_CRM_KULLANICI_GECICI ile isim çözümle
        // ═══════════════════════════════════════════════════════════════════

        /// <summary>
        /// OwnerId → PersonNameSurname mapping from TBLSOS_CRM_PERSON_ODATA (cached)
        /// </summary>
        private async Task<Dictionary<string, string>> GetOwnerMapAsync()
        {
            if (_cache.TryGetValue("opp_owner_map_v2", out Dictionary<string, string>? cached) && cached != null)
                return cached;

            using var db = _contextFactory.CreateDbContext();
            var map = await db.TBLSOS_CRM_PERSON_ODATAs.AsNoTracking()
                .Where(p => p.PersonNameSurname != null)
                .ToDictionaryAsync(p => p.Id, p => p.PersonNameSurname!);

            _cache.Set("opp_owner_map_v2", map, CacheTTL);
            return map;
        }

        /// <summary>
        /// Dönem hedef tutarını DB'den çeker (TBLSOS_HEDEF_AYLIK, Tip=GENEL)
        /// </summary>
        private async Task<decimal> GetDonemHedefAsync(DateTime start, DateTime end)
        {
            var months = Enumerable.Range(0, (end.Year - start.Year) * 12 + end.Month - start.Month + 1)
                .Select(i => new { Yil = start.AddMonths(i).Year, Ay = start.AddMonths(i).Month });

            using var db = _contextFactory.CreateDbContext();
            decimal toplam = 0;
            foreach (var m in months)
            {
                var hedef = await db.TBLSOS_HEDEF_AYLIKs.AsNoTracking()
                    .Where(h => h.Yil == m.Yil && h.Ay == m.Ay && h.Tip == "GENEL" && h.Aktif)
                    .Select(h => h.HedefTutar)
                    .FirstOrDefaultAsync();
                toplam += hedef;
            }
            return toplam;
        }

        private string ResolveOwnerName(string? ownerId, Dictionary<string, string> ownerMap)
        {
            if (string.IsNullOrEmpty(ownerId)) return "Bilinmiyor";
            return ownerMap.TryGetValue(ownerId, out var name) ? name : ownerId[..8] + "…";
        }

        // ───────────────────────────────────────────────────────────────
        // GET /FirsatAnaliz/GetOpportunitySummary
        // ───────────────────────────────────────────────────────────────
        [HttpGet]
        public async Task<IActionResult> GetOpportunitySummary(string? filter, string? startDate, string? endDate, string? owner)
        {
            using var db = _contextFactory.CreateDbContext();
            var (start, end, _, _) = ParseFilter(filter, startDate, endDate);
            var ownerMap = await GetOwnerMapAsync();

            // TBLSOS_VARUNA_FIRSAT_ODATA — OData'dan sync edilmiş, Amount dolu
            var query = db.TBLSOS_VARUNA_FIRSAT_ODATAs.AsNoTracking()
                .Where(o => o.CloseDate.HasValue
                    && o.CloseDate.Value >= start && o.CloseDate.Value <= end);

            if (!string.IsNullOrEmpty(owner))
                query = query.Where(o => o.OwnerId == owner);

            var data = await query.Select(o => new
            {
                o.OwnerId,
                o.OpportunityStageName,
                DealType = o.DealType,
                DealTypeTR = (string?)null,
                o.Probability,
                o.CloseDate,
                CreatedOn = (DateTime?)null,
                AmountAmount = o.AmountValue,
                o.AccountId,
                o.Name
            }).ToListAsync();

            var toplam = data.Count;
            var wonList = data.Where(d => d.OpportunityStageName == "Won").ToList();
            var lostList = data.Where(d => d.OpportunityStageName != null
                && (d.OpportunityStageName == "Lost" || d.OpportunityStageName.Contains("Closed"))).ToList();
            var activeList = data.Where(d => d.OpportunityStageName != null
                && d.OpportunityStageName != "Won"
                && d.OpportunityStageName != "Lost"
                && !d.OpportunityStageName.Contains("Closed")).ToList();

            var kazanmaOrani = (wonList.Count + lostList.Count) > 0
                ? Math.Round((decimal)wonList.Count / (wonList.Count + lostList.Count) * 100, 1)
                : 0m;

            // ── Tutarlar: OData AmountValue'dan doğrudan ──
            var toplamFirsatTutar = data.Sum(d => d.AmountAmount ?? 0m);
            var wonTutar = wonList.Sum(d => d.AmountAmount ?? 0m);
            var lostTutar = lostList.Sum(d => d.AmountAmount ?? 0m);
            var aktivTutar = activeList.Sum(d => d.AmountAmount ?? 0m);
            var kazanmaOraniRevenue = (wonTutar + lostTutar) > 0
                ? Math.Round(wonTutar / (wonTutar + lostTutar) * 100, 1) : 0m;

            // ── SATIŞ HUNİSİ: Fırsat → Teklif → Sipariş → Fatura zinciri ──

            // Tüm fırsatlar (havuz — dönem filtresi yok)
            var tumFirsatlar = await db.TBLSOS_VARUNA_FIRSAT_ODATAs.AsNoTracking()
                .Select(o => new { o.AmountValue, o.OpportunityStageName })
                .ToListAsync();
            var tumFirsatAdet = tumFirsatlar.Count;
            var tumFirsatTutar = tumFirsatlar.Sum(o => o.AmountValue ?? 0m);

            // Fırsatsız teklifler (havuz uyarısı — OpportunityId NULL)
            var firsatsizTeklifAdet = await db.TBL_VARUNA_TEKLIFs.AsNoTracking()
                .CountAsync(t => t.DeletedOn == null && t.OpportunityId == null);
            var firsatsizTeklifTutar = await db.TBL_VARUNA_TEKLIFs.AsNoTracking()
                .Where(t => t.DeletedOn == null && t.OpportunityId == null)
                .SumAsync(t => t.TotalNetAmountLocalCurrency_Amount ?? 0m);

            // Dönemdeki fırsatların ID'leri (data zaten CloseDate filtrelenmiş)
            var donemFirsatIds = await query.Select(o => o.Id).ToListAsync();
            var donemFirsatGuidSet = donemFirsatIds
                .Where(id => Guid.TryParse(id, out _))
                .Select(id => Guid.Parse(id))
                .ToHashSet();

            // Bu fırsatlara bağlı teklifler
            var donemTeklifler = await db.TBL_VARUNA_TEKLIFs.AsNoTracking()
                .Where(t => t.DeletedOn == null && t.OpportunityId.HasValue && donemFirsatGuidSet.Contains(t.OpportunityId.Value))
                .Select(t => new { t.Id, t.TotalNetAmountLocalCurrency_Amount, t.Status, t.OpportunityId })
                .ToListAsync();
            var teklifToplam = donemTeklifler.Count;
            var teklifTutar = donemTeklifler.Sum(t => t.TotalNetAmountLocalCurrency_Amount ?? 0m);

            // Ağırlıklı potansiyel: Teklif tutarı × Fırsat olasılığı
            var firsatProbMap = await db.TBLSOS_VARUNA_FIRSAT_ODATAs.AsNoTracking()
                .Where(o => donemFirsatGuidSet.Select(g => g.ToString()).Contains(o.Id))
                .ToDictionaryAsync(o => o.Id, o => o.Probability ?? 0m);

            var agirlikliPotansiyel = donemTeklifler.Sum(t => {
                var prob = t.OpportunityId.HasValue && firsatProbMap.TryGetValue(t.OpportunityId.Value.ToString(), out var p) ? p : 0m;
                return (t.TotalNetAmountLocalCurrency_Amount ?? 0m) * prob / 100m;
            });
            var ortOlasilik = donemTeklifler.Count > 0
                ? donemTeklifler.Average(t => {
                    return t.OpportunityId.HasValue && firsatProbMap.TryGetValue(t.OpportunityId.Value.ToString(), out var p) ? p : 0m;
                }) : 0m;

            // Bu tekliflere bağlı siparişler
            var donemTeklifIdSet = donemTeklifler.Select(t => t.Id.ToString()).ToHashSet();
            var donemSiparisler = await db.TBL_VARUNA_SIPARIs.AsNoTracking()
                .Where(s => s.QuoteId != null && donemTeklifIdSet.Contains(s.QuoteId))
                .Select(s => new { s.TotalNetAmount, s.OrderStatus, s.InvoiceDate, s.CreateOrderDate })
                .ToListAsync();
            // Dönemden ÖNCE faturlananları düş (Mart faturalı → Nisan sipariş kartında görünmemeli)
            var aktifSiparisler = donemSiparisler.Where(s =>
                !s.InvoiceDate.HasValue || s.InvoiceDate.Value >= start).ToList();
            var acikSiparisAdet = aktifSiparisler.Count(s => s.OrderStatus == "Open");
            var acikSiparisTutar = aktifSiparisler.Where(s => s.OrderStatus == "Open").Sum(s => s.TotalNetAmount ?? 0m);
            var toplamSiparisAdet = aktifSiparisler.Count;
            var toplamSiparisTutar = aktifSiparisler.Sum(s => s.TotalNetAmount ?? 0m);

            // Faturalanan siparişler — sadece InvoiceDate dönem içinde olanlar
            // InvoiceDate NULL ise fatura henüz kesilmemiş demek, gösterme
            var kapaliDonemde = donemSiparisler.Where(s => s.OrderStatus == "Closed"
                && s.InvoiceDate.HasValue
                && s.InvoiceDate.Value >= start && s.InvoiceDate.Value <= end)
                .ToList();
            var kapaliSiparisAdet = kapaliDonemde.Count;
            var kapaliSiparisTutar = kapaliDonemde.Sum(s => s.TotalNetAmount ?? 0m);

            // Aşama dağılımı
            var stageDagilim = data
                .GroupBy(d => d.OpportunityStageName ?? "Bilinmiyor")
                .Select(g => new { asama = g.Key, adet = g.Count() })
                .OrderByDescending(x => x.adet)
                .ToList();

            // Anlaşma tipi dağılımı
            var dealTypeDagilim = data
                .GroupBy(d => d.DealTypeTR ?? d.DealType ?? "Bilinmiyor")
                .Select(g => new { tip = g.Key, adet = g.Count() })
                .OrderByDescending(x => x.adet)
                .ToList();

            // Aylık trend (CloseDate bazlı)
            var aylikTrend = data
                .Where(d => d.CloseDate.HasValue)
                .GroupBy(d => new { d.CloseDate!.Value.Year, d.CloseDate.Value.Month })
                .Select(g => new
                {
                    ay = $"{g.Key.Year}-{g.Key.Month:D2}",
                    toplam = g.Count(),
                    won = g.Count(d => d.OpportunityStageName == "Won"),
                    lost = g.Count(d => d.OpportunityStageName == "Lost" || (d.OpportunityStageName != null && d.OpportunityStageName.Contains("Closed")))
                })
                .OrderBy(x => x.ay)
                .ToList();

            // Geçen dönem fatura (karşılaştırma)
            var prevDuration = end - start;
            var prevStart = start.AddDays(-prevDuration.TotalDays);
            var prevEnd = start.AddSeconds(-1);
            var prevDonemFirsatIds = await db.TBLSOS_VARUNA_FIRSAT_ODATAs.AsNoTracking()
                .Where(o => o.CloseDate.HasValue && o.CloseDate.Value >= prevStart && o.CloseDate.Value <= prevEnd)
                .Select(o => o.Id).ToListAsync();
            var prevGuidSet = prevDonemFirsatIds.Where(id => Guid.TryParse(id, out _)).Select(id => Guid.Parse(id)).ToHashSet();
            var prevTeklifIds = await db.TBL_VARUNA_TEKLIFs.AsNoTracking()
                .Where(t => t.DeletedOn == null && t.OpportunityId.HasValue && prevGuidSet.Contains(t.OpportunityId.Value))
                .Select(t => t.Id.ToString()).ToListAsync();
            var prevTeklifIdSet = prevTeklifIds.ToHashSet();
            var gecenDonemFatura = await db.TBL_VARUNA_SIPARIs.AsNoTracking()
                .Where(s => s.OrderStatus == "Closed" && s.InvoiceDate.HasValue
                    && s.InvoiceDate.Value >= prevStart && s.InvoiceDate.Value <= prevEnd
                    && s.QuoteId != null && prevTeklifIdSet.Contains(s.QuoteId))
                .SumAsync(s => s.TotalNetAmount ?? 0m);

            return Json(new
            {
                // Kart 1: Tüm fırsatlar (havuz)
                tumFirsatAdet,
                tumFirsatTutar,
                firsatsizTeklifAdet,
                firsatsizTeklifTutar,
                gecenDonemFatura,
                // Kart 2: Dönem fırsat potansiyeli
                toplam,
                aktif = activeList.Count,
                won = wonList.Count,
                lost = lostList.Count,
                kazanmaOrani,
                toplamTutar = toplamFirsatTutar,
                wonTutar,
                lostTutar,
                aktivTutar,
                kazanmaOraniRevenue,
                // Kart 3: Dönem teklif (fırsata bağlı)
                teklifToplam,
                teklifTutar,
                // Potansiyel (teklif × fırsat olasılığı)
                agirlikliPotansiyel,
                ortOlasilik = Math.Round(ortOlasilik, 1),
                // Kart 4: Dönem sipariş (teklife bağlı)
                toplamSiparisAdet,
                toplamSiparisTutar,
                acikSiparisAdet,
                acikSiparisTutar,
                // Kart 5: Faturalanan (kapalı sipariş)
                kapaliSiparisAdet,
                kapaliSiparisTutar,
                // Hedef (DB'den)
                hedefTutar = await GetDonemHedefAsync(start, end),
                // Detaylar
                stageDagilim,
                dealTypeDagilim,
                aylikTrend
            });
        }

        // ───────────────────────────────────────────────────────────────
        // GET /FirsatAnaliz/GetOwnerPerformance — Satış temsilcisi bazlı
        // ───────────────────────────────────────────────────────────────
        [HttpGet]
        public async Task<IActionResult> GetOwnerPerformance(string? filter, string? startDate, string? endDate)
        {
            using var db = _contextFactory.CreateDbContext();
            var (start, end, _, _) = ParseFilter(filter, startDate, endDate);
            var ownerMap = await GetOwnerMapAsync();

            // TBLSOS_VARUNA_FIRSAT_ODATA — CloseDate bazlı
            var data = await db.TBLSOS_VARUNA_FIRSAT_ODATAs.AsNoTracking()
                .Where(o => o.CloseDate.HasValue
                    && o.CloseDate.Value >= start && o.CloseDate.Value <= end
                    && o.OwnerId != null)
                .Select(o => new { o.OwnerId, o.OpportunityStageName, AmountAmount = o.AmountValue })
                .ToListAsync();

            var performance = data
                .GroupBy(d => d.OwnerId!)
                .Select(g =>
                {
                    var total = g.Count();
                    var won = g.Count(d => d.OpportunityStageName == "Won");
                    var lost = g.Count(d => d.OpportunityStageName == "Lost"
                        || (d.OpportunityStageName != null && d.OpportunityStageName.Contains("Closed")));
                    var active = total - won - lost;
                    var winRate = (won + lost) > 0
                        ? Math.Round((decimal)won / (won + lost) * 100, 1) : 0m;

                    var toplamTutar = g.Sum(d => d.AmountAmount ?? 0m);
                    var wonTutar = g.Where(d => d.OpportunityStageName == "Won").Sum(d => d.AmountAmount ?? 0m);

                    return new
                    {
                        ownerId = g.Key,
                        adSoyad = ResolveOwnerName(g.Key, ownerMap),
                        toplam = total,
                        aktif = active,
                        won,
                        lost,
                        kazanmaOrani = winRate,
                        toplamTutar,
                        wonTutar
                    };
                })
                .OrderByDescending(x => x.wonTutar)
                .ThenByDescending(x => x.toplam)
                .ToList();

            return Json(performance);
        }

        // ───────────────────────────────────────────────────────────────
        // GET /FirsatAnaliz/GetOwnerFilterOptions — Filtre dropdown
        // ───────────────────────────────────────────────────────────────
        [HttpGet]
        public async Task<IActionResult> GetOwnerFilterOptions()
        {
            using var db = _contextFactory.CreateDbContext();
            var ownerMap = await GetOwnerMapAsync();

            var owners = await db.TBLSOS_VARUNA_FIRSAT_ODATAs.AsNoTracking()
                .Where(o => o.OwnerId != null)
                .GroupBy(o => o.OwnerId!)
                .Select(g => new { ownerId = g.Key, adet = g.Count() })
                .OrderByDescending(x => x.adet)
                .ToListAsync();

            var result = owners.Select(o => new
            {
                o.ownerId,
                adSoyad = ResolveOwnerName(o.ownerId, ownerMap),
                o.adet
            }).ToList();

            return Json(result);
        }

        // ───────────────────────────────────────────────────────────────
        // GET /FirsatAnaliz/GetOpportunityDetail — Detay listesi
        // ───────────────────────────────────────────────────────────────
        [HttpGet]
        public async Task<IActionResult> GetOpportunityDetail(string? filter, string? startDate, string? endDate,
            string? owner, string? stage, string? customer, string? product, int page = 1, int pageSize = 20)
        {
            using var db = _contextFactory.CreateDbContext();
            var (start, end, _, _) = ParseFilter(filter, startDate, endDate);
            var ownerMap = await GetOwnerMapAsync();

            // TBLSOS_VARUNA_FIRSAT_ODATA — CloseDate bazlı filtreleme
            var query = db.TBLSOS_VARUNA_FIRSAT_ODATAs.AsNoTracking()
                .Where(o => o.CloseDate.HasValue
                    && o.CloseDate.Value >= start && o.CloseDate.Value <= end);

            if (!string.IsNullOrEmpty(owner))
                query = query.Where(o => o.OwnerId == owner);
            if (!string.IsNullOrEmpty(stage))
                query = query.Where(o => o.OpportunityStageName == stage);

            // Müşteri filtresi — Teklif.Account_Title üzerinden fırsat ID'lerini bul
            if (!string.IsNullOrEmpty(customer))
            {
                var customerOppIds = await db.TBL_VARUNA_TEKLIFs.AsNoTracking()
                    .Where(t => t.DeletedOn == null && t.Account_Title == customer && t.OpportunityId.HasValue)
                    .Select(t => t.OpportunityId!.Value.ToString())
                    .Distinct().ToListAsync();
                var custIdSet = customerOppIds.ToHashSet();
                query = query.Where(o => custIdSet.Contains(o.Id));
            }

            // Ürün filtresi — ProductGroupId üzerinden (eski opportunities tablosu)
            if (!string.IsNullOrEmpty(product))
            {
                var productOppIds = await db.Database.SqlQueryRaw<string>(
                    @"SELECT o.Id FROM TBL_VARUNA_OPPORTUNITIES o
                      JOIN TBL_VARUNA_PRODUCTGRUPS pg ON o.ProductGroupId = CAST(pg.Id AS NVARCHAR(64))
                      WHERE pg.Name = {0} AND o.DeletedOn IS NULL", product).ToListAsync();
                var prodIdSet = productOppIds.ToHashSet();
                query = query.Where(o => prodIdSet.Contains(o.Id));
            }

            var total = await query.CountAsync();
            var items = await query
                .OrderByDescending(o => o.CloseDate)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(o => new
                {
                    o.Name,
                    o.OpportunityStageName,
                    DealTypeTR = o.DealType,
                    o.Probability,
                    o.CloseDate,
                    o.AmountValue,
                    ownerId = o.OwnerId,
                    o.Source
                })
                .ToListAsync();

            // Müşteri isimleri: Fırsat Id → Teklif.OpportunityId → Account_Title
            // SQL JOIN ile çöz — Guid case farkı sorun yaratmasın
            var oppNames = items.Select(i => i.Name).Where(n => n != null).Distinct().ToList();
            var nameMusteriMap = new Dictionary<string, string>();
            if (oppNames.Count > 0)
            {
                // Fırsat Id'lerini al
                var oppIdPairs = await db.TBLSOS_VARUNA_FIRSAT_ODATAs.AsNoTracking()
                    .Where(o => oppNames.Contains(o.Name))
                    .Select(o => new { o.Name, o.Id })
                    .ToListAsync();
                var oppIdList = oppIdPairs.Select(x => x.Id).Where(id => id != null).Distinct().ToList();
                // Teklif tablosundan müşteri ismi (LOWER ile case-insensitive eşleşme)
                var teklifMusteri = await db.TBL_VARUNA_TEKLIFs.AsNoTracking()
                    .Where(t => t.DeletedOn == null && t.OpportunityId.HasValue && t.Account_Title != null)
                    .Select(t => new { OppId = t.OpportunityId!.Value.ToString().ToLower(), t.Account_Title })
                    .ToListAsync();
                var musteriLookup = teklifMusteri
                    .GroupBy(t => t.OppId)
                    .ToDictionary(g => g.Key, g => g.First().Account_Title ?? "");
                foreach (var p in oppIdPairs)
                {
                    if (p.Name != null && p.Id != null && musteriLookup.TryGetValue(p.Id.ToLower(), out var musteri))
                        nameMusteriMap.TryAdd(p.Name, musteri);
                }
            }

            var result = items.Select(i => new
            {
                i.Name,
                asama = i.OpportunityStageName,
                anlasmaTipi = i.DealTypeTR,
                olasilik = i.Probability,
                tutar = i.AmountValue,
                kapanisTarihi = i.CloseDate?.ToString("dd.MM.yyyy"),
                satisTemsilcisi = ResolveOwnerName(i.ownerId, ownerMap),
                kaynak = i.Source,
                musteri = i.Name != null && nameMusteriMap.TryGetValue(i.Name, out var m) ? m : ""
            }).ToList();

            return Json(new { total, page, pageSize, items = result });
        }

        // ───────────────────────────────────────────────────────────────
        // GET /FirsatAnaliz/GetFunnelBreakdown?filter=month&funnel=4
        // Huni kartına tıklandığında: ürün + owner dağılımı
        // funnel: 1=tüm fırsatlar, 2=dönem fırsatlar, 3=teklifler, 4=siparişler, 5=faturalanan
        // ───────────────────────────────────────────────────────────────
        [HttpGet]
        public async Task<IActionResult> GetFunnelBreakdown(string? filter, string? startDate, string? endDate, int funnel = 2)
        {
            using var db = _contextFactory.CreateDbContext();
            var (start, end, _, _) = ParseFilter(filter, startDate, endDate);
            var ownerMap = await GetOwnerMapAsync();
            var eslestirmeMap = await GetUrunEslestirmeMapAsync();

            // Dönem fırsatlarını al (CloseDate bazlı)
            var donemFirsatIds = await db.TBLSOS_VARUNA_FIRSAT_ODATAs.AsNoTracking()
                .Where(o => o.CloseDate.HasValue && o.CloseDate.Value >= start && o.CloseDate.Value <= end)
                .Select(o => o.Id)
                .ToListAsync();
            var donemGuidSet = donemFirsatIds.Where(id => Guid.TryParse(id, out _)).Select(id => Guid.Parse(id)).ToHashSet();

            // Dönem teklifleri (fırsata bağlı)
            var donemTeklifler = await db.TBL_VARUNA_TEKLIFs.AsNoTracking()
                .Where(t => t.DeletedOn == null && t.OpportunityId.HasValue && donemGuidSet.Contains(t.OpportunityId.Value))
                .Select(t => new { t.Id, t.ProposalOwnerId, t.TotalNetAmountLocalCurrency_Amount, t.Status })
                .ToListAsync();
            var donemTeklifIdSet = donemTeklifler.Select(t => t.Id.ToString()).ToHashSet();

            // Dönem siparişleri (teklife bağlı) + ürün detayları
            var donemSiparisler = await db.TBL_VARUNA_SIPARIs.AsNoTracking()
                .Where(s => s.QuoteId != null && donemTeklifIdSet.Contains(s.QuoteId))
                .Select(s => new { s.OrderId, s.AccountTitle, s.TotalNetAmount, s.OrderStatus, s.ProposalOwnerId, s.InvoiceDate })
                .ToListAsync();

            var orderIds = donemSiparisler.Select(s => s.OrderId).Where(o => o != null).Distinct().ToList();
            var siparisUrunleri = await db.TBL_VARUNA_SIPARIS_URUNLERIs.AsNoTracking()
                .Where(u => u.CrmOrderId != null && orderIds.Contains(u.CrmOrderId))
                .Select(u => new { u.CrmOrderId, u.StockCode, u.ProductName, Total = u.Total ?? 0m })
                .ToListAsync();

            // Owner dağılımı: funnel'a göre
            object ownerBreakdown;
            object productBreakdown;

            if (funnel <= 2)
            {
                // Owner: OData tablosundan
                var firsatOdata = funnel == 1
                    ? db.TBLSOS_VARUNA_FIRSAT_ODATAs.AsNoTracking()
                    : db.TBLSOS_VARUNA_FIRSAT_ODATAs.AsNoTracking()
                        .Where(o => o.CloseDate.HasValue && o.CloseDate.Value >= start && o.CloseDate.Value <= end);

                var firsatOwnerData = await firsatOdata
                    .Where(o => o.OwnerId != null)
                    .Select(o => new { o.OwnerId, o.AmountValue })
                    .ToListAsync();

                ownerBreakdown = firsatOwnerData.GroupBy(d => d.OwnerId!)
                    .Select(g => new { adSoyad = ResolveOwnerName(g.Key, ownerMap), tutar = g.Sum(d => d.AmountValue ?? 0m), adet = g.Count() })
                    .OrderByDescending(x => x.tutar).Take(10).ToList();

                // Ürün: Raw SQL — ProductGroupId → ProductGrups.Name
                var urunSql = funnel == 1
                    ? @"SELECT pg.Name as UrunGrubu, COUNT(*) as Adet, SUM(ISNULL(fo.AmountValue,0)) as Tutar
                        FROM TBL_VARUNA_OPPORTUNITIES o
                        JOIN TBL_VARUNA_PRODUCTGRUPS pg ON o.ProductGroupId = CAST(pg.Id AS NVARCHAR(64))
                        LEFT JOIN TBLSOS_VARUNA_FIRSAT_ODATA fo ON o.Id = fo.Id
                        WHERE o.DeletedOn IS NULL AND o.ProductGroupId IS NOT NULL
                        GROUP BY pg.Name ORDER BY Tutar DESC"
                    : @"SELECT pg.Name as UrunGrubu, COUNT(*) as Adet, SUM(ISNULL(fo.AmountValue,0)) as Tutar
                        FROM TBL_VARUNA_OPPORTUNITIES o
                        JOIN TBL_VARUNA_PRODUCTGRUPS pg ON o.ProductGroupId = CAST(pg.Id AS NVARCHAR(64))
                        LEFT JOIN TBLSOS_VARUNA_FIRSAT_ODATA fo ON o.Id = fo.Id
                        WHERE o.DeletedOn IS NULL AND o.ProductGroupId IS NOT NULL
                          AND o.CloseDate >= @p0 AND o.CloseDate <= @p1
                        GROUP BY pg.Name ORDER BY Tutar DESC";

                List<FirsatUrunGrupDto> firsatUrunResult;
                if (funnel == 1)
                    firsatUrunResult = await db.Database.SqlQueryRaw<FirsatUrunGrupDto>(urunSql).ToListAsync();
                else
                    firsatUrunResult = await db.Database.SqlQueryRaw<FirsatUrunGrupDto>(urunSql, start, end).ToListAsync();

                productBreakdown = firsatUrunResult
                    .Select(x => new { urun = x.UrunGrubu ?? "Diğer", tutar = x.Tutar, adet = x.Adet })
                    .ToList();
            }
            else if (funnel == 3)
            {
                // Teklif bazlı — sadece gönderilmiş teklifler (Draft ve InReview hariç)
                var sentTeklifler = donemTeklifler
                    .Where(t => t.Status != null && t.Status != "Draft" && t.Status != "InReview")
                    .ToList();

                ownerBreakdown = sentTeklifler
                    .Where(t => t.ProposalOwnerId.HasValue)
                    .GroupBy(t => t.ProposalOwnerId!.Value.ToString())
                    .Select(g => new { adSoyad = ResolveOwnerName(g.Key, ownerMap), tutar = g.Sum(t => t.TotalNetAmountLocalCurrency_Amount ?? 0m), adet = g.Count() })
                    .OrderByDescending(x => x.tutar).Take(10).ToList();

                // Ürün: gönderilmiş teklif ürünlerinden
                var sentTeklifIds = sentTeklifler.Select(t => t.Id).ToHashSet();
                var teklifUrunleri = await db.TBL_VARUNA_TEKLIF_URUNLERIs.AsNoTracking()
                    .Where(u => u.DeletedOn == null && u.QuoteId.HasValue && sentTeklifIds.Contains(u.QuoteId.Value))
                    .Select(u => new { u.StockCode, Total = u.NetLineTotalAmountLocal_Amount ?? 0m })
                    .ToListAsync();

                productBreakdown = teklifUrunleri
                    .Select(u => new { urun = ResolveProductGroup(u.StockCode, eslestirmeMap), u.Total })
                    .GroupBy(x => x.urun)
                    .Select(g => new { urun = g.Key, tutar = g.Sum(x => x.Total), adet = g.Count() })
                    .OrderByDescending(x => x.tutar).ToList();
            }
            else
            {
                // Sipariş bazlı — dönemden ÖNCE faturlananları hariç tut
                var filteredSiparisler = funnel == 5
                    // Satış faturası: sadece InvoiceDate dönem içinde
                    ? donemSiparisler.Where(s => s.OrderStatus == "Closed"
                        && s.InvoiceDate.HasValue
                        && s.InvoiceDate.Value >= start && s.InvoiceDate.Value <= end).ToList()
                    // Sipariş kartı: dönemden ÖNCE faturlananları düş
                    : donemSiparisler.Where(s =>
                        !s.InvoiceDate.HasValue || s.InvoiceDate.Value >= start).ToList();

                // Owner: sipariş bazında (AccountTitle'dan veya teklifteki owner'dan)
                var siparisTeklifOwner = donemTeklifler.ToDictionary(t => t.Id.ToString(), t => t.ProposalOwnerId?.ToString());
                var siparisOwnerMap = new Dictionary<string, string>();
                foreach (var s in donemSiparisler)
                {
                    // Sipariş'in teklifi üzerinden owner
                    // QuoteId → teklifId → ProposalOwnerId
                }

                ownerBreakdown = filteredSiparisler
                    .Where(s => s.AccountTitle != null)
                    .GroupBy(s => s.AccountTitle!)
                    .Select(g => new { adSoyad = g.Key, tutar = g.Sum(s => s.TotalNetAmount ?? 0m), adet = g.Count() })
                    .OrderByDescending(x => x.tutar).Take(10).ToList();

                // Ürün: sipariş ürünlerinden StockCode → AnaUrun
                var filteredOrderIds = filteredSiparisler.Select(s => s.OrderId).Where(o => o != null).ToHashSet();
                productBreakdown = siparisUrunleri
                    .Where(u => filteredOrderIds.Contains(u.CrmOrderId))
                    .Select(u => new { urun = ResolveProductGroup(u.StockCode, eslestirmeMap), u.Total })
                    .GroupBy(x => x.urun)
                    .Select(g => new { urun = g.Key, tutar = g.Sum(x => x.Total), adet = g.Count() })
                    .OrderByDescending(x => x.tutar).ToList();
            }

            // Müşteri verisi — funnel'a göre
            object customerBreakdown;
            if (funnel <= 2)
            {
                // Fırsat bazlı müşteri — eski tablodan AccountTitle (doğrudan fırsat → teklif → Account_Title)
                var firsatMusteriSql = await db.Database.SqlQueryRaw<FirsatMusteriDto>(
                    funnel == 1
                    ? @"SELECT TOP 10 Musteri, COUNT(*) as Adet, SUM(AmountValue) as Tutar FROM (
                        SELECT t.Account_Title as Musteri, fo.Id, fo.AmountValue
                        FROM TBL_VARUNA_TEKLIF t
                        JOIN TBLSOS_VARUNA_FIRSAT_ODATA fo ON CAST(t.OpportunityId AS NVARCHAR(64)) = fo.Id
                        WHERE t.DeletedOn IS NULL AND t.Account_Title IS NOT NULL
                        GROUP BY t.Account_Title, fo.Id, fo.AmountValue
                      ) x GROUP BY Musteri ORDER BY SUM(AmountValue) DESC"
                    : @"SELECT TOP 10 Musteri, COUNT(*) as Adet, SUM(AmountValue) as Tutar FROM (
                        SELECT t.Account_Title as Musteri, fo.Id, fo.AmountValue
                        FROM TBL_VARUNA_TEKLIF t
                        JOIN TBLSOS_VARUNA_FIRSAT_ODATA fo ON CAST(t.OpportunityId AS NVARCHAR(64)) = fo.Id
                        WHERE t.DeletedOn IS NULL AND t.Account_Title IS NOT NULL
                          AND fo.CloseDate >= {0} AND fo.CloseDate <= {1}
                        GROUP BY t.Account_Title, fo.Id, fo.AmountValue
                      ) x GROUP BY Musteri ORDER BY SUM(AmountValue) DESC",
                    start, end).ToListAsync();

                customerBreakdown = firsatMusteriSql
                    .Select(x => new { musteri = x.Musteri ?? "Bilinmiyor", tutar = x.Tutar, adet = x.Adet })
                    .ToList();
            }
            else if (funnel == 3)
            {
                // Teklif bazlı müşteri — gönderilmiş teklifler
                customerBreakdown = donemTeklifler
                    .Where(t => t.Status != null && t.Status != "Draft" && t.Status != "InReview")
                    .Join(await db.TBL_VARUNA_TEKLIFs.AsNoTracking()
                        .Where(t => t.DeletedOn == null && t.Account_Title != null)
                        .Select(t => new { t.Id, t.Account_Title })
                        .ToListAsync(),
                        dt => dt.Id, ft => ft.Id, (dt, ft) => new { ft.Account_Title, dt.TotalNetAmountLocalCurrency_Amount })
                    .GroupBy(x => x.Account_Title!)
                    .Select(g => new { musteri = g.Key, tutar = g.Sum(x => x.TotalNetAmountLocalCurrency_Amount ?? 0m), adet = g.Count() })
                    .OrderByDescending(x => x.tutar).Take(10).ToList();
            }
            else
            {
                var fSiparisler = funnel == 5
                    ? donemSiparisler.Where(s => s.OrderStatus == "Closed"
                        && s.InvoiceDate.HasValue && s.InvoiceDate.Value >= start && s.InvoiceDate.Value <= end).ToList()
                    : donemSiparisler;
                customerBreakdown = fSiparisler
                    .Where(s => s.AccountTitle != null)
                    .GroupBy(s => s.AccountTitle!)
                    .Select(g => new { musteri = g.Key, tutar = g.Sum(s => s.TotalNetAmount ?? 0m), adet = g.Count() })
                    .OrderByDescending(x => x.tutar).Take(10).ToList();
            }

            return Json(new { ownerBreakdown, productBreakdown, customerBreakdown, funnel });
        }
    }
}
