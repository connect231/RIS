using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using System.Security.Claims;
using UniCP.DbData;
using UniCP.Models.Talepler;
using UniCP.Models.MsK.SpModels;
using UniCP.Models.MsK;
using Microsoft.EntityFrameworkCore;
using System.IO;
using Ganss.Xss;
using UniCP.Services;

namespace UniCP.Controllers
{
    [Authorize(Roles = UniCP.Constants.AppConstants.Roles.Talepler + "," + UniCP.Constants.AppConstants.Roles.Admin)]
    public class TaleplerController : Controller
    {
        private readonly MskDbContext _mskDb;
        private readonly ICompanyResolutionService _companyResolution;
        private readonly IUrlEncryptionService _urlEncryption;
        private readonly ILogService _logService;

        public TaleplerController(MskDbContext mskDb, ICompanyResolutionService companyResolution, IUrlEncryptionService urlEncryption, ILogService logService)
        {
            _mskDb = mskDb;
            _companyResolution = companyResolution;
            _urlEncryption = urlEncryption;
            _logService = logService;
        }

        [Route("Talepler")]
        [Route("Talepler/Index")]
        public async Task<IActionResult> Index(string status = null, string? filteredCompanyId = null, string searchId = null)
        {
            try
            {
            var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userIdStr)) return RedirectToAction("Login", "Account");

            // Decrypt Company ID
            int? decryptedCompanyId = _urlEncryption.DecryptId(filteredCompanyId);

            int userId = int.Parse(userIdStr);
            var kullanici = await _mskDb.TBL_KULLANICIs.FirstOrDefaultAsync(i => i.LNGIDENTITYKOD == userId);

            if (kullanici == null)
            {
                return View(new List<Request>());
            }

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

            var targetCompanies = companyResolution.TargetCompanyIds;
            ViewBag.AuthorizedCompanies = companyResolution.AuthorizedCompanies;
            ViewBag.SelectedCompanyId = companyResolution.SelectedCompanyId;
            
            // Load Project Names for Mapping
            var projectNames = _mskDb.VIEW_ORTAK_PROJE_ISIMLERIs
                                .Where(p => targetCompanies.Contains(p.LNGKOD))
                                .ToDictionary(p => p.LNGKOD, p => p.TXTORTAKPROJEADI);

            // 1. Fetch TFS Data (System of Record for "External" Requests)
            var filteredTfsBag = new System.Collections.Concurrent.ConcurrentBag<(SSP_TFS_GELISTIRME Item, int CompanyId)>();
            var scopeFactory = HttpContext.RequestServices.GetService<IServiceScopeFactory>();

            try 
            {
                await Parallel.ForEachAsync(targetCompanies, new ParallelOptions { MaxDegreeOfParallelism = 10 }, async (companyId, ct) =>
                {
                    using (var scope = scopeFactory.CreateScope())
                    {
                        var db = scope.ServiceProvider.GetRequiredService<MskDbContext>();
                        try 
                        {
                            var liveTfsRequests = await db.SP_TFS_GELISTIRMEAsync(Convert.ToInt16(companyId));
                            
                            // Filter TFS Data
                            var startDate = new DateTime(2025, 1, 1);
                            var excludedTfsStatuses = new HashSet<string>(StringComparer.OrdinalIgnoreCase) 
                            { 
                                "CLOSED", "CANCEL", "CANCELED", "RESOLVED", "SEND BACK", "SEND-BACK", "REJECTED",
                                "KAPATILDI", "İPTAL EDİLDİ", "İPTAL", "ÇÖZÜLDÜ", "REDDEDİLDİ", "KAPALI"
                            };

                            var companyFiltered = liveTfsRequests
                                .Where(tfs => !excludedTfsStatuses.Contains((tfs.MADDEDURUM ?? "").Trim()) &&
                                              tfs.ACILMATARIHI >= startDate)
                                .ToList();
                            
                            foreach(var item in companyFiltered)
                            {
                                filteredTfsBag.Add((item, Convert.ToInt32(companyId)));
                            }
                        } 
                        catch (Exception ex) 
                        {
                             // Log or ignore
                        }
                    }
                });
            } // Close Try
            catch (Exception)
            {
                 // Silently fail logic handled inside loop
            }

            var filteredTfs = filteredTfsBag.ToList();
    
                // Fetch Users for Mapping (Project Responsible Code -> Name)
                var users = await _mskDb.TBL_KULLANICIs
                    .Where(u => u.LNGIDENTITYKOD.HasValue)
                    .Select(u => new { Id = u.LNGIDENTITYKOD.Value.ToString(), Name = u.TXTADSOYAD })
                    .ToListAsync();
                    
                var userMap = users
                    .GroupBy(u => u.Id)
                    .ToDictionary(g => g.Key, g => g.First().Name);
    
                // 2. Fetch Portal Data (TBL_TALEP) - Includes "Shadow" records for TFS items and "Pure" portal requests
                // We preload Notes and Workflow Logs to avoid N+1
                
                var tfsIds = filteredTfsBag.Select(x => x.Item.TFSNO).ToList();
                
                var portalRequests = await _mskDb.TBL_TALEPs
                    .IgnoreQueryFilters() // Enable cross-tenant visibility for Shared/Admin-created items
                    .Include(t => t.TBL_TALEP_NOTLARs)
                    .Include(t => t.TBL_TALEP_FILEs)
                    .Where(t => (t.LNGTFSNO.HasValue && tfsIds.Contains(t.LNGTFSNO.Value)) || // Link via TFS ID (already filtered by permission)
                                (targetCompanies.Contains(t.LNGVARUNAKOD.Value))) // OR Link via Company (for Portal-only)
                    .ToListAsync();
    
                // Helper to find portal record for a TFS ID
                var portalMap = portalRequests
                    .Where(r => r.LNGTFSNO.HasValue && r.LNGTFSNO > 0)
                    .GroupBy(r => r.LNGTFSNO.Value)
                    .ToDictionary(g => g.Key, g => g.First());
    
                // 3. Merge Data
                var viewModels = new List<Request>();
    
                // A. Add TFS Requests
                foreach (var item in filteredTfs)
                {
                    var tfs = item.Item;
                    var tfsCompanyId = item.CompanyId;
                    var id = $"TFS-{tfs.TFSNO}-{tfsCompanyId}";
                    var baseStatus = "Analiz"; // Default to Analiz as per user request
                    var baseProgress = tfs.TAMAMLANMA_OARANI.HasValue ? (int)tfs.TAMAMLANMA_OARANI.Value : 0;
    
                    // Portal Override Logic
                    var portalRecord = portalMap.ContainsKey(tfs.TFSNO) ? portalMap[tfs.TFSNO] : null;
                    var comments = new List<Comment>();
                    
                    if (portalRecord != null)
                    {
                        // Check for latest status in Access Log
                        var latestLog = _mskDb.TBL_TALEP_AKIS_LOGs
                            .Where(l => l.LNGTALEPKOD == portalRecord.LNGKOD)
                            .OrderByDescending(l => l.TRHDURUMBASLANGIC)
                            .Include(l => l.LNGDURUMKODNavigation)
                            .FirstOrDefault();
    
                        if (latestLog != null && latestLog.LNGDURUMKODNavigation != null)
                        {
                            baseStatus = latestLog.LNGDURUMKODNavigation.TXTDURUMADI;
                            baseProgress = GetProgressForStatus(baseStatus);
                        }

                        // SELF-HEALING: If status is not "Canlıya Geçiş", clear survey data
                        if (baseStatus != "Canlıya Geçiş" && (portalRecord.INT_ANKET_PUAN != null || !string.IsNullOrEmpty(portalRecord.TXT_ANKET_NOT)))
                        {
                            portalRecord.INT_ANKET_PUAN = null;
                            portalRecord.TXT_ANKET_NOT = null;
                            _mskDb.SaveChanges();
                        }
    
                        // Comments
                        if (portalRecord.TBL_TALEP_NOTLARs != null)
                        {
                            foreach (var note in portalRecord.TBL_TALEP_NOTLARs)
                            {
                                 var noteUser = _mskDb.TBL_KULLANICIs.FirstOrDefault(u => u.LNGIDENTITYKOD == note.LNGKULLANICIKOD);
                                 var userName = noteUser?.TXTADSOYAD ?? "Kullanıcı";
    
                                comments.Add(new Comment
                                {
                                    Id = note.LNGKOD.ToString(),
                                    Text = note.TXTNOT ?? "",
                                    User = userName,
                                    Date = "Not Tarihi Yok" 
                                });
                            }
                        }
                    }
    
                    // Fix progress for display
                    if (baseProgress == 100 && (baseStatus == "Analiz" || baseStatus == "ACTIVE")) baseProgress = 15;
                    if (baseProgress == 0 && baseStatus == "Analiz") baseProgress = 15;
    
                    decimal? yazilimInfo = tfs.YAZILIM_TOPLAMAG;
    
                    viewModels.Add(new Request
                    {
                        Id = id,
                        DisplayId = "TFS-" + tfs.TFSNO.ToString(),
                        Title = tfs.MADDEBASLIK ?? "Başlıksız Talep",
                        Description = (portalRecord?.TXTTALEPACIKLAMA ?? "") + (projectNames.ContainsKey(portalRecord?.LNGVARUNAKOD ?? 0) ? $" [{projectNames[portalRecord?.LNGVARUNAKOD ?? 0]}]" : ""), // Hacky way to show company TODO: Add clean UI column
                        Status = baseStatus,
                        DevOpsStatus = tfs.MADDEDURUM ?? "-",
                        Date = tfs.ACILMATARIHI?.ToString("dd.MM.yyyy HH:mm") ?? DateTime.Now.ToString("dd.MM.yyyy HH:mm"),
                        LastModifiedDate = tfs.DEGISTIRMETARIHI?.ToString("dd.MM.yyyy") ?? "-",
                        PlanlananPyuat = tfs.PLANLANAN_PYUAT?.ToString("dd.MM.yyyy") ?? "-",
                        GerceklesenPyuat = tfs.GERCEKLESEN_PYUAT?.ToString("dd.MM.yyyy") ?? "-",
                        PlanlananCanliTeslim = tfs.PLANLAN_CANLITESLIM?.ToString("dd.MM.yyyy") ?? "-",
                        GerceklesenCanliTeslim = tfs.GERCEKLESEN_CANLITESLIM?.ToString("dd.MM.yyyy") ?? "-",
                        Priority = "Orta",
                        Progress = baseProgress,
                        Budget = tfs.COST ?? "-",
                        AssignedTo = (!string.IsNullOrEmpty(tfs.MUSTERI_SORUMLUSU) && userMap.ContainsKey(tfs.MUSTERI_SORUMLUSU)) 
                                        ? userMap[tfs.MUSTERI_SORUMLUSU] 
                                        : (tfs.MUSTERI_SORUMLUSU ?? "Atanmamış"),
                        Effort = yazilimInfo.HasValue && yazilimInfo.Value > 0 ? yazilimInfo.Value.ToString("N2") + " K/G" : "-",
                        Cost = yazilimInfo.HasValue && yazilimInfo.Value > 0 ? (yazilimInfo.Value * 22500).ToString("N2") + " TL" : "-", 
                        Type = "Geliştirme",
                        Subtasks = new List<Subtask>(),
                        Comments = comments,
                        LastRevisionNote = comments
                                            .Where(c => c.Text.StartsWith("REVIZE İSTEĞİ:"))
                                            .OrderByDescending(c => int.TryParse(c.Id, out int id) ? id : 0)
                                            .FirstOrDefault()?.Text.Replace("REVIZE İSTEĞİ: ", ""),

                        History = new List<HistoryItem>(),
                        SurveyScore = portalRecord?.INT_ANKET_PUAN,
                        SurveyNote = portalRecord?.TXT_ANKET_NOT
                    });
                }
    
                // B. Add Portal-Only Requests (Not synced to TFS yet)
                var portalOnlyRequests = portalRequests
                    .Where(r => (!r.LNGTFSNO.HasValue || r.LNGTFSNO == 0) 
                                && r.LNGVARUNAKOD.HasValue 
                                && targetCompanies.Contains(r.LNGVARUNAKOD.Value) 
                                && (r.BYTDURUM == null || r.BYTDURUM.Trim() == "1")) 
                    .ToList();
    
                foreach (var req in portalOnlyRequests)
                {
                    var comments = new List<Comment>();
                     if (req.TBL_TALEP_NOTLARs != null)
                    {
                        foreach (var note in req.TBL_TALEP_NOTLARs)
                        {
                             var noteUser = _mskDb.TBL_KULLANICIs.FirstOrDefault(u => u.LNGIDENTITYKOD == note.LNGKULLANICIKOD);
                             var userName = noteUser?.TXTADSOYAD ?? "Kullanıcı";
    
                            comments.Add(new Comment
                            {
                                Id = note.LNGKOD.ToString(),
                                Text = note.TXTNOT ?? "",
                                User = userName,
                                Date = "Not Tarihi Yok" 
                            });
                        }
                    }
                    
                    // Get Status
                     var latestLog = _mskDb.TBL_TALEP_AKIS_LOGs
                            .Where(l => l.LNGTALEPKOD == req.LNGKOD)
                            .OrderByDescending(l => l.TRHDURUMBASLANGIC)
                            .Include(l => l.LNGDURUMKODNavigation)
                            .FirstOrDefault();
                    
                    var itemStatus = "Analiz";
                    if(latestLog?.LNGDURUMKODNavigation != null) itemStatus = latestLog.LNGDURUMKODNavigation.TXTDURUMADI;

                    // SELF-HEALING: If status is not "Canlıya Geçiş", clear survey data
                    if (itemStatus != "Canlıya Geçiş" && (req.INT_ANKET_PUAN != null || !string.IsNullOrEmpty(req.TXT_ANKET_NOT)))
                    {
                        req.INT_ANKET_PUAN = null;
                        req.TXT_ANKET_NOT = null;
                        _mskDb.SaveChanges();
                    }
    
                    decimal effort = req.DEC_EFOR ?? 0;
                    string effortStr = effort > 0 ? effort.ToString("N2") + " K/G" : "-";
                    string costStr = effort > 0 ? (effort * 22500).ToString("N2") + " TL" : "-";
    
                    // Parse Assignees (Stored as IDs "1,2,5") and resolve names
                    string assigneeNames = "Atanmamış";
                    if (!string.IsNullOrEmpty(req.TXT_SORUMLULAR))
                    {
                        var assigneeIds = req.TXT_SORUMLULAR.Split(',', StringSplitOptions.RemoveEmptyEntries)
                                            .Select(s => int.Parse(s)).ToList();
                        var assignees = _mskDb.TBL_KULLANICIs.Where(u => u.LNGIDENTITYKOD.HasValue && assigneeIds.Contains(u.LNGIDENTITYKOD.Value)).Select(u => u.TXTADSOYAD).ToList();
                        if(assignees.Any()) assigneeNames = string.Join(", ", assignees);
                    }
    
                    viewModels.Add(new Request
                    {
                        Id = "PORTAL-" + req.LNGKOD,
                        DisplayId = "PORTAL-" + req.LNGKOD,
                        Title = req.TXTTALEPBASLIK ?? "Başlıksız",
                        Description = req.TXTTALEPACIKLAMA ?? "",
                        Status = itemStatus,
                        DevOpsStatus =("-"),
                        Date = (req.TRHKAYIT ?? DateTime.Now).ToString("dd.MM.yyyy HH:mm"),
                        LastModifiedDate = DateTime.Now.ToString("dd.MM.yyyy"),
                        PlanlananPyuat = "-",
                        GerceklesenPyuat = "-",
                        PlanlananCanliTeslim = "-",
                        GerceklesenCanliTeslim = "-",
                        Priority = "Orta",
                        Progress = GetProgressForStatus(itemStatus ?? "Analiz"),
                        Budget = "-",
                        AssignedTo = assigneeNames,
                        Effort = effortStr,
                        Cost = costStr,
                        Po = req.TXT_PO ?? "-",
                        Type = "Geliştirme",
                        Subtasks = new List<Subtask>(),
                        Comments = comments,
                        LastRevisionNote = comments
                                            .Where(c => c.Text.StartsWith("REVIZE İSTEĞİ:"))
                                            .OrderByDescending(c => int.TryParse(c.Id, out int id) ? id : 0)
                                            .FirstOrDefault()?.Text.Replace("REVIZE İSTEĞİ: ", ""),

                        History = new List<HistoryItem>(),
                        SurveyScore = req.INT_ANKET_PUAN,
                        SurveyNote = req.TXT_ANKET_NOT
                    });

                }
    
                // Sort first
                viewModels = viewModels.OrderByDescending(x => x.Id).ToList();

                // ------------------------------------------------------------
                // 4. Finance Integration: Match TFS-NO with TBL_VARUNA_TEKLIF_URUNLERI.ItemNo
                // ------------------------------------------------------------
                try 
                {
                    // A. Gather TFS Numbers from ViewModels
                    var requestMap = new Dictionary<string, List<Request>>(StringComparer.OrdinalIgnoreCase);
                    
                    foreach(var req in viewModels)
                    {
                        string tfsId = "";
                        if (req.Id.StartsWith("TFS-"))
                        {
                             var parts = req.Id.Split('-');
                             if (parts.Length >= 2) tfsId = parts[1];
                        }
                        
                        if (!string.IsNullOrEmpty(tfsId))
                        {
                            if (!requestMap.ContainsKey(tfsId)) requestMap[tfsId] = new List<Request>();
                            requestMap[tfsId].Add(req);
                        }
                    }

                    if (requestMap.Any())
                    {
                        // B. Query TBL_VARUNA_TEKLIF_URUNLERI directly
                        var tfsNumbers = requestMap.Keys.ToList();
                        
                        // Using NetLineTotalAmount_Amount as the correct price column per user request
                        var costData = await _mskDb.TBL_VARUNA_TEKLIF_URUNLERIs
                            .Where(t => t.ItemNo != null && tfsNumbers.Contains(t.ItemNo.Trim()))
                            .GroupBy(t => t.ItemNo.Trim())
                            .Select(g => new { 
                                ItemNo = g.Key, 
                                TotalCost = g.Sum(x => x.NetLineTotalAmount_Amount ?? 0),
                                OldCost = g.Sum(x => x.NetLineSubTotalLocal_Amount ?? 0),
                                VarunaQuantity = g.Sum(x => x.Quantity ?? 0)
                            })
                            .ToListAsync();

                        foreach (var item in costData)
                        {
                            if (requestMap.ContainsKey(item.ItemNo))
                            {
                                string costStr = item.TotalCost.ToString("N2") + " TL";
                                string varunaKG = item.VarunaQuantity > 0 ? item.VarunaQuantity.ToString("N2") + " K/ Gün" : null;
                                foreach (var req in requestMap[item.ItemNo])
                                {
                                    req.Cost = costStr;
                                    req.VarunaKisiGun = varunaKG;
                                }
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    System.Console.WriteLine("Finance Integration Error: " + ex.Message);
                }


                // Prepare Filter List
                var statusList = viewModels.Select(r => r.Status).Distinct().OrderBy(s => s).ToList();
                ViewBag.StatusList = statusList;
                ViewBag.CurrentStatus = status;

                // Apply Filter if selected
                if (!string.IsNullOrEmpty(status))
                {
                    if (status == "Yeni Talep")
                    {
                        viewModels = viewModels.Where(r => string.IsNullOrEmpty(r.Status) || r.Status == "Yeni Talep").ToList();
                    }
                    else
                    {
                        viewModels = viewModels.Where(r => r.Status == status).ToList();
                    }
                }

                 // Search Filter Logic
                if (!string.IsNullOrEmpty(searchId))
                {
                    viewModels = viewModels.Where(r => r.Id.IndexOf(searchId, StringComparison.OrdinalIgnoreCase) >= 0).ToList();
                }

                ViewBag.CurrentSearchId = searchId; // Pass back to view to keep input populated

                return View(viewModels);
            }
            catch (Exception ex)
            {
                // Fallback View in case of ANY error
                // Return an empty list but maybe log the error?
                // For now, returning empty list allows user to see the page at least.
                return View(new List<Request>());
            }
        }

        [HttpPost]
        public IActionResult UpdateStatus(string id, string status)
        {
            var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
            int userId = string.IsNullOrEmpty(userIdStr) ? 0 : int.Parse(userIdStr);

            // Get or Create TBL_TALEP
            var talep = GetOrCreateTalep(id, userId);
            if (talep == null) return Json(new { success = false, message = "Talep bulunamadı" });

            // Find Status ID
            var statusRecord = _mskDb.TBL_TALEP_AKISDURUMLARIs.FirstOrDefault(s => s.TXTDURUMADI == status);
            if (statusRecord == null)
            {
                // Create Status if not exists (Auto-seed for convenience)
                statusRecord = new TBL_TALEP_AKISDURUMLARI { TXTDURUMADI = status };
                _mskDb.TBL_TALEP_AKISDURUMLARIs.Add(statusRecord);
                _mskDb.SaveChanges();
            }

            // Validation: Enforce document for "Analiz Onayı"
            if (status == UniCP.Constants.AppConstants.TicketStatus.AnalizOnayi)
            {
                bool hasFiles = _mskDb.TBL_TALEP_FILEs.Any(f => f.LNGTALEPKOD == talep.LNGKOD);
                if (!hasFiles)
                {
                    return Json(new { success = false, message = "Analizi tamamlamak için en az bir doküman yüklemelisiniz." });
                }
            }

            // Check if status is effectively changing (Idempotency Check)
            var latestLog = _mskDb.TBL_TALEP_AKIS_LOGs
                            .Where(l => l.LNGTALEPKOD == talep.LNGKOD)
                            .OrderByDescending(l => l.TRHDURUMBASLANGIC)
                            .FirstOrDefault();

            if (latestLog != null && latestLog.LNGDURUMKOD == statusRecord.LNGKOD)
            {
                // Status is already this value. Do nothing, just return success.
                return Json(new { success = true });
            }

            // Insert Log
            var log = new TBL_TALEP_AKIS_LOG
            {
                LNGTALEPKOD = talep.LNGKOD,
                LNGTFSNO = talep.LNGTFSNO,
                LNGDURUMKOD = statusRecord.LNGKOD,
                TRHDURUMBASLANGIC = DateTime.Now,
                LNGONAYKULLANICI = userId,
                // LNGSIRA: Calculate max + 1
                LNGSIRA = (_mskDb.TBL_TALEP_AKIS_LOGs.Where(l => l.LNGTALEPKOD == talep.LNGKOD).Max(l => (int?)l.LNGSIRA) ?? 0) + 1
            };

            _mskDb.TBL_TALEP_AKIS_LOGs.Add(log);

            // Reset Survey if moving away from "Canlıya Geçiş"
            if (status != "Canlıya Geçiş")
            {
                talep.INT_ANKET_PUAN = null;
                talep.TXT_ANKET_NOT = null;
            }

            _mskDb.SaveChanges();

            _ = _logService.LogAsync("UPDATE_STATUS", $"Talep {talep.LNGKOD} durumu '{status}' olarak güncellendi.", "TALEPLER");

            return Json(new { success = true });
        }

        [HttpGet]
        public IActionResult CreateRequest()
        {
            return View();
        }

        [HttpPost]
        public IActionResult Create(string title, string description, decimal? effort, string assignees, string po)
        {
            var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
            int userId = string.IsNullOrEmpty(userIdStr) ? 0 : int.Parse(userIdStr);
            var kullanici = _mskDb.TBL_KULLANICIs.FirstOrDefault(i => i.LNGIDENTITYKOD == userId);
            int firmaKod = kullanici?.LNGORTAKFIRMAKOD ?? 0;

            if (string.IsNullOrEmpty(title)) return Json(new { success = false, message = "Başlık zorunludur" });

            // Sanitize Inputs
            var sanitizer = new HtmlSanitizer();
            var sanitizedTitle = sanitizer.Sanitize(title);
            var sanitizedDescription = sanitizer.Sanitize(description ?? "");
            var sanitizedPo = sanitizer.Sanitize(po ?? "");

            // 1. Create TBL_TALEP
            var talep = new TBL_TALEP
            {
                TXTTALEPBASLIK = sanitizedTitle,
                TXTTALEPACIKLAMA = sanitizedDescription,
                BYTDURUM = "1", // Active
                LNGPROJEKOD = firmaKod, // Set Project Code to User's Company Code
                LNGTFSNO = 0, // Not linked to TFS yet
                LNGVARUNAKOD = firmaKod, // Link to User's Company
                DEC_EFOR = effort,
                TXT_SORUMLULAR = assignees,
                TXT_PO = sanitizedPo,
                TRHKAYIT = DateTime.Now
            };

            _mskDb.TBL_TALEPs.Add(talep);
            _mskDb.SaveChanges(); // to get LNGKOD

            // 2. Create Initial Log (Yeni Talep Status)
            var statusStr = "Yeni Talep";
            var statusRecord = _mskDb.TBL_TALEP_AKISDURUMLARIs.FirstOrDefault(s => s.TXTDURUMADI == statusStr);
            if (statusRecord == null)
            {
                statusRecord = new TBL_TALEP_AKISDURUMLARI { TXTDURUMADI = statusStr };
                _mskDb.TBL_TALEP_AKISDURUMLARIs.Add(statusRecord);
                _mskDb.SaveChanges();
            }

            var log = new TBL_TALEP_AKIS_LOG
            {
                LNGTALEPKOD = talep.LNGKOD,
                LNGTFSNO = 0,
                LNGDURUMKOD = statusRecord.LNGKOD,
                TRHDURUMBASLANGIC = DateTime.Now,
                LNGONAYKULLANICI = userId,
                LNGSIRA = 1
            };

            _mskDb.TBL_TALEP_AKIS_LOGs.Add(log);
            _mskDb.SaveChanges();

            _ = _logService.LogAsync("CREATE_REQUEST", $"Yeni talep oluşturuldu: {talep.TXTTALEPBASLIK} (ID: {talep.LNGKOD})", "TALEPLER");

            // Return formatted request object for UI to append
            var newRequest = new Request
            {
                Id = "PORTAL-" + talep.LNGKOD, // Temporary ID format for portal-only requests
                DisplayId = "PORTAL-" + talep.LNGKOD,
                Title = talep.TXTTALEPBASLIK,
                Description = talep.TXTTALEPACIKLAMA,
                Status = "Analiz",
                DevOpsStatus = "",
                Date = DateTime.Now.ToString("dd.MM.yyyy HH:mm"),
                LastModifiedDate = DateTime.Now.ToString("dd.MM.yyyy"),
                Priority = "Orta",
                Progress = 15,
                AssignedTo = "Atanmamış",
                Type = "Geliştirme",
                Comments = new List<Comment>(),
                Subtasks = new List<Subtask>(),
                History = new List<HistoryItem>()
                {
                    new HistoryItem { Id = "1", User = User.Identity?.Name ?? "Ben", Action = "Talep oluşturuldu.", Date = DateTime.Now.ToString("dd.MM.yyyy HH:mm"), Type="status_change" }
                }
            };

            return Json(new { success = true, request = newRequest });
        }

        [HttpPost]
        public IActionResult AddComment(string id, string text)
        {
            var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
            int userId = string.IsNullOrEmpty(userIdStr) ? 0 : int.Parse(userIdStr);

            var talep = GetOrCreateTalep(id, userId);
            if (talep == null) return Json(new { success = false, message = "Talep bulunamadı veya erişim yetkiniz yok." });

            // Sanitize Input
            var sanitizer = new HtmlSanitizer();
            var sanitizedText = sanitizer.Sanitize(text);

            var note = new TBL_TALEP_NOTLAR
            {
                LNGTALEPKOD = talep.LNGKOD,
                TXTNOT = sanitizedText,
                LNGKULLANICIKOD = userId,
                BYTDURUM = 1 // Active
            };

            _mskDb.TBL_TALEP_NOTLARs.Add(note);
            _mskDb.SaveChanges();
            
            _ = _logService.LogAsync("ADD_COMMENT", $"Talep {talep.LNGKOD} için yorum eklendi.", "TALEPLER");

             var userName = User.Identity?.Name ?? "Kullanıcı";
             var userRec = _mskDb.TBL_KULLANICIs.FirstOrDefault(u => u.LNGIDENTITYKOD == userId);
             if (userRec != null) userName = userRec.TXTADSOYAD;

            return Json(new { success = true, comment = new Comment { 
                User = userName, 
                Text = text, 
                Date = DateTime.Now.ToString("dd.MM.yyyy HH:mm") 
            }});
        }

        [HttpPost]
        public async Task<IActionResult> UploadFile(string id, IFormFile file)
        {
            if (file == null || file.Length == 0) return Json(new { success = false, message = "Dosya seçilmedi" });

            // Extension Validation
            var allowedExtensions = new[] { ".pdf", ".xslt", ".docx", ".xls", ".xlsx", ".doc", ".txt", ".jpeg", ".png" };
            var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
            if (!allowedExtensions.Contains(extension))
            {
                return Json(new { success = false, message = "Geçersiz dosya uzantısı. İzin verilenler: pdf, xslt, docx, xls, xlsx, doc, txt, jpeg, png" });
            }

            var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
            int userId = string.IsNullOrEmpty(userIdStr) ? 0 : int.Parse(userIdStr);

            var talep = GetOrCreateTalep(id, userId);
            if (talep == null) return Json(new { success = false, message = "Talep bulunamadı veya erişim yetkiniz yok." });

            try
            {
                // Ensure directory exists
                string uploadPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads", "talepler", talep.LNGKOD.ToString());
                if (!Directory.Exists(uploadPath)) Directory.CreateDirectory(uploadPath);

                string originalFileName = Path.GetFileName(file.FileName);
                string uniqueFileName = $"{DateTime.Now.Ticks}_{originalFileName}";
                string filePath = Path.Combine(uploadPath, uniqueFileName);

                using (var stream = new FileStream(filePath, FileMode.Create))
                {
                    await file.CopyToAsync(stream);
                }

                // Create DB Record
                var talepFile = new TBL_TALEP_FILE
                {
                    LNGTALEPKOD = talep.LNGKOD,
                    FileName = originalFileName,
                    FileBase64 = $"/uploads/talepler/{talep.LNGKOD}/{uniqueFileName}",
                    FileContentType = file.ContentType
                };

                _mskDb.TBL_TALEP_FILEs.Add(talepFile);
                await _mskDb.SaveChangesAsync();

                _ = _logService.LogAsync("UPLOAD_FILE", $"Talep {talep.LNGKOD} için dosya yüklendi: {originalFileName}", "TALEPLER");

                return Json(new { success = true, fileName = originalFileName, filePath = talepFile.FileBase64 });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Hata: " + ex.Message });
            }
        }











        [HttpGet]
        public IActionResult GetFiles(string id, string? talepNo)
        {
            var talepId = id ?? talepNo;
            if (string.IsNullOrEmpty(talepId)) return Json(new { success = false, message = "ID eksik" });

            var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
            int userId = string.IsNullOrEmpty(userIdStr) ? 0 : int.Parse(userIdStr);

            var talep = GetOrCreateTalep(talepId, userId);
            if (talep == null) return Json(new { success = false, message = "Talep bulunamadı" });

            var files = _mskDb.TBL_TALEP_FILEs
                .Where(f => f.LNGTALEPKOD == talep.LNGKOD)
                .Select(f => new
                {
                    lngkod = f.LNGKOD,
                    fileName = f.FileName,
                    fileBase64 = f.FileBase64,
                    date = "-"
                })
                .ToList();

            return Json(new { success = true, files = files });
        }

        [HttpGet]
        public IActionResult DownloadFile(int id)
        {
            var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
            int userId = string.IsNullOrEmpty(userIdStr) ? 0 : int.Parse(userIdStr);

            var fileRec = _mskDb.TBL_TALEP_FILEs.Include(f => f.LNGTALEPKODNavigation).FirstOrDefault(f => f.LNGKOD == id);
            
            if (fileRec == null) return NotFound("Dosya kaydı bulunamadı.");

            // SECURE CHECK: Ensure user has access to the parent Talep
            var talep = GetOrCreateTalep(fileRec.LNGTALEPKOD.ToString(), userId);
            if (talep == null) return Unauthorized("Bu dosyaya erişim yetkiniz yok.");

            string relativePath = fileRec.FileBase64?.TrimStart('/') ?? "";
            string filePath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", relativePath);
            
            if (!System.IO.File.Exists(filePath)) return NotFound("Dosya fiziksel olarak bulunamadı.");

            var contentType = fileRec.FileContentType ?? "application/octet-stream";
            var downloadName = fileRec.FileName ?? "download";

            return File(System.IO.File.OpenRead(filePath), contentType, downloadName);
        }

        [HttpGet]
        public IActionResult GetUsers(string q)
        {
             var query = _mskDb.TBL_KULLANICIs.AsQueryable();
             if (!string.IsNullOrEmpty(q))
             {
                 query = query.Where(u => u.TXTADSOYAD.Contains(q));
             }

             var users = query.Select(u => new { 
                 id = u.LNGIDENTITYKOD, 
                 name = u.TXTADSOYAD, 
                 email = u.TXTEMAIL 
             }).Take(20).ToList();

             return Json(users);
        }

        [HttpPost]
        public IActionResult UpdatePo(string id, string po)
        {
            var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
            int userId = string.IsNullOrEmpty(userIdStr) ? 0 : int.Parse(userIdStr);

            var talep = GetOrCreateTalep(id, userId);
            if (talep == null) return Json(new { success = false, message = "Talep bulunamadı" });

            var sanitizer = new HtmlSanitizer();
            talep.TXT_PO = sanitizer.Sanitize(po);
            _mskDb.SaveChanges();

            return Json(new { success = true });
        }

        [HttpPost]
        public IActionResult Delete(string id)
        {
            var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
            int userId = string.IsNullOrEmpty(userIdStr) ? 0 : int.Parse(userIdStr);

            var talep = GetOrCreateTalep(id, userId);
            if (talep == null) return Json(new { success = false, message = "Talep bulunamadı" });

            if (talep.LNGTFSNO.HasValue && talep.LNGTFSNO > 0)
            {
                return Json(new { success = false, message = "TFS ile senkronize olmuş talepler silinemez." });
            }

            var latestLog = _mskDb.TBL_TALEP_AKIS_LOGs
                            .Where(l => l.LNGTALEPKOD == talep.LNGKOD)
                            .OrderByDescending(l => l.TRHDURUMBASLANGIC)
                            .Include(l => l.LNGDURUMKODNavigation)
                            .FirstOrDefault();

            string currentStatus = latestLog?.LNGDURUMKODNavigation?.TXTDURUMADI ?? "Analiz";

            if (currentStatus != "Analiz" && currentStatus != "Yeni Talep")
            {
                 return Json(new { success = false, message = "Sadece 'Yeni Talep' veya 'Analiz' aşamasındaki talepler silinebilir." });
            }

            talep.BYTDURUM = "0"; // Passive
            _mskDb.SaveChanges();

            _ = _logService.LogAsync("DELETE_REQUEST", $"Talep {talep.LNGKOD} silindi (pasife alındı).", "TALEPLER");

            return Json(new { success = true });
        }

        [HttpPost]
        public IActionResult RequestRevision(string id, string reason)
        {
            var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
            int userId = string.IsNullOrEmpty(userIdStr) ? 0 : int.Parse(userIdStr);

            var talep = GetOrCreateTalep(id, userId);
            if (talep == null) return Json(new { success = false, message = "Talep bulunamadı" });

            // Sanitize Input
            var sanitizer = new HtmlSanitizer();
            var sanitizedReason = sanitizer.Sanitize(reason);

            // 1. Add Revision Note
            var note = new TBL_TALEP_NOTLAR
            {
                LNGTALEPKOD = talep.LNGKOD,
                TXTNOT = "REVIZE İSTEĞİ: " + sanitizedReason,
                LNGKULLANICIKOD = userId,
                BYTDURUM = 1
            };
            _mskDb.TBL_TALEP_NOTLARs.Add(note);

            // 2. Update Status to Proje Testi (Back to Project Test)
            var statusStr = "Proje Testi";
            var statusRecord = _mskDb.TBL_TALEP_AKISDURUMLARIs.FirstOrDefault(s => s.TXTDURUMADI == statusStr);
            if (statusRecord == null)
            {
                statusRecord = new TBL_TALEP_AKISDURUMLARI { TXTDURUMADI = statusStr };
                _mskDb.TBL_TALEP_AKISDURUMLARIs.Add(statusRecord);
            }

            // 3. Log
             var log = new TBL_TALEP_AKIS_LOG
            {
                LNGTALEPKOD = talep.LNGKOD,
                LNGTFSNO = talep.LNGTFSNO,
                LNGDURUMKOD = statusRecord.LNGKOD,
                TRHDURUMBASLANGIC = DateTime.Now,
                LNGONAYKULLANICI = userId,
                // LNGSIRA: Calculate max + 1
                LNGSIRA = (_mskDb.TBL_TALEP_AKIS_LOGs.Where(l => l.LNGTALEPKOD == talep.LNGKOD).Max(l => (int?)l.LNGSIRA) ?? 0) + 1
            };
            _mskDb.TBL_TALEP_AKIS_LOGs.Add(log);

            // Reset Survey (since we are moving back to Test/Dev)
            talep.INT_ANKET_PUAN = null;
            talep.TXT_ANKET_NOT = null;
            
            _mskDb.SaveChanges();

            return Json(new { success = true });
        }

        [HttpPost]
        public IActionResult UpdateRevisionNote(string id, string text)
        {
            var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
            int userId = string.IsNullOrEmpty(userIdStr) ? 0 : int.Parse(userIdStr);

            var talep = GetOrCreateTalep(id, userId);
            if (talep == null) return Json(new { success = false, message = "Talep bulunamadı" });

            // Sanitize Input
            var sanitizer = new HtmlSanitizer();
            var sanitizedReason = sanitizer.Sanitize(text);

            // Find the latest revision note
            var note = _mskDb.TBL_TALEP_NOTLARs
                        .Where(n => n.LNGTALEPKOD == talep.LNGKOD && n.TXTNOT.StartsWith("REVIZE İSTEĞİ:"))
                        .OrderByDescending(n => n.LNGKOD)
                        .FirstOrDefault();

            if (note != null)
            {
                note.TXTNOT = "REVIZE İSTEĞİ: " + sanitizedReason;
                // note.LNGKULLANICIKOD = userId; // Optional: update who edited it? Maybe keep original author logic.
                _mskDb.SaveChanges();
                return Json(new { success = true });
            }
            else
            {
                 // Fallback: If not found (maybe manual status change?), create new?
                 // For now, return error as we are "editing"
                 return Json(new { success = false, message = "Düzenlenecek revize notu bulunamadı." });
            }
        }

        [HttpPost]
        public async Task<IActionResult> SubmitSurvey(string id, int score, string? note)
        {
            try
            {
                var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
                if (string.IsNullOrEmpty(userIdStr)) return Json(new { success = false, message = "Oturum açmanız gerekiyor." });

                // Find or Create wrapper
                var talep = _mskDb.TBL_TALEPs.FirstOrDefault(t => t.LNGKOD.ToString() == id || (t.LNGTFSNO.ToString() == id && t.LNGTFSNO > 0)); 
                
                int? portalId = null;
                int? tfsId = null;

                if (id.StartsWith("PORTAL-")) { int.TryParse(id.Replace("PORTAL-", ""), out int pId); portalId = pId; }
                else if (id.StartsWith("TFS-")) 
                { 
                    var parts = id.Split('-');
                    if (parts.Length >= 2) { int.TryParse(parts[1], out int tId); tfsId = tId; }
                }
                else { int.TryParse(id, out int pId); portalId = pId; }

                TBL_TALEP? talepRecord = null;
                if(portalId.HasValue && portalId.Value > 0) talepRecord = _mskDb.TBL_TALEPs.FirstOrDefault(t => t.LNGKOD == portalId.Value);
                if(talepRecord == null && tfsId.HasValue && tfsId.Value > 0) talepRecord = _mskDb.TBL_TALEPs.FirstOrDefault(t => t.LNGTFSNO == tfsId.Value);

                if (talepRecord == null)
                {
                    if (tfsId.HasValue && tfsId.Value > 0)
                    {
                        talepRecord = new TBL_TALEP
                        {
                            LNGTFSNO = tfsId.Value,
                            // TXTTALEPBASLIK? Maybe fetch or leave empty
                            // LNGPROJEKOD = 1 // default
                            TRHKAYIT = DateTime.Now
                        };
                        _mskDb.TBL_TALEPs.Add(talepRecord);
                    }
                    else
                    {
                        return Json(new { success = false, message = "Talep bulunamadı." });
                    }
                }

                // Update Score
                talepRecord.INT_ANKET_PUAN = score;
                talepRecord.TXT_ANKET_NOT = note;
                
                await _mskDb.SaveChangesAsync();

                return Json(new { success = true });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        private TBL_TALEP? GetOrCreateTalep(string idStr, int userId)
        {
            // Handle Portal IDs
            if (idStr.StartsWith("PORTAL-"))
            {
                if (int.TryParse(idStr.Replace("PORTAL-", ""), out int talepId))
                {
                    // Use IgnoreQueryFilters to find the record even if created by another company (Admin vs Customer)
                    // Security check happens later.
                    return _mskDb.TBL_TALEPs.IgnoreQueryFilters().FirstOrDefault(t => t.LNGKOD == talepId);
                }
                return null;
            }
            
            // Handle raw Integers (Legacy/Direct ID)
            if (int.TryParse(idStr, out int rawId))
            {
                 var direct = _mskDb.TBL_TALEPs.IgnoreQueryFilters().FirstOrDefault(t => t.LNGKOD == rawId);
                 if(direct != null) return direct;
            }

            bool isTfs = idStr.StartsWith("TFS-");
            int tfsNo = 0;
            int targetCompanyId = 0;

            if (isTfs)
            {
                var parts = idStr.Split('-');
                if (parts.Length >= 2) int.TryParse(parts[1], out tfsNo);
                if (parts.Length >= 3) int.TryParse(parts[2], out targetCompanyId);
            }

            TBL_TALEP? talep = null;

            if (isTfs && tfsNo > 0)
            {
                // First try to find existing record globally
                talep = _mskDb.TBL_TALEPs.IgnoreQueryFilters().FirstOrDefault(t => t.LNGTFSNO == tfsNo);

                if (talep == null)
                {
                    // Create Shadow Record
                    // Use targetCompanyId if available, otherwise look it up
                    if (targetCompanyId <= 0)
                    {
                         // Fallback: If we don't have company ID in string (Old link?), we default to User's company or 2
                         // ideally this path shouldn't be hit often with new IDs
                         var user = _mskDb.TBL_KULLANICIs.FirstOrDefault(u => u.LNGIDENTITYKOD == userId);
                         targetCompanyId = user?.LNGORTAKFIRMAKOD ?? 2;
                    }
                    
                    // Fetch Title from TFS using the Target Company
                    var tfsItem = _mskDb.SP_TFS_GELISTIRME(Math.Max(2, targetCompanyId)).FirstOrDefault(t => t.TFSNO == tfsNo);
                    
                    talep = new TBL_TALEP
                    {
                        LNGTFSNO = tfsNo,
                        TXTTALEPBASLIK = tfsItem?.MADDEBASLIK ?? "Otomatik Oluşturulan Talep",
                        BYTDURUM = "1", // Active
                        LNGPROJEKOD = targetCompanyId, // CRITICAL: Set Project Code to the Actual Company Code of the TFS Item
                        LNGVARUNAKOD = targetCompanyId // CRITICAL: Set Varuna/Tenant Code to Actual Company Code
                    };
                    _mskDb.TBL_TALEPs.Add(talep);
                    _mskDb.SaveChanges();
                }
            }
            
            if (talep != null && userId > 0)
            {
                // MANUAL IDOR / AUTHZ CHECK
                var user = _mskDb.TBL_KULLANICIs.FirstOrDefault(u => u.LNGIDENTITYKOD == userId);
                if (user != null && !user.LNGKULLANICITIPI.GetValueOrDefault().Equals((int)UniCP.Models.Enums.UserType.Admin) 
                                 && !user.LNGKULLANICITIPI.GetValueOrDefault().Equals((int)UniCP.Models.Enums.UserType.UniveraInternal))
                {
                    // If regular customer, check ownership
                    // Compare against LNGVARUNAKOD which we ensured is correct now
                    if (talep.LNGVARUNAKOD.HasValue && user.LNGORTAKFIRMAKOD.HasValue && talep.LNGVARUNAKOD != user.LNGORTAKFIRMAKOD)
                    {
                        // Unauthorized access attempt!
                        return null; 
                    }
                }
            }
            
            return talep;
        }

        private int GetProgressForStatus(string status)
        {
            return status switch
            {
                "Yeni Talep" => 0,
                "Analiz" => 15,
                "Bütçe Onayı" => 30,
                "Geliştirme" => 50,
                "Proje Testi" => 70,
                "Müşteri UAT" => 85,
                "Canlıya Geçiş" => 100,
                _ => 15
            };
        }
    }
}
