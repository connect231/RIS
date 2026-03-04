using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using System.Security.Claims;
using UniCP.DbData;
using UniCP.Models.Talepler;
using UniCP.Models.MsK.SpModels;
using UniCP.Models.MsK;
using Microsoft.EntityFrameworkCore;
using System.IO;
using Microsoft.Extensions.Caching.Memory;
using UniCP.Services;

namespace UniCP.Controllers
{
    [Authorize(Roles = UniCP.Constants.AppConstants.Roles.Raporlar + "," + UniCP.Constants.AppConstants.Roles.Admin)]
    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public class RaporlarController : Controller
    {
        private readonly MskDbContext _mskDb;
        private readonly UniCP.Models.IEmailService _emailService;
        private readonly Microsoft.Extensions.Caching.Memory.IMemoryCache _cache;
        private readonly ICompanyResolutionService _companyResolution;
        private readonly IUrlEncryptionService _urlEncryption;

        public RaporlarController(
            MskDbContext mskDb, 
            UniCP.Models.IEmailService emailService, 
            Microsoft.Extensions.Caching.Memory.IMemoryCache cache,
            ICompanyResolutionService companyResolution,
            IUrlEncryptionService urlEncryption)
        {
            _mskDb = mskDb;
            _emailService = emailService;
            _cache = cache;
            _companyResolution = companyResolution;
            _urlEncryption = urlEncryption;
        }

        public IActionResult Index()
        {
            return View();
        }

        public async Task<IActionResult> Gelistirme(string period = "1m", DateTime? baslangic = null, DateTime? bitis = null, string status = null, string? filteredCompanyId = null)
        {
            var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);

            // Decrypt Company ID
            int? decryptedCompanyId = _urlEncryption.DecryptId(filteredCompanyId);
           
            // Auto-Migration for TRHKAYIT Column (Emergency fix for Report View)
            try
            {
               _mskDb.Database.ExecuteSqlRaw("IF NOT EXISTS(SELECT 1 FROM sys.columns WHERE Name = N'TRHKAYIT' AND Object_ID = Object_ID(N'TBL_TALEP')) BEGIN ALTER TABLE TBL_TALEP ADD TRHKAYIT DATETIME NULL DEFAULT GETDATE(); END");
            } catch { /* Ignore permissions/errors */ }

            if (string.IsNullOrEmpty(userIdStr)) return RedirectToAction("Login", "Account");
            int userId = int.Parse(userIdStr);

            // Fetch Authorized Companies for Filter
            var kullanici = await _mskDb.TBL_KULLANICIs.FirstOrDefaultAsync(i => i.LNGIDENTITYKOD == userId);
            if (kullanici == null) return RedirectToAction("Login", "Account");

            // Use CompanyResolutionService
            var companyResolution = await _companyResolution.ResolveCompaniesAsync(
                kullanici.LNGKOD,
                decryptedCompanyId,
                HttpContext);

            // Handle cookie setting (Refactored to service)
            if (decryptedCompanyId.HasValue)
            {
                if (decryptedCompanyId.Value == -1)
                {
                    _companyResolution.ClearCompanyCookie(HttpContext);
                }
                else if (companyResolution.TargetCompanyIds.Contains(decryptedCompanyId.Value))
                {
                    _companyResolution.SetCompanyCookie(HttpContext, decryptedCompanyId.Value);
                }
            }

            ViewBag.AuthorizedCompanies = companyResolution.AuthorizedCompanies;
            ViewBag.SelectedCompanyId = companyResolution.SelectedCompanyId;

            // Fetch Data
            var (viewModels, startDate, endDate) = await GetDevelopmentRequestsAsync(userId, period, baslangic, bitis, filteredCompanyId);

            // Extract All Statuses for Dropdown (Before Filtering)
            var allStatuses = viewModels.Select(r => r.Status).Distinct().OrderBy(s => s).ToList();

            // Apply Status Filter
            if (!string.IsNullOrEmpty(status))
            {
                viewModels = viewModels.Where(r => r.Status == status).ToList();
            }

            // Prepare View Data
            ViewBag.CurrentPeriod = period;
            ViewBag.StartDate = startDate.ToString("yyyy-MM-dd");
            ViewBag.EndDate = endDate.ToString("yyyy-MM-dd");
            ViewBag.Status = status;
            ViewBag.AllStatuses = allStatuses;

            // Prepare Chart Data
            var statusCounts = viewModels
                .GroupBy(r => r.Status)
                .Select(g => new { Status = g.Key, Count = g.Count() })
                .ToList();

            ViewBag.ChartLabels = statusCounts.Select(s => s.Status).ToList();
            ViewBag.ChartData = statusCounts.Select(s => s.Count).ToList();

            return View(viewModels);
        }

        public async Task<IActionResult> ExportExcel(string period = "1m", DateTime? baslangic = null, DateTime? bitis = null, string? filteredCompanyId = null)
        {
            var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userIdStr)) return RedirectToAction("Login", "Account");
            int userId = int.Parse(userIdStr);

            var (viewModels, _, _) = await GetDevelopmentRequestsAsync(userId, period, baslangic, bitis, filteredCompanyId);
            var content = GenerateDevelopmentExcel(viewModels);
            var fileName = $"Gelistirme_Raporu_{DateTime.Now:yyyyMMdd_HHmm}.xlsx";

            return File(content, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", fileName);
        }

        [HttpPost]
        public async Task<IActionResult> SendDevelopmentReport(string email, string period = "1m", DateTime? baslangic = null, DateTime? bitis = null, string? filteredCompanyId = null)
        {
            var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userIdStr)) return Json(new { success = false, message = "Oturum bulunamadÄ±." });
            int userId = int.Parse(userIdStr);

            // Rate Limiting Check
            // Rate Limiting Check (Global per user, 60 seconds)
            string cacheKey = $"MailLimit_{userId}";
            if (_cache.TryGetValue(cacheKey, out object _))
            {
                return Json(new { success = false, message = "Ã‡ok fazla istek gÃ¶nderdiniz. LÃ¼tfen 1 dakika bekleyin." });
            }
            _cache.Set(cacheKey, true, TimeSpan.FromMinutes(1));

            var kullanici = _mskDb.TBL_KULLANICIs.FirstOrDefault(i => i.LNGIDENTITYKOD == userId);
            string firmaAdi = kullanici?.TXTFIRMAADI ?? "Firma";

            try
            {
                var (viewModels, _, _) = await GetDevelopmentRequestsAsync(userId, period, baslangic, bitis, filteredCompanyId);
                var content = GenerateDevelopmentExcel(viewModels);
                
                string subject = $"{firmaAdi} GeliÅŸtirme Talebi Raporu";
                string message = $@"
                    <h3>SayÄ±n Ä°lgili,</h3>
                    <p>{firmaAdi} firmasÄ±na ait geliÅŸtirme talebi raporu ektedir.</p>
                    <p>Ä°yi Ã§alÄ±ÅŸmalar dileriz.</p>
                ";

                await _emailService.SendEmailAsync(email, subject, message, content, $"Gelistirme_Raporu_{DateTime.Now:yyyyMMdd}.xlsx");

                // Set Cache Expiration (e.g., 5 minutes to prevent spam/abuse, or period duration)
                using (var entry = _cache.CreateEntry(cacheKey))
                {
                    entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5);
                    entry.Value = DateTime.Now;
                }

                return Json(new { success = true, message = $"Rapor baÅŸarÄ±yla {email} adresine gÃ¶nderildi." });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Mail gÃ¶nderilirken hata oluÅŸtu: " + ex.Message });
            }
        }

        private byte[] GenerateDevelopmentExcel(List<Request> viewModels)
        {
             using (var workbook = new ClosedXML.Excel.XLWorkbook())
            {
                var worksheet = workbook.Worksheets.Add("BasvuruListesi");
                
                // Headers
                var headers = new[] { "Firma", "Talep No", "BaÅŸlÄ±k", "Tarih", "Durum", "Ä°lerleme", "Planlanan PY/UAT", "Planlanan CanlÄ±", "Efor", "Maliyet" };
                for (int i = 0; i < headers.Length; i++)
                {
                    worksheet.Cell(1, i + 1).Value = headers[i];
                }

                // Styling Headers
                var headerRange = worksheet.Range(1, 1, 1, headers.Length);
                headerRange.Style.Font.Bold = true;
                headerRange.Style.Fill.BackgroundColor = ClosedXML.Excel.XLColor.LightGray;
                headerRange.Style.Alignment.Horizontal = ClosedXML.Excel.XLAlignmentHorizontalValues.Center;

                // Data Rows
                int row = 2;
                foreach (var item in viewModels)
                {
                    worksheet.Cell(row, 1).Value = item.Company ?? "";
                    worksheet.Cell(row, 2).Value = item.Id ?? "";
                    worksheet.Cell(row, 3).Value = item.Title ?? "";
                    worksheet.Cell(row, 4).Value = item.Date ?? "";
                    worksheet.Cell(row, 5).Value = item.Status ?? "";
                    worksheet.Cell(row, 6).Value = item.Progress; 
                    worksheet.Cell(row, 7).Value = item.PlanlananPyuat ?? "";
                    worksheet.Cell(row, 8).Value = item.PlanlananCanliTeslim ?? "";
                    worksheet.Cell(row, 9).Value = item.Effort ?? "";
                    worksheet.Cell(row, 10).Value = item.Cost ?? "";

                    worksheet.Cell(row, 6).Style.NumberFormat.Format = "0\\%";
                    row++;
                }

                worksheet.Columns().AdjustToContents();

                using (var stream = new System.IO.MemoryStream())
                {
                    workbook.SaveAs(stream);
                    return stream.ToArray();
                }
            }
        }

        private async Task<(List<Request> Requests, DateTime StartDate, DateTime EndDate)> GetDevelopmentRequestsAsync(int userId, string period, DateTime? baslangic, DateTime? bitis, string? filteredCompanyId = null)
        {
            int? decryptedCompanyId = _urlEncryption.DecryptId(filteredCompanyId);
            var kullanici = await _mskDb.TBL_KULLANICIs.FirstOrDefaultAsync(i => i.LNGIDENTITYKOD == userId);

            if (kullanici == null)
            {
                return (new List<Request>(), DateTime.Now, DateTime.Now);
            }
            
            string firmaAdi = kullanici.TXTFIRMAADI ?? "";

            // 1. Determine Target Companies
            var targetCompanies = new List<int>();
            int defaultFirmaKod = kullanici.LNGORTAKFIRMAKOD ?? 2;

            if (kullanici.LNGKULLANICITIPI == 3 || kullanici.LNGKULLANICITIPI == 1)
            {
                 var authorizedIndices = await _mskDb.TBL_KULLANICI_FIRMAs
                                     .Where(f => f.LNGKULLANICIKOD == kullanici.LNGKOD)
                                     .Select(f => f.LNGFIRMAKOD)
                                     .ToListAsync();

                 if (kullanici.LNGKULLANICITIPI == 1 && !authorizedIndices.Any()) authorizedIndices.Add(defaultFirmaKod);
                 
                 if (decryptedCompanyId.HasValue && authorizedIndices.Contains(decryptedCompanyId.Value))
                 {
                     targetCompanies.Add(decryptedCompanyId.Value);
                 }
                 else
                 {
                     var projectClaimInner = User.FindFirst("ProjectCode");
                     if (projectClaimInner != null && int.TryParse(projectClaimInner.Value, out int selectedPid) && authorizedIndices.Contains(selectedPid))
                     {
                         targetCompanies.Add(selectedPid);
                     }
                     else
                     {
                        targetCompanies = authorizedIndices;
                     }
                 }
            }

            // Fallback Logic
            var projectClaim = User.FindFirst("ProjectCode");
            if (!targetCompanies.Any() && projectClaim != null && int.TryParse(projectClaim.Value, out int selectedProject))
            {
                targetCompanies.Add(selectedProject);
            }
            else if (!targetCompanies.Any())
            {
                 targetCompanies.Add(defaultFirmaKod);
            }


            // 2. Fetch TFS Data (Aggregated - Parallel)
            var liveTfsRequests = new System.Collections.Concurrent.ConcurrentBag<SSP_TFS_GELISTIRME>();
            var scopeFactory = HttpContext.RequestServices.GetService<IServiceScopeFactory>();

            await Parallel.ForEachAsync(targetCompanies, new ParallelOptions { MaxDegreeOfParallelism = 10 }, async (code, ct) =>
            {
                using (var scope = scopeFactory.CreateScope())
                {
                    var db = scope.ServiceProvider.GetRequiredService<MskDbContext>();
                    try 
                    {
                        var tfs = await db.SP_TFS_GELISTIRMEAsync(Convert.ToInt16(code));
                        foreach(var item in tfs)
                        {
                            liveTfsRequests.Add(item);
                        }
                    }
                    catch (Exception ex) 
                    {
                         // Log error?
                         Console.WriteLine($"Error fetching TFS for company {code}: {ex.Message}");
                    }
                }
            });


            // Filtering Logic
            DateTime startDate;
            DateTime endDate = DateTime.Now;

            if (baslangic.HasValue)
            {
                startDate = baslangic.Value;
                if (bitis.HasValue)
                {
                     endDate = bitis.Value.Date.AddDays(1).AddTicks(-1);
                }
            }
            else
            {
                switch (period)
                {
                    case "1y":
                        startDate = DateTime.Now.AddYears(-1);
                        break;
                    case "3m":
                        startDate = DateTime.Now.AddMonths(-3);
                        break;
                    case "1m":
                    default:
                        startDate = DateTime.Now.AddMonths(-1);
                        break;
                }
            }

            var filteredTfs = liveTfsRequests
                .Where(tfs => !string.Equals(tfs.MADDEDURUM, "CLOSED", StringComparison.OrdinalIgnoreCase) &&
                              !string.Equals(tfs.MADDEDURUM, "CANCEL", StringComparison.OrdinalIgnoreCase) &&
                              !string.Equals(tfs.MADDEDURUM, "CANCELED", StringComparison.OrdinalIgnoreCase) &&
                              !string.Equals(tfs.MADDEDURUM, "SEND BACK", StringComparison.OrdinalIgnoreCase) &&
                              !string.Equals(tfs.MADDEDURUM, "SEND-BACK", StringComparison.OrdinalIgnoreCase) &&
                              !string.Equals(tfs.MADDEDURUM, "REJECTED", StringComparison.OrdinalIgnoreCase))
                  .Where(tfs => (tfs.ACILMATARIHI >= startDate && tfs.ACILMATARIHI <= endDate) || 
                               ((string.Equals(tfs.MADDEDURUM, "RESOLVED", StringComparison.OrdinalIgnoreCase) || string.Equals(tfs.MADDEDURUM, "RESOLVE", StringComparison.OrdinalIgnoreCase)) && tfs.DEGISTIRMETARIHI >= startDate && tfs.DEGISTIRMETARIHI <= endDate))
                .OrderByDescending(tfs => tfs.ACILMATARIHI)
                .ToList();

            // 3. Fetch Portal Data
            var tfsIds = liveTfsRequests.Select(t => t.TFSNO).Distinct().ToList();

            var portalRequests = _mskDb.TBL_TALEPs
                .Include(t => t.TBL_TALEP_NOTLARs)
                .Include(t => t.TBL_TALEP_FILEs)
                .Where(r => (r.LNGVARUNAKOD.HasValue && targetCompanies.Contains(r.LNGVARUNAKOD.Value)) 
                            || (r.LNGTFSNO.HasValue && tfsIds.Contains(r.LNGTFSNO.Value)))
                .ToList();

            var portalMap = portalRequests
                .Where(r => r.LNGTFSNO.HasValue && r.LNGTFSNO.Value > 0)
                .GroupBy(r => r.LNGTFSNO.Value)
                .ToDictionary(g => g.Key, g => g.First());
            
            // Helper to get company name if needed, but for now we use scope 'firmaAdi' or user's main company name? 
            // Actually 'firmaAdi' is just the User's company name. 
            // Better to show the specific company name for each request if user is Type 3?
            // For now, let's stick to the current design or maybe fetch company names? 
            // The VIEW/Model doesn't seem to have CompanyName per request easily without Join.
            // Let's use the 'firmaAdi' (User's company) as default but ideally we should show the request's company.

            // 4. Merge Data
            var viewModels = new List<Request>();

            foreach (var tfs in filteredTfs)
            {
                var id = "TFS-" + tfs.TFSNO;
                var baseStatus = "Analiz"; 
                var baseProgress = tfs.TAMAMLANMA_OARANI.HasValue ? (int)tfs.TAMAMLANMA_OARANI.Value : 0;

                var portalRecord = portalMap.ContainsKey(tfs.TFSNO) ? portalMap[tfs.TFSNO] : null;
                
                if (portalRecord != null)
                {
                    var latestLog = _mskDb.TBL_TALEP_AKIS_LOGs
                        .Where(l => l.LNGTALEPKOD == portalRecord.LNGKOD)
                        .OrderByDescending(l => l.TRHDURUMBASLANGIC)
                        .Include(l => l.LNGDURUMKODNavigation)
                        .FirstOrDefault();

                    if (latestLog != null && latestLog.LNGDURUMKODNavigation != null)
                    {
                        baseStatus = latestLog.LNGDURUMKODNavigation.TXTDURUMADI;
                        baseProgress = GetProgressForStatus(baseStatus ?? "");
                    }
                }

                if (string.Equals(tfs.MADDEDURUM, "RESOLVED", StringComparison.OrdinalIgnoreCase) || 
                    string.Equals(tfs.MADDEDURUM, "RESOLVE", StringComparison.OrdinalIgnoreCase))
                {
                    baseStatus = "TamamlandÄ±";
                    baseProgress = 100;
                }

                decimal? yazilimInfo = tfs.YAZILIM_TOPLAMAG;

                viewModels.Add(new Request
                {
                    Id = id,
                    Title = tfs.MADDEBASLIK ?? "BaÅŸlÄ±ksÄ±z Talep",
                    Description = portalRecord?.TXTTALEPACIKLAMA ?? "",
                    Company = firmaAdi, // Maybe update this later to be dynamic if needed
                    Status = baseStatus,
                    DevOpsStatus = tfs.MADDEDURUM ?? "-",
                    Date = tfs.ACILMATARIHI?.ToString("dd.MM.yyyy") ?? DateTime.Now.ToString("dd.MM.yyyy"),
                    LastModifiedDate = tfs.DEGISTIRMETARIHI?.ToString("dd.MM.yyyy") ?? "-",
                    PlanlananPyuat = tfs.PLANLANAN_PYUAT?.ToString("dd.MM.yyyy") ?? "-", 
                    PlanlananCanliTeslim = tfs.PLANLAN_CANLITESLIM?.ToString("dd.MM.yyyy") ?? "-",
                    Priority = "Orta",
                    Progress = baseProgress,
                    Budget = tfs.COST ?? "-",
                    AssignedTo = tfs.YARATICI ?? "AtanmamÄ±ÅŸ",
                    Effort = yazilimInfo.HasValue && yazilimInfo.Value > 0 ? yazilimInfo.Value.ToString("N2") + " K/G" : "-",
                    Cost = yazilimInfo.HasValue && yazilimInfo.Value > 0 ? (yazilimInfo.Value * 22500).ToString("N2") + " TL" : "-", 
                    Type = "GeliÅŸtirme"
                });
            }

            // 5. Add Portal-Only Requests (Not in TFS yet)
            var portalOnlyRequests = portalRequests
                .Where(r => (!r.LNGTFSNO.HasValue || r.LNGTFSNO == 0) && r.TRHKAYIT >= startDate && r.TRHKAYIT <= endDate && (r.BYTDURUM == null || r.BYTDURUM.Trim() == "1"))
                .ToList();

            foreach (var req in portalOnlyRequests)
            {
                viewModels.Add(new Request
                {
                    Id = "PORTAL-" + req.LNGKOD,
                    Title = req.TXTTALEPBASLIK ?? "BaÅŸlÄ±ksÄ±z",
                    Description = req.TXTTALEPACIKLAMA ?? "",
                    Company = firmaAdi, // Inherited from scope
                    Status = "Analiz", // Default
                    DevOpsStatus = "PORTAL",
                    Date = req.TRHKAYIT?.ToString("dd.MM.yyyy") ?? DateTime.Now.ToString("dd.MM.yyyy"),
                    LastModifiedDate = req.TRHKAYIT?.ToString("dd.MM.yyyy") ?? "-",
                    PlanlananPyuat = "-",
                    PlanlananCanliTeslim = "-",
                    Priority = "Orta",
                    Progress = 0,
                    Budget = "-",
                    AssignedTo = !string.IsNullOrEmpty(req.TXT_SORUMLULAR) ? req.TXT_SORUMLULAR : "AtanmamÄ±ÅŸ",
                    Effort = req.DEC_EFOR.HasValue ? req.DEC_EFOR.Value.ToString("N2") + " K/G" : "-",
                    Cost = req.DEC_EFOR.HasValue ? (req.DEC_EFOR.Value * 22500).ToString("N2") + " TL" : "-",
                    Po = req.TXT_PO ?? "-", // Should display PO
                    Type = "GeliÅŸtirme"
                });
            }

            return (viewModels, startDate, endDate);
        }

        public async Task<IActionResult> Destek(string period = "1m", DateTime? baslangic = null, DateTime? bitis = null, string status = null, string? filteredCompanyId = null)
        {
            var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userIdStr)) return RedirectToAction("Login", "Account");

            // Decrypt Company ID
            int? decryptedCompanyId = _urlEncryption.DecryptId(filteredCompanyId);
            int userId = int.Parse(userIdStr);

             // Fetch Authorized Companies for Filter
            var kullanici = await _mskDb.TBL_KULLANICIs.FirstOrDefaultAsync(i => i.LNGIDENTITYKOD == userId);
            if (kullanici == null) return RedirectToAction("Login", "Account");

            // Use CompanyResolutionService
            var companyResolution = await _companyResolution.ResolveCompaniesAsync(
                kullanici.LNGKOD,
                decryptedCompanyId,
                HttpContext);

            // Handle cookie setting (Refactored to service)
            if (decryptedCompanyId.HasValue)
            {
                if (decryptedCompanyId.Value == -1)
                {
                    _companyResolution.ClearCompanyCookie(HttpContext);
                }
                else if (companyResolution.TargetCompanyIds.Contains(decryptedCompanyId.Value))
                {
                    _companyResolution.SetCompanyCookie(HttpContext, decryptedCompanyId.Value);
                }
            }

            ViewBag.AuthorizedCompanies = companyResolution.AuthorizedCompanies;
            ViewBag.SelectedCompanyId = companyResolution.SelectedCompanyId;

            // Fetch Data
            var (tickets, startDate, endDate, allStatuses) = await GetSupportTicketsAsync(userId, period, baslangic, bitis, filteredCompanyId);

            // Apply Status Filter
            if (!string.IsNullOrEmpty(status))
            {
                tickets = tickets.Where(r => r.Bildirim_Durumu == status).ToList();
            }

            // Prepare View Data
            ViewBag.CurrentPeriod = period;
            ViewBag.StartDate = startDate.ToString("yyyy-MM-dd");
            ViewBag.EndDate = endDate.ToString("yyyy-MM-dd");
            ViewBag.Status = status;
            ViewBag.AllStatuses = allStatuses;

            // Prepare Chart Data
            var statusCounts = tickets
                .GroupBy(r => r.Bildirim_Durumu)
                .Select(g => new { Status = g.Key, Count = g.Count() })
                .ToList();

            ViewBag.ChartLabels = statusCounts.Select(s => s.Status ?? "Belirsiz").ToList();
            ViewBag.ChartData = statusCounts.Select(s => s.Count).ToList();

            return View(tickets);
        }

        public async Task<IActionResult> ExportDestekExcel(string period = "1m", DateTime? baslangic = null, DateTime? bitis = null, string? filteredCompanyId = null)
        {
            var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userIdStr)) return RedirectToAction("Login", "Account");
            int userId = int.Parse(userIdStr);

            var (tickets, _, _, _) = await GetSupportTicketsAsync(userId, period, baslangic, bitis, filteredCompanyId);
            var content = GenerateSupportExcel(tickets);
            var fileName = $"Destek_Raporu_{DateTime.Now:yyyyMMdd_HHmm}.xlsx";

            return File(content, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", fileName);
        }

        public async Task<IActionResult> SendSupportReport(string email, string period = "1m", DateTime? baslangic = null, DateTime? bitis = null, string? filteredCompanyId = null)
        {
            var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userIdStr)) return Json(new { success = false, message = "Oturum bulunamadÄ±." });
            int userId = int.Parse(userIdStr);

             // Rate Limiting Check
             // Rate Limiting Check (Global per user, 60 seconds)
            string cacheKey = $"MailLimit_{userId}"; 
            if (_cache.TryGetValue(cacheKey, out object _))
            {
                 // Check if it's the same request or just rate limited? 
                 // Simple rate limit: 1 email per minute per user.
                 return Json(new { success = false, message = "Ã‡ok fazla istek gÃ¶nderdiniz. LÃ¼tfen 1 dakika bekleyin." });
            }
            // Set cache
            _cache.Set(cacheKey, true, TimeSpan.FromMinutes(1));

            var kullanici = await _mskDb.TBL_KULLANICIs.FirstOrDefaultAsync(i => i.LNGIDENTITYKOD == userId);
            string firmaAdi = kullanici?.TXTFIRMAADI ?? "Firma";

            try
            {
                var (tickets, _, _, _) = await GetSupportTicketsAsync(userId, period, baslangic, bitis, filteredCompanyId);
                var content = GenerateSupportExcel(tickets);

                string subject = $"{firmaAdi} Destek Talebi Raporu";
                string message = $@"
                    <h3>SayÄ±n Ä°lgili,</h3>
                    <p>{firmaAdi} firmasÄ±na ait destek talebi raporu ektedir.</p>
                    <p>Ä°yi Ã§alÄ±ÅŸmalar dileriz.</p>
                ";

                await _emailService.SendEmailAsync(email, subject, message, content, $"Destek_Raporu_{DateTime.Now:yyyyMMdd}.xlsx");

                // Set Cache Expiration (5 Minute Cooldown)
                using (var entry = _cache.CreateEntry(cacheKey))
                {
                    entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5);
                    entry.Value = DateTime.Now;
                }

                return Json(new { success = true, message = $"Rapor baÅŸarÄ±yla {email} adresine gÃ¶nderildi." });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Mail gÃ¶nderilirken hata oluÅŸtu: " + ex.Message });
            }
        }

        private byte[] GenerateSupportExcel(List<SSP_N4B_TICKETLARI> tickets)
        {
            using (var workbook = new ClosedXML.Excel.XLWorkbook())
            {
                var worksheet = workbook.Worksheets.Add("DestekTalepleri");

                // Headers
                var headers = new[] { "Firma", "Bildirim No", "Konu", "Tarih", "Durum", "Kanal", "Ä°lgili KiÅŸi", "SLA (Saat)" };
                for (int i = 0; i < headers.Length; i++)
                {
                    worksheet.Cell(1, i + 1).Value = headers[i];
                }

                // Styling Headers
                var headerRange = worksheet.Range(1, 1, 1, headers.Length);
                headerRange.Style.Font.Bold = true;
                headerRange.Style.Fill.BackgroundColor = ClosedXML.Excel.XLColor.LightGray;
                headerRange.Style.Alignment.Horizontal = ClosedXML.Excel.XLAlignmentHorizontalValues.Center;

                // Data Rows
                int row = 2;
                foreach (var item in tickets)
                {
                    worksheet.Cell(row, 1).Value = item.Firma ?? "";
                    worksheet.Cell(row, 2).Value = item.Bildirim_No;
                    worksheet.Cell(row, 3).Value = item.Bildirim_Aciklamasi ?? "";
                    worksheet.Cell(row, 4).Value = item.Bildirim_Tarihi.ToString("dd.MM.yyyy HH:mm");
                    worksheet.Cell(row, 5).Value = item.Bildirim_Durumu ?? "";
                    worksheet.Cell(row, 6).Value = item.Gelis_Kanali ?? "";
                    worksheet.Cell(row, 7).Value = item.Musteri_Tipi1 ?? ""; 
                    worksheet.Cell(row, 8).Value = item.SLA_YD_Cozum_Sure.HasValue ? item.SLA_YD_Cozum_Sure.Value.ToString("N2") : "-";

                    row++;
                }

                worksheet.Columns().AdjustToContents();

                using (var stream = new System.IO.MemoryStream())
                {
                    workbook.SaveAs(stream);
                    return stream.ToArray();
                }
            }
        }

        private async Task<(List<SSP_N4B_TICKETLARI> Tickets, DateTime StartDate, DateTime EndDate, List<string> AllStatuses)> GetSupportTicketsAsync(int userId, string period, DateTime? baslangic, DateTime? bitis, string? filteredCompanyId = null)
        {
            int? decryptedCompanyId = _urlEncryption.DecryptId(filteredCompanyId);
            var kullanici = await _mskDb.TBL_KULLANICIs.FirstOrDefaultAsync(i => i.LNGIDENTITYKOD == userId);
            if (kullanici == null) return (new List<SSP_N4B_TICKETLARI>(), DateTime.Now, DateTime.Now, new List<string>());

            string email = User.FindFirstValue(ClaimTypes.Email) ?? "test@univera.com.tr";
            int firmaKod = kullanici.LNGORTAKFIRMAKOD ?? 2;
            var projectClaim = User.FindFirst("ProjectCode");
            
            // Company Selection Logic
            List<int> targetCompanies = new List<int>();
            if (kullanici.LNGKULLANICITIPI == 3 || kullanici.LNGKULLANICITIPI == 1)
            {
                 var authorizedIndices = await _mskDb.TBL_KULLANICI_FIRMAs
                                     .Where(f => f.LNGKULLANICIKOD == kullanici.LNGKOD)
                                     .Select(f => f.LNGFIRMAKOD)
                                     .ToListAsync();

                 if (kullanici.LNGKULLANICITIPI == 1 && !authorizedIndices.Any()) authorizedIndices.Add(firmaKod);

                  if (decryptedCompanyId.HasValue)
                  {
                      if(decryptedCompanyId.Value == -1)
                      {
                          targetCompanies.AddRange(authorizedIndices); 
                      }
                      else if (authorizedIndices.Contains(decryptedCompanyId.Value))
                      {
                          targetCompanies.Add(decryptedCompanyId.Value);
                      }
                  }
                  else
                  {
                      var cookieVal = HttpContext.Request.Cookies["SelectedCompanyId"];
                      bool cookieUsed = false;
                      if (!string.IsNullOrEmpty(cookieVal) && int.TryParse(cookieVal, out int cookiePid) && authorizedIndices.Contains(cookiePid))
                      {
                          targetCompanies.Add(cookiePid);
                          cookieUsed = true;
                      }
                      
                      if (!cookieUsed)
                      {
                          var projectClaimInner = User.FindFirst("ProjectCode");
                          if (projectClaimInner != null && int.TryParse(projectClaimInner.Value, out int selectedPid) && authorizedIndices.Contains(selectedPid))
                          {
                              targetCompanies.Add(selectedPid);
                          }
                          else
                          {
                             targetCompanies = authorizedIndices;
                          }
                      }
                  }
            }
            
            if (!targetCompanies.Any()) 
            {
                 if (projectClaim != null && int.TryParse(projectClaim.Value, out int selectedProject)) firmaKod = selectedProject;
                 targetCompanies.Add(firmaKod);
            }

            // Fetch Data using SP_N4B_TICKETLARI (Async Parallel)
            var allTickets = new System.Collections.Concurrent.ConcurrentBag<SSP_N4B_TICKETLARI>();
            
            // Use IServiceScopeFactory for thread-safe DbContext access in parallel tasks
            var scopeFactory = HttpContext.RequestServices.GetService<IServiceScopeFactory>();

            await Parallel.ForEachAsync(targetCompanies, new ParallelOptions { MaxDegreeOfParallelism = 10 }, async (fCode, ct) =>
            {
                using (var scope = scopeFactory.CreateScope())
                {
                    var db = scope.ServiceProvider.GetRequiredService<MskDbContext>();
                    try
                    {
                        var t = await db.SP_N4B_TICKETLARIAsync(Convert.ToInt16(fCode), email, 0);
                        foreach (var item in t)
                        {
                            allTickets.Add(item);
                        }
                    }
                    catch (Exception ex)
                    {
                        // Log error
                         Console.WriteLine($"Error fetching Support Tickets for company {fCode}: {ex.Message}");
                    }
                }
            });

            // Extract All Unique Statuses (Unfiltered)
            var allStatuses = allTickets
                .Select(t => t.Bildirim_Durumu ?? "")
                .Where(s => !string.IsNullOrEmpty(s))
                .Distinct()
                .OrderBy(s => s)
                .ToList();

            // Filtering Logic
            DateTime startDate;
            DateTime endDate = DateTime.Now;

            if (baslangic.HasValue)
            {
                startDate = baslangic.Value;
                if (bitis.HasValue)
                {
                    endDate = bitis.Value.Date.AddDays(1).AddTicks(-1);
                }
            }
            else
            {
                switch (period)
                {
                    case "1y":
                        startDate = DateTime.Now.AddYears(-1);
                        break;
                    case "3m":
                        startDate = DateTime.Now.AddMonths(-3);
                        break;
                    case "1m":
                    default:
                        startDate = DateTime.Now.AddMonths(-1);
                        break;
                }
            }

            var filteredTickets = allTickets
                .Where(t => t.Bildirim_Tarihi >= startDate && t.Bildirim_Tarihi <= endDate)
                .OrderByDescending(t => t.Bildirim_Tarihi)
                .ToList();

            return (filteredTickets, startDate, endDate, allStatuses);
        }

        private int GetProgressForStatus(string status)
        {
            if(string.IsNullOrEmpty(status)) return 0;
            return status switch
            {
                "Analiz" => 15,
                "BÃ¼tÃ§e OnayÄ±" => 30,
                "GeliÅŸtirme" => 50,
                "Proje Testi" => 70,
                "MÃ¼ÅŸteri UAT" => 85,
                "CanlÄ±ya GeÃ§iÅŸ" => 100,
                _ => 15
            };
        }
    }
}


