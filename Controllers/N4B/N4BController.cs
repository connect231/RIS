using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using UniCP.DbData;
using UniCP.Models.N4BModels;
using UniCP.Models.MsK.SpModels;
using UniCP.Models.MsK;
using Microsoft.EntityFrameworkCore;
using UniCP.Services;
using System.Collections.Concurrent;

namespace UniCP.Controllers.N4B
{
    [Authorize(Roles = "N4B,Admin")]
    public class N4BController : Controller
    {
        private readonly MskDbContext _mskDb;
        private readonly ILogger<N4BController> _logger;
        private readonly IUrlEncryptionService _urlEncryption;
        private readonly ICompanyResolutionService _companyResolution;
        private static readonly HttpClient _externalClient = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };

        public N4BController(MskDbContext mskDb, ILogger<N4BController> logger, ICompanyResolutionService companyResolution, IUrlEncryptionService urlEncryption)
        {
            _mskDb = mskDb;
            _logger = logger;
            _companyResolution = companyResolution;
            _urlEncryption = urlEncryption;
        }
       
        public async Task<IActionResult> Index(int id = 0, string? filteredCompanyId = null)
        {
            var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userIdStr)) return RedirectToAction("Login", "Account");

            // Decrypt the Company ID
            // If user manually enters "123", DecryptId returns null (safe).
            int? decryptedCompanyId = _urlEncryption.DecryptId(filteredCompanyId);

            int userId = int.Parse(userIdStr);
            var kullanici = await _mskDb.TBL_KULLANICIs.FirstOrDefaultAsync(i => i.LNGIDENTITYKOD == userId);
            
            if (kullanici == null)
            {
                return View(new List<SSP_N4B_TICKETLARI>());
            }

            string email = User.FindFirstValue(ClaimTypes.Email) ?? "test@univera.com.tr";
            
            // For Admin (1) and Univera Internal (3), pass null to SP to see ALL tickets
            string? emailToPass = (kullanici.LNGKULLANICITIPI == 1 || kullanici.LNGKULLANICITIPI == 3) ? null : email;
            
            // Use CompanyResolutionService for robust handling of Type 1, 2, 3 and Filters
            var resolution = await _companyResolution.ResolveCompaniesAsync(kullanici.LNGKOD, decryptedCompanyId, HttpContext);
            var targetCompanies = resolution.TargetCompanyIds;
            
            ViewBag.AuthorizedCompanies = resolution.AuthorizedCompanies;
            ViewBag.SelectedCompanyId = resolution.SelectedCompanyId;
            
             if (decryptedCompanyId.HasValue)
            {
                if (decryptedCompanyId.Value == -1)
                {
                    _companyResolution.ClearCompanyCookie(HttpContext);
                }
                else if (resolution.TargetCompanyIds.Contains(decryptedCompanyId.Value))
                {
                     _companyResolution.SetCompanyCookie(HttpContext, decryptedCompanyId.Value);
                }
            }

            // User specified: Pass 3 for 'BildirimTipi' to get Open tickets
            // UPDATE: Dashboard logic counts ANY status containing "Açık". 
            // SP with id=3 returns only strict "Open" status.
            // We fetch ALL (0) and filter in memory if id was 0 (default).
            bool filterOpen = false;
            if (id == 0) 
            {
                filterOpen = true; // Default view: Show open tickets
            }

            // DateTime trh = new DateTime(2025, 1, 1);
            // Sync with Dashboard: Use current year
            DateTime trh = new DateTime(DateTime.Now.Year, 1, 1);
            
            var bildirim_durum_sayı = new ConcurrentBag<SSP_N4B_TICKET_DURUM_SAYILARI>();
            var bildirimler = new ConcurrentBag<SSP_N4B_TICKETLARI>();
            List<SSP_N4B_SLA_ORAN> slaList = new List<SSP_N4B_SLA_ORAN>();
            
            // DEBUG INFO
            var debugErrors = new ConcurrentBag<string>();
            int debugTotalCompanies = targetCompanies.Count;
            int debugSuccessCount = 0;

            try 
            {
                // [PERFORMANCE FIX] Use Parallel.ForEachAsync to prevent timeout for admins with many companies
                await Parallel.ForEachAsync(targetCompanies, new ParallelOptions { MaxDegreeOfParallelism = 10 }, async (companyId, token) =>
                {
                    try 
                    {
                        // Create a new scope for thread safety of DbContext
                        using (var scope = HttpContext.RequestServices.CreateScope())
                        {
                            var scopedDb = scope.ServiceProvider.GetRequiredService<MskDbContext>();
                            
                            var stats = await scopedDb.SP_N4B_TICKET_DURUM_SAYILARIAsync(companyId, emailToPass, trh);
                            foreach(var s in stats) bildirim_durum_sayı.Add(s);

                            // Fetch ALL tickets (0) instead of restricted (id)
                            var fetchId = (id == 0 || id == 3) ? 0 : id;

                            var tickets = await scopedDb.SP_N4B_TICKETLARIAsync(companyId, emailToPass, fetchId);
                            foreach(var t in tickets) bildirimler.Add(t);
                            
                            // SLA not used in view currently, skipping to save time or implement later if needed
                            // var sla = await scopedDb.SP_N4B_SLA_ORANAsync(companyId);
                        }
                        Interlocked.Increment(ref debugSuccessCount);
                    }
                    catch (Exception ex)
                    {
                        debugErrors.Add($"Comp {companyId}: {ex.Message}");
                        _logger.LogWarning(ex, "Failed to fetch N4B data for company {CompanyId}", companyId);
                    }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Critical failure in N4B Index");
            }

            // Post-Processing (Moved outside try-catch to ensure execution)
            var bildirimList = bildirimler.ToList();
            
            // Default Filter: Open Tickets (if id was 0 or 3)
            if (filterOpen || id == 3)
            {
                bildirimList = bildirimList
                    .Where(x => 
                        !string.IsNullOrEmpty(x.Bildirim_Durumu) &&
                        !x.Bildirim_Durumu.Contains("Kapatıldı", StringComparison.OrdinalIgnoreCase) &&
                        !x.Bildirim_Durumu.Contains("İptal", StringComparison.OrdinalIgnoreCase))
                    .OrderByDescending(x => x.Bildirim_Tarihi)
                    .ToList();
            }
            else
            {
                bildirimList = bildirimList.OrderByDescending(x => x.Bildirim_Tarihi).ToList();
            }

            ViewBag.toplambildirim = bildirim_durum_sayı.Select(i => i.Sayi).Sum();
            ViewBag.acikbildirimsayi = bildirim_durum_sayı.Where(i => i.Durum.Contains("Açık")).Select(i => i.Sayi).Sum();
            ViewBag.kapalibildirimsayi = bildirim_durum_sayı.Where(i => i.Durum.Contains("Kapatıldı") || i.Durum.Contains("İptal")).Select(i => i.Sayi).Sum();
            ViewBag.cagrimerkezisayi = bildirim_durum_sayı.Where(i => i.Durum.Contains("Telefon")).Select(i => i.Sayi).Sum();
            ViewBag.yazilimdesteksayi = bildirim_durum_sayı.Where(i => i.Durum.Contains("Email")).Select(i => i.Sayi).Sum();
            
            ViewBag.SLA = slaList;
            
            // DEBUG VIEW REMOVED
            // var distinctStatuses = bildirimler.Select(b => b.Bildirim_Durumu).Distinct().Take(5).ToList();
            // ViewBag.DebugInfo = ...
            // ViewBag.DebugErrors = ...

            // Assign back to view model (View expects List)
            return View(bildirimList);
        }

        public async Task<IActionResult> Details(int id)
        {
            var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userIdStr)) return RedirectToAction("Login", "Account");
            int userId = int.Parse(userIdStr);
            var kullanici = await _mskDb.TBL_KULLANICIs.FirstOrDefaultAsync(i => i.LNGIDENTITYKOD == userId);
            
            if (kullanici == null) return NotFound();

            bool isAuthorized = false;

            // Admin ve İç Kullanıcılar tüm biletleri görebilir
            if (kullanici.LNGKULLANICITIPI == 1 || kullanici.LNGKULLANICITIPI == 3)
            {
                isAuthorized = true;
            }
            else
            {
                var resolution = await _companyResolution.ResolveCompaniesAsync(kullanici.LNGKOD, null, HttpContext);
                var targetCompanies = resolution.TargetCompanyIds;
                string email = User.FindFirstValue(ClaimTypes.Email) ?? "test@univera.com.tr";
                
                foreach (var companyId in targetCompanies)
                {
                    try
                    {
                        var t = await _mskDb.SP_N4B_TICKETLARIAsync(Convert.ToInt16(companyId), email, 0);
                        if (t.Any(x => x.Bildirim_No == id))
                        {
                            isAuthorized = true;
                            break;
                        }
                    }
                    catch
                    {
                        // Ignore individual company errors
                    }
                }
            }

            if (!isAuthorized)
            {
                return Unauthorized("Bu bildirimi görüntüleme yetkiniz bulunmamaktadır.");
            }

            var ticket = await _mskDb.VIEW_N4BISSUEs.FirstOrDefaultAsync(x => x.Bildirim_No == id);
            if (ticket == null) return NotFound();

            var history = await _mskDb.VIEW_N4BISSUESLIFECYCLEs
                .Where(x => x.IssueLifeIssueId == id)
                .OrderBy(x => x.Tarihce_Sira)
                .ToListAsync();

            ViewBag.History = history;
            return View(ticket);
        }
        public ActionResult Create() { return View(); }
        [HttpGet]
        public async Task<IActionResult> GetCategories()
        {
            try 
            {
                // Fetch all active categories
                var categories = await _mskDb.VIEW_N4B_KATEGORILERs
                    //.Where(x => x.UnDeleted == 1 || x.UnDeleted == null) // UnDeleted value varies (e.g. 25), not filtering for now
                    .ToListAsync();

                // Build Tree Structure in Memory
                // ParentCategoryID is string?, CategoryID is int
                // Root is defined as ParentCategoryID == "0" OR ParentCategoryID == null OR ParentCategoryID == ""
                var rootCategories = categories
                    .Where(x => x.ParentCategoryID == "0" || string.IsNullOrEmpty(x.ParentCategoryID))
                    .Select(x => new 
                    {
                        CategoryID = x.CategoryID,
                        CategoryName = x.CategoryName,
                        Children = GetChildren(categories, x.CategoryID)
                    }).ToList();

                return Json(new { success = true, data = rootCategories });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to fetch categories");
                // Return detailed error for debugging purposes
                return Json(new { success = false, message = "Error: " + ex.Message, stack = ex.StackTrace });
            }
        }

        private List<object> GetChildren(List<VIEW_N4B_KATEGORILER> allCats, int? parentId)
        {
            string pidStr = parentId.ToString();
            var children = allCats.Where(x => x.ParentCategoryID == pidStr).ToList();
            if (!children.Any()) return null;

            return children.Select(x => new
            {
                CategoryID = x.CategoryID,
                CategoryName = x.CategoryName,
                Children = GetChildren(allCats, x.CategoryID)
            }).Cast<object>().ToList();
        }

        [HttpGet]
        public IActionResult GetTicketTypes(int categoryId)
        {
            if (categoryId > 0)
            {
                try
                {
                    // Fetch all categories once to build tree in memory
                    var allCategories = _mskDb.VIEW_N4B_KATEGORILERs.ToList();
                    
                    // Build tree starting from children of selected category
                    string pidStr = categoryId.ToString();
                    var children = allCategories
                        .Where(x => x.ParentCategoryID == pidStr)
                        .Select(x => new {
                            CategoryID = x.CategoryID,
                            CategoryName = x.CategoryName,
                            Children = GetChildren(allCategories, x.CategoryID)
                        })
                        .ToList();
                    
                    return Json(new { success = true, data = children });
                }
                catch (Exception ex)
                {
                     _logger.LogError(ex, "Failed to fetch ticket types for category {CategoryId}", categoryId);
                     return Json(new { success = false, message = "Error loading types" });
                }
            }
            
            return Json(new { success = true, data = new List<object>() });
        }

        [HttpPost]
        public async Task<IActionResult> CreateTicket(string title, string description, int bildirimTipiId, int? categoryId, string? filesJson)
        {
            try
            {
                var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
                if (string.IsNullOrEmpty(userIdStr)) return Json(new { success = false, message = "Oturum bulunamadı." });

                int userId = int.Parse(userIdStr);
                var kullanici = await _mskDb.TBL_KULLANICIs.FirstOrDefaultAsync(i => i.LNGIDENTITYKOD == userId);
                if (kullanici == null) return Json(new { success = false, message = "Kullanıcı bulunamadı." });

                string email = User.FindFirstValue(ClaimTypes.Email) ?? kullanici.TXTEMAIL ?? "";

                if (string.IsNullOrWhiteSpace(title))
                    return Json(new { success = false, message = "Başlık zorunludur." });

                // Map bildirimTipiId (root category selection) to IssueTypeID
                int issueTypeId = 0;
                var rootCategory = await _mskDb.VIEW_N4B_KATEGORILERs.FirstOrDefaultAsync(c => c.CategoryID == bildirimTipiId);
                if (rootCategory != null)
                {
                    var catName = (rootCategory.CategoryName ?? "").Trim();
                    if (catName.Contains("Soru", StringComparison.OrdinalIgnoreCase)) issueTypeId = 3425;
                    else if (catName.Contains("Talep", StringComparison.OrdinalIgnoreCase)) issueTypeId = 3426;
                    else if (catName.Contains("Öneri", StringComparison.OrdinalIgnoreCase) || catName.Contains("Oneri", StringComparison.OrdinalIgnoreCase)) issueTypeId = 3427;
                    else if (catName.Contains("Şikayet", StringComparison.OrdinalIgnoreCase) || catName.Contains("Sikayet", StringComparison.OrdinalIgnoreCase)) issueTypeId = 3428;
                    else if (catName.Contains("Hata", StringComparison.OrdinalIgnoreCase)) issueTypeId = 3429;
                }

                var issue = new TBL_N4BISSUE
                {
                    TXTBILDIRIMBASLIK = title,
                    TXTBILDIRIMACIKALAMA = description,
                    IssueTypeID = issueTypeId,
                    CustomerEmail = email,
                    CategoryID = categoryId,
                    IssueID = null,
                    DURUM = 0
                };

                _mskDb.TBL_N4BISSUEs.Add(issue);
                await _mskDb.SaveChangesAsync();

                // Save attached files
                int fileCount = 0;
                if (!string.IsNullOrEmpty(filesJson))
                {
                    try
                    {
                        var jsonOpts = new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                        var files = System.Text.Json.JsonSerializer.Deserialize<List<FileUploadDto>>(filesJson, jsonOpts);
                        if (files != null)
                        {
                            foreach (var f in files)
                            {
                                var ext = System.IO.Path.GetExtension(f.FileName ?? "");
                                var fileRecord = new TBL_N4BISSSEFILE
                                {
                                    LNGTBLISSUEKOD = issue.LNGKOD,
                                    FileName = f.FileName,
                                    FileBase64 = f.FileContent,
                                    FileContentType = f.FileType,
                                    FileExtension = ext
                                };
                                _mskDb.TBL_N4BISSSEFILEs.Add(fileRecord);
                                fileCount++;
                            }
                            await _mskDb.SaveChangesAsync();
                        }
                    }
                    catch (Exception fex)
                    {
                        _logger.LogWarning(fex, "File save failed for ticket {Id}", issue.LNGKOD);
                    }
                }

                _logger.LogInformation("Ticket created: {Title} by {Email}, LNGKOD: {Id}, Files: {FileCount}", title, email, issue.LNGKOD, fileCount);

                // Send to external N4B API
                string apiError = null;
                try
                {
                    var apiUrl = $"http://10.135.140.24:8185/N4B/CreateTicket?LNGKOD={issue.LNGKOD}";
                    var apiResponse = await _externalClient.GetAsync(apiUrl);
                    var apiBody = await apiResponse.Content.ReadAsStringAsync();
                    _logger.LogInformation("External API called for LNGKOD: {Id}, Status: {Status}, Response: {Body}", issue.LNGKOD, apiResponse.StatusCode, apiBody);
                    
                    if (!apiResponse.IsSuccessStatusCode)
                    {
                        apiError = $"API Hatası ({apiResponse.StatusCode}): {apiBody}";
                    }
                }
                catch (Exception apiEx)
                {
                    _logger.LogWarning(apiEx, "External API call failed for LNGKOD: {Id}", issue.LNGKOD);
                    apiError = "API bağlantı hatası: " + apiEx.Message;
                }

                if (apiError != null)
                {
                    return Json(new { success = false, message = $"Talep oluşturuldu (LNGKOD: {issue.LNGKOD}) ancak API hatası: {apiError}", id = issue.LNGKOD });
                }

                return Json(new { success = true, message = "Talep başarıyla oluşturuldu.", id = issue.LNGKOD });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to create ticket");
                var innerMsg = ex.InnerException?.Message ?? ex.Message;
                return Json(new { success = false, message = "Hata: " + innerMsg });
            }
        }
    }

    public class FileUploadDto
    {
        public string? FileName { get; set; }
        public string? FileContent { get; set; }
        public string? FileType { get; set; }
    }
}
