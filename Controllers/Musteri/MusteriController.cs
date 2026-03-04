using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using UniCP.DbData;
using UniCP.Models;
using UniCP.Models.Kullanici;
using UniCP.Models.MsK.SpModels;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using UniCP.Models.ViewModels;
using UniCP.Services;

namespace UniCP.Controllers.Musteri
{
    [Authorize(Roles = "Musteri,Admin")]
    public class MusteriController : Controller
    {
        private readonly MskDbContext _mskDb;
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly IUrlEncryptionService _urlEncryption;
        private readonly ICompanyResolutionService _companyResolution;

        public MusteriController(MskDbContext mskDb, IServiceScopeFactory scopeFactory, IUrlEncryptionService urlEncryption, ICompanyResolutionService companyResolution)
        {
            _mskDb = mskDb;
            _scopeFactory = scopeFactory;
            _urlEncryption = urlEncryption;
            _companyResolution = companyResolution;
        }

        public async Task<IActionResult> Index(string? filteredCompanyId = null)
        {
            var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userIdStr)) return RedirectToAction("Login", "Account");

            // Decrypt Company ID
            int? decryptedCompanyId = _urlEncryption.DecryptId(filteredCompanyId);

            int userId = int.Parse(userIdStr);
            var kullanici = await _mskDb.TBL_KULLANICIs.FirstOrDefaultAsync(i => i.LNGIDENTITYKOD == userId);
            
            if (kullanici == null) return RedirectToAction("Login", "Account");

            string email = User.FindFirstValue(ClaimTypes.Email) ?? "test@univera.com.tr";
            // FIX: For Type 3 (Univera) and Admin, pass null email to see target company's tickets
            string? emailParam = (kullanici.LNGKULLANICITIPI == 3 || kullanici.LNGKULLANICITIPI == 1) ? null : email;
            
            // Use CompanyResolutionService to handle all company filtering logic
            var resolution = await _companyResolution.ResolveCompaniesAsync(
                kullanici.LNGKOD, 
                decryptedCompanyId, 
                HttpContext);

            var targetCompanies = resolution.TargetCompanyIds;
            var authorizedCompaniesList = resolution.AuthorizedCompanies;

            // Handle cookie setting for filtered company
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

            var model = new UniveraHomeViewModel();
            ViewBag.IsMusteri = true;
            model.Kullanici = kullanici;
            model.AuthorizedCompanies = authorizedCompaniesList;
            ViewBag.AuthorizedCompanies = authorizedCompaniesList;
            ViewBag.SelectedCompanyId = resolution.SelectedCompanyId;

            DateTime trh = new DateTime(2025, 1, 1);
            var stats = new List<SSP_N4B_TICKET_DURUM_SAYILARI>();
            var slaData = new List<SSP_N4B_SLA_ORAN>();
            var openTickets = new List<SSP_N4B_TICKETLARI>();
            var liveTfsRequests = new List<SSP_TFS_GELISTIRME>();
            var financeOrders = new System.Collections.Concurrent.ConcurrentBag<SpVarunaSiparisResult>();

            // --- STRATEGY 1: 5 SEPARATE PARALLEL LOOPS (each SP type runs independently) ---
            if (targetCompanies.Any())
            {
                var statsBag = new System.Collections.Concurrent.ConcurrentBag<SSP_N4B_TICKET_DURUM_SAYILARI>();
                var slaBag = new System.Collections.Concurrent.ConcurrentBag<SSP_N4B_SLA_ORAN>();
                var ticketsBag = new System.Collections.Concurrent.ConcurrentBag<SSP_N4B_TICKETLARI>();
                var tfsBag = new System.Collections.Concurrent.ConcurrentBag<SSP_TFS_GELISTIRME>();

                var opts = new ParallelOptions { MaxDegreeOfParallelism = 10 };

                var tStats = Parallel.ForEachAsync(targetCompanies, opts, async (cid, token) => {
                    using var scope = _scopeFactory.CreateScope();
                    var db = scope.ServiceProvider.GetRequiredService<MskDbContext>();
                    try { foreach (var s in await db.SP_N4B_TICKET_DURUM_SAYILARIAsync(Convert.ToInt16(cid), emailParam, trh)) statsBag.Add(s); } catch { }
                });

                var tSla = Parallel.ForEachAsync(targetCompanies, opts, async (cid, token) => {
                    using var scope = _scopeFactory.CreateScope();
                    var db = scope.ServiceProvider.GetRequiredService<MskDbContext>();
                    try { foreach (var s in await db.SP_N4B_SLA_ORANAsync(Convert.ToInt16(cid))) slaBag.Add(s); } catch { }
                });

                var tTickets = Parallel.ForEachAsync(targetCompanies, opts, async (cid, token) => {
                    using var scope = _scopeFactory.CreateScope();
                    var db = scope.ServiceProvider.GetRequiredService<MskDbContext>();
                    try { foreach (var t in await db.SP_N4B_TICKETLARIAsync(Convert.ToInt16(cid), emailParam, 3)) ticketsBag.Add(t); } catch { }
                });

                var tTfs = Parallel.ForEachAsync(targetCompanies, opts, async (cid, token) => {
                    using var scope = _scopeFactory.CreateScope();
                    var db = scope.ServiceProvider.GetRequiredService<MskDbContext>();
                    try { foreach (var t in await db.SP_TFS_GELISTIRMEAsync(Convert.ToInt16(cid))) tfsBag.Add(t); } catch { }
                });

                var tFinance = Parallel.ForEachAsync(targetCompanies, opts, async (cid, token) => {
                    using var scope = _scopeFactory.CreateScope();
                    var db = scope.ServiceProvider.GetRequiredService<MskDbContext>();
                    try { foreach (var o in await db.SP_VARUNA_SIPARISAsync(Convert.ToInt16(cid))) financeOrders.Add(o); } catch { }
                });

                await Task.WhenAll(tStats, tSla, tTickets, tTfs, tFinance);

                stats = statsBag.ToList();
                slaData = slaBag.ToList();
                openTickets = ticketsBag.ToList();
                liveTfsRequests = tfsBag.ToList();
            }

            // --- Common calculations after SP data is fetched ---
            ViewBag.SelectedCompanyId = resolution.SelectedCompanyId;
            ViewBag.OpenTicketsCount = stats.Where(i => i.Durum.Contains("Açık", StringComparison.OrdinalIgnoreCase)).Select(i => i.Sayi).Sum();
            
            ViewBag.EscalatedCount = openTickets.Count(i => 
                (i.Bildirim_Durumu?.Contains("Eskale", StringComparison.OrdinalIgnoreCase) ?? false) || 
                (i.SLA_YD_Cozum_Kalan_Sure ?? 0) < 0
            );
            
            var startDate = new DateTime(2025, 1, 1);
            var excludedTfsStatuses = new HashSet<string>(StringComparer.OrdinalIgnoreCase) 
            { 
                "CLOSED", "CANCEL", "CANCELED", "RESOLVED", "SEND BACK", "SEND-BACK", "REJECTED",
                "KAPATILDI", "İPTAL EDİLDİ", "İPTAL", "ÇÖZÜLDÜ", "REDDEDİLDİ", "KAPALI"
            };

            var openDevRequestsCount = liveTfsRequests
                .Count(tfs => !excludedTfsStatuses.Contains((tfs.MADDEDURUM ?? "").Trim()) &&
                              tfs.ACILMATARIHI >= startDate);

            ViewBag.OpenDevRequestsCount = openDevRequestsCount;

            var aggregatedSla = slaData
                .GroupBy(x => x.DONEM)
                .Select(g => new SSP_N4B_SLA_ORAN 
                { 
                    DONEM = g.Key, 
                    ORAN = g.Average(x => x.ORAN),
                    YIL = g.First().YIL,
                    AY = g.First().AY
                })
                .OrderBy(x => x.YIL).ThenBy(x => x.AY)
                .ToList();

            var tfsIds = liveTfsRequests.Select(t => t.TFSNO).ToList();

            // --- STRATEGY 2: PARALLELIZE INDEPENDENT POST-SP QUERIES ---
            // Each query gets its own scoped DbContext (DbContext is NOT thread-safe)
            var scopePortal = _scopeFactory.CreateScope();
            var dbPortal = scopePortal.ServiceProvider.GetRequiredService<MskDbContext>();
            var tPortalRequests = dbPortal.TBL_TALEPs
                                    .Where(r => (r.LNGVARUNAKOD.HasValue && targetCompanies.Contains(r.LNGVARUNAKOD.Value)) 
                                             || (r.LNGTFSNO.HasValue && tfsIds.Contains(r.LNGTFSNO.Value)))
                                    .ToListAsync();

            var scopeApproval = _scopeFactory.CreateScope();
            var dbApproval = scopeApproval.ServiceProvider.GetRequiredService<MskDbContext>();
            var tAllApprovals = dbApproval.TBL_FINANS_ONAYs
                    .AsNoTracking()
                    .OrderByDescending(x => x.CreatedDate) 
                    .ToListAsync();

            // Company names & expired licenses (independent queries)
            Task<List<string>> tCompanyNames = Task.FromResult(new List<string>());
            Task<List<UniCP.Models.MsK.TBL_VARUNA_SOZLESME>> tExpiredContracts = Task.FromResult(new List<UniCP.Models.MsK.TBL_VARUNA_SOZLESME>());
            
            IServiceScope scopeNames = null, scopeLic = null;
            if (targetCompanies.Any())
            {
                scopeNames = _scopeFactory.CreateScope();
                var dbNames = scopeNames.ServiceProvider.GetRequiredService<MskDbContext>();
                tCompanyNames = dbNames.VIEW_ORTAK_PROJE_ISIMLERIs
                    .Where(c => targetCompanies.Contains(c.LNGKOD))
                    .Select(c => c.TXTORTAKPROJEADI)
                    .ToListAsync();

                scopeLic = _scopeFactory.CreateScope();
                var dbLic = scopeLic.ServiceProvider.GetRequiredService<MskDbContext>();
                var licenseToday = DateTime.Today;
                tExpiredContracts = dbLic.TBL_VARUNA_SOZLESMEs
                    .AsNoTracking()
                    .Where(c => c.RenewalDate.HasValue && c.RenewalDate < licenseToday)
                    .ToListAsync();
            }

            // Wait for all independent queries
            try 
            {
                await Task.WhenAll(tPortalRequests, tAllApprovals, tCompanyNames, tExpiredContracts);
            }
            catch (Exception ex)
            {
                var faults = new List<string>();
                if (tPortalRequests.IsFaulted) faults.Add("tPortalRequests");
                if (tAllApprovals.IsFaulted) faults.Add("tAllApprovals");
                if (tCompanyNames.IsFaulted) faults.Add("tCompanyNames");
                if (tExpiredContracts.IsFaulted) faults.Add("tExpiredContracts");
                throw new Exception($"Task.WhenAll failed at line 209! Faulty tasks: {string.Join(", ", faults)}", ex);
            }
            
            // Dispose scopes after queries complete
            scopePortal.Dispose();
            scopeApproval.Dispose();
            scopeNames?.Dispose();
            scopeLic?.Dispose();

            var portalRequests = tPortalRequests.Result;
            var allApprovals = tAllApprovals.Result;
            var activeCompanyNames = tCompanyNames.Result;
            var potentialExpired = tExpiredContracts.Result;

            // Dependent query: TBL_TALEP_AKIS_LOG needs portalRequests
            var requestIds = portalRequests.Select(r => (int?)r.LNGKOD).ToList();
            
            var allLogs = await _mskDb.TBL_TALEP_AKIS_LOGs
                            .Where(l => requestIds.Contains(l.LNGTALEPKOD))
                            .Include(l => l.LNGDURUMKODNavigation)
                            .ToListAsync();
            
            var logsMap = allLogs
                            .GroupBy(l => l.LNGTALEPKOD)
                            .ToDictionary(g => g.Key, g => g.OrderByDescending(l => l.TRHDURUMBASLANGIC).ThenByDescending(l => l.LNGSIRA).FirstOrDefault());

            var tfsMap = liveTfsRequests.GroupBy(x => x.TFSNO).ToDictionary(g => g.Key, g => g.First());

            decimal pendingBudgetEffort = 0;

            int uatCount = 0;
            var tfsRejectCount = liveTfsRequests.Count(t => string.Equals(t.MADDEDURUM, "Reject", StringComparison.OrdinalIgnoreCase));
            
            foreach (var req in portalRequests)
            {
                var latestLog = logsMap.ContainsKey(req.LNGKOD) ? logsMap[req.LNGKOD] : null;

                var currentStatus = "Analiz";
                if (latestLog?.LNGDURUMKODNavigation != null)
                {
                    currentStatus = latestLog.LNGDURUMKODNavigation.TXTDURUMADI ?? "Analiz";
                }
                
                if (latestLog?.LNGDURUMKOD == 5) // 5 = Müşteri UAT
                {
                     if (req.LNGTFSNO.HasValue && req.LNGTFSNO > 0)
                     {
                         if (tfsMap.ContainsKey(req.LNGTFSNO.Value)) uatCount++;
                     }
                     else
                     {
                         uatCount++;
                     }
                }

                if (string.Equals(currentStatus, "Bütçe Onayı", StringComparison.OrdinalIgnoreCase))
                {
                    decimal effort = 0;
                    
                    if (req.LNGTFSNO.HasValue && req.LNGTFSNO > 0 && tfsMap.ContainsKey(req.LNGTFSNO.Value))
                    {
                        effort = tfsMap[req.LNGTFSNO.Value].YAZILIM_TOPLAMAG ?? 0;
                    }
                    else
                    {
                        effort = req.DEC_EFOR ?? 0;
                    }

                    pendingBudgetEffort += effort;
                }
            }

            ViewBag.UatTestCount = uatCount + tfsRejectCount;
            ViewBag.PendingBudgetEffort = pendingBudgetEffort;

            // Finance approval processing
            var approvalMap = allApprovals
                    .GroupBy(x => x.OrderId.Trim(), StringComparer.OrdinalIgnoreCase)
                    .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

            decimal totalApprovalPending = 0;
            int financePendingCount = 0;

            foreach(var order in financeOrders)
            {
                if (string.IsNullOrEmpty(order.OrderId)) continue;
                
                bool isApproved = false;
                if (approvalMap.ContainsKey(order.OrderId.Trim()))
                {
                    var approval = approvalMap[order.OrderId.Trim()];
                    if (!approval.IsRevoked) isApproved = true;
                }
                
                if (string.Equals(order.Durum, "Onaylandı", StringComparison.OrdinalIgnoreCase))
                {
                    isApproved = true;
                }

                if (string.IsNullOrEmpty(order.SerialNumber) && !isApproved)
                {
                    totalApprovalPending += order.TotalAmountWithTax ?? 0;
                    financePendingCount++;
                }
            }
            
            ViewBag.PendingBudgetCost = totalApprovalPending;
            ViewBag.FinancePendingCount = financePendingCount;

            // --- EXPIRED LICENSE COUNT (uses pre-fetched data from Strategy 2) ---
            if (targetCompanies.Any() && activeCompanyNames.Any())
            {
                try 
                {
                    int expiredLicenseCount = potentialExpired.Count(c => 
                    {
                        if (string.IsNullOrEmpty(c.AccountTitle)) return false;
                        
                        return activeCompanyNames.Any(name => 
                        {
                            if (string.IsNullOrWhiteSpace(name)) return false;
                            var parts = name.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                            return parts.All(part => c.AccountTitle.Contains(part, StringComparison.OrdinalIgnoreCase));
                        });
                    });
                    
                    ViewBag.ExpiredLicenseCount = expiredLicenseCount;
                    model.ExpiredLicenseCount = expiredLicenseCount;

                    if (activeCompanyNames.Any())
                    {
                        model.AuthorizedCompanyNames = string.Join(", ", activeCompanyNames.OrderBy(n => n));
                    }
                }
                catch (Exception)
                {
                    ViewBag.ExpiredLicenseCount = 0;
                    model.ExpiredLicenseCount = 0;
                }
            }
            else
            {
                ViewBag.ExpiredLicenseCount = 0;
            }

            ViewBag.TotalSla = aggregatedSla.LastOrDefault()?.ORAN ?? 100;
            ViewBag.SlaHistory = aggregatedSla;
            
            ViewBag.Kullanici = kullanici;
            // --- Create ViewModel (Already initialized at top) ---
            model.SelectedCompanyId = resolution.SelectedCompanyId; // Or logic for default?
            
            // Map calculated data to Model
            model.OpenTicketsCount = stats.Where(i => i.Durum.Contains("Açık", StringComparison.OrdinalIgnoreCase)).Select(i => i.Sayi).Sum();
            
            model.EscalatedCount = openTickets.Count(i => 
                (i.Bildirim_Durumu?.Contains("Eskale", StringComparison.OrdinalIgnoreCase) ?? false) || 
                (i.SLA_YD_Cozum_Kalan_Sure ?? 0) < 0
            );

            model.OpenDevRequestsCount = openDevRequestsCount;
            model.SlaHistory = aggregatedSla; 
            model.UatTestCount = uatCount + tfsRejectCount;
            model.FinancePendingCount = financePendingCount;
            model.PendingBudgetCost = totalApprovalPending; 
            model.PendingBudgetEffort = pendingBudgetEffort; 

            // ExpiredLicenseCount is set in the block above
            
            return View(model);
        }

        public IActionResult N4Bgrafik()
        {
            // TEMPORARY MOCK CHART DATA
            List<BildirimChartVm> bld = new List<BildirimChartVm>
            {
                new BildirimChartVm { Durum = "Açık/Email", Sayi = 5 },
                new BildirimChartVm { Durum = "Kapatıldı/Telefon", Sayi = 8 },
                new BildirimChartVm { Durum = "İptal Edildi/Email", Sayi = 2 }
            };

            return PartialView("N4Bgrafik", bld.ToList());
        }
    }
}
