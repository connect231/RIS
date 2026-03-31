using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using SOS.DbData;
using Microsoft.EntityFrameworkCore;
using SOS.Models.ViewModels;

namespace SOS.Controllers
{
    [Authorize]
    public class CockpitController : Controller
    {
        private readonly MskDbContext _context;
        private const decimal AYLIK_HEDEF = 45_000_000m;

        public CockpitController(MskDbContext context)
        {
            _context = context;
        }

        private (DateTime start, DateTime end, string filter, int months) ParseFilter(string? filter, string? startDate, string? endDate)
        {
            var now = DateTime.Now;
            var today = now.Date.AddHours(23).AddMinutes(59).AddSeconds(59);
            var year = now.Year;
            DateTime start;
            DateTime end;
            int months;
            var fmtP = System.Globalization.CultureInfo.InvariantCulture;
            var style = System.Globalization.DateTimeStyles.None;

            // Manuel aralık (öncelikli)
            if (!string.IsNullOrEmpty(startDate) && !string.IsNullOrEmpty(endDate)
                && DateTime.TryParseExact(startDate, "yyyy-MM-dd", fmtP, style, out var sd)
                && DateTime.TryParseExact(endDate, "yyyy-MM-dd", fmtP, style, out var ed))
            {
                filter = "range";
                start = sd.Date;
                end = ed.Date.AddHours(23).AddMinutes(59).AddSeconds(59);
                months = Math.Max(1, (end.Year - start.Year) * 12 + end.Month - start.Month + 1);
                return (start, end, filter, months);
            }

            switch (filter?.ToLowerInvariant())
            {
                case "ytd":
                    start = new DateTime(year, 1, 1);
                    end = today;
                    months = now.Month;
                    break;
                case "q1": // Ocak - Nisan
                    start = new DateTime(year, 1, 1);
                    end = new DateTime(year, 4, 30, 23, 59, 59);
                    if (end > today) end = today;
                    months = 4;
                    break;
                case "q2": // Mayıs - Ağustos
                    start = new DateTime(year, 5, 1);
                    end = new DateTime(year, 8, 31, 23, 59, 59);
                    if (end > today) end = today;
                    months = 4;
                    break;
                case "q3": // Eylül - Aralık
                    start = new DateTime(year, 9, 1);
                    end = new DateTime(year, 12, 31, 23, 59, 59);
                    if (end > today) end = today;
                    months = 4;
                    break;
                default: // "month" veya null → Bulunduğumuz ay (default)
                    filter = "month";
                    start = new DateTime(year, now.Month, 1);
                    end = today;
                    months = 1;
                    break;
            }

            return (start, end, filter ?? "month", months);
        }

        private static bool IsFatura(string? durum)
            => string.IsNullOrWhiteSpace(durum)
               || durum.Trim().Equals("İADE", StringComparison.OrdinalIgnoreCase)
               || durum.Trim().Equals("IADE", StringComparison.OrdinalIgnoreCase);

        private static bool IsTahsilat(string? durum)
            => !string.IsNullOrWhiteSpace(durum)
               && durum.Trim().Equals("TAHSİL EDİLDİ", StringComparison.OrdinalIgnoreCase);

        public async Task<IActionResult> Index(string? filter, string? startDate, string? endDate)
        {
            var (start, end, activeFilter, months) = ParseFilter(filter, startDate, endDate);

            // Faturalar: Fatura_Tarihi bazlı filtreleme
            var allFaturalar = await _context.VIEW_CP_EXCEL_FATURAs
                .Where(f => f.Fatura_Tarihi.HasValue
                         && f.Fatura_Tarihi.Value >= start
                         && f.Fatura_Tarihi.Value <= end)
                .ToListAsync();

            var faturalar = allFaturalar.Where(f => IsFatura(f.Durum)).ToList();

            // Tahsilatlar: Fatura_Vade_Tarihi bazlı filtreleme
            var allTahsilatKayitlari = await _context.VIEW_CP_EXCEL_FATURAs
                .Where(f => f.Fatura_Vade_Tarihi.HasValue
                         && f.Fatura_Vade_Tarihi.Value >= start
                         && f.Fatura_Vade_Tarihi.Value <= end)
                .ToListAsync();

            var tahsilatlar = allTahsilatKayitlari.Where(f => IsTahsilat(f.Durum)).ToList();

            // Sözleşmeler (Archived — tarih bağımsız)
            var sozlesmeler = await _context.TBL_VARUNA_SOZLESMEs
                .Where(s => s.ContractStatus == "Archived")
                .ToListAsync();

            // Trend: önceki eşdeğer dönem
            var prevDuration = end - start;
            var prevStart = start.AddDays(-prevDuration.TotalDays);
            var prevEnd = start.AddSeconds(-1);

            var prevFaturalar = await _context.VIEW_CP_EXCEL_FATURAs
                .Where(f => f.Fatura_Tarihi.HasValue
                         && f.Fatura_Tarihi.Value >= prevStart
                         && f.Fatura_Tarihi.Value <= prevEnd)
                .ToListAsync();

            var prevTahsilatlar = await _context.VIEW_CP_EXCEL_FATURAs
                .Where(f => f.Fatura_Vade_Tarihi.HasValue
                         && f.Fatura_Vade_Tarihi.Value >= prevStart
                         && f.Fatura_Vade_Tarihi.Value <= prevEnd)
                .ToListAsync();

            var prevFatToplam = prevFaturalar.Where(f => IsFatura(f.Durum)).Sum(f => f.Fatura_Toplam ?? 0);
            var prevTahToplam = prevTahsilatlar.Where(f => IsTahsilat(f.Durum)).Sum(f => f.Fatura_Toplam ?? 0);

            var fatToplam = faturalar.Sum(f => f.Fatura_Toplam ?? 0);
            var tahToplam = tahsilatlar.Sum(f => f.Fatura_Toplam ?? 0);
            var sozToplam = sozlesmeler.Sum(s => s.TotalAmount ?? 0);

            // Dinamik hedef: aylık 45M × ay sayısı
            var donemHedef = AYLIK_HEDEF * months;
            var hedefKalan = Math.Max(donemHedef - fatToplam, 0);
            var hedefYuzde = donemHedef > 0
                ? Math.Round(Math.Min(fatToplam / donemHedef * 100, 100), 1)
                : 0;

            var vm = new CockpitViewModel
            {
                FaturalarToplam = fatToplam,
                FaturalarAdet = faturalar.Count,
                TahsilatlarToplam = tahToplam,
                TahsilatlarAdet = tahsilatlar.Count,
                SozlesmelerToplam = sozToplam,
                SozlesmelerAdet = sozlesmeler.Count,
                FaturalarTrend = prevFatToplam > 0 ? Math.Round((fatToplam - prevFatToplam) / prevFatToplam * 100, 1) : 0,
                TahsilatlarTrend = prevTahToplam > 0 ? Math.Round((tahToplam - prevTahToplam) / prevTahToplam * 100, 1) : 0,
                SozlesmelerTrend = 0,
                AylikHedef = donemHedef,
                HedefGerceklesme = fatToplam,
                HedefKalan = hedefKalan,
                HedefYuzde = hedefYuzde,
                HedefAySayisi = months,
                AktifFiltre = activeFilter,
                FiltreBaslangic = start,
                FiltreBitis = end,
                FaturaDetaylari = faturalar.OrderByDescending(f => f.Fatura_Tarihi).ToList(),
                TahsilatDetaylari = tahsilatlar.OrderByDescending(f => f.Fatura_Vade_Tarihi).ToList(),
                SozlesmeDetaylari = sozlesmeler.OrderByDescending(s => s.TotalAmount).ToList()
            };

            return View(vm);
        }

        [HttpGet]
        public async Task<IActionResult> GetDailyBreakdown(string type, string? filter, string? startDate, string? endDate)
        {
            var (start, end, _, _) = ParseFilter(filter, startDate, endDate);

            switch (type?.ToLowerInvariant())
            {
                case "faturalar":
                {
                    var data = await _context.VIEW_CP_EXCEL_FATURAs
                        .Where(f => f.Fatura_Tarihi.HasValue
                                 && f.Fatura_Tarihi.Value >= start
                                 && f.Fatura_Tarihi.Value <= end)
                        .ToListAsync();

                    var daily = data.Where(f => IsFatura(f.Durum))
                        .GroupBy(f => f.Fatura_Tarihi!.Value.Date)
                        .Select(g => new { Tarih = g.Key.ToString("yyyy-MM-dd"), Toplam = g.Sum(x => x.Fatura_Toplam ?? 0), Adet = g.Count() })
                        .OrderBy(x => x.Tarih).ToList();
                    return Json(daily);
                }
                case "tahsilatlar":
                {
                    var data = await _context.VIEW_CP_EXCEL_FATURAs
                        .Where(f => f.Fatura_Vade_Tarihi.HasValue
                                 && f.Fatura_Vade_Tarihi.Value >= start
                                 && f.Fatura_Vade_Tarihi.Value <= end)
                        .ToListAsync();

                    var daily = data.Where(f => IsTahsilat(f.Durum))
                        .GroupBy(f => f.Fatura_Vade_Tarihi!.Value.Date)
                        .Select(g => new { Tarih = g.Key.ToString("yyyy-MM-dd"), Toplam = g.Sum(x => x.Fatura_Toplam ?? 0), Adet = g.Count() })
                        .OrderBy(x => x.Tarih).ToList();
                    return Json(daily);
                }
                case "sozlesmeler":
                {
                    var data = await _context.TBL_VARUNA_SOZLESMEs
                        .Where(s => s.ContractStatus == "Archived")
                        .ToListAsync();

                    var daily = data.Where(s => s.CreatedOn.HasValue)
                        .GroupBy(s => s.CreatedOn!.Value.Date)
                        .Select(g => new { Tarih = g.Key.ToString("yyyy-MM-dd"), Toplam = g.Sum(x => x.TotalAmount ?? 0), Adet = g.Count() })
                        .OrderBy(x => x.Tarih).ToList();
                    return Json(daily);
                }
                default:
                    return BadRequest(new { error = "Geçersiz tip" });
            }
        }
    }
}
