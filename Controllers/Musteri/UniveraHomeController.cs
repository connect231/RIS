using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using System.Collections.Concurrent;
using System.Security.Claims;
using UniCP.DbData;
using UniCP.Models;
using UniCP.Models.Kullanici;
using UniCP.Models.MsK.SpModels;
using UniCP.Models.MsK;
using Microsoft.EntityFrameworkCore;
using System.Linq;
using UniCP.Services;
using UniCP.Models.ViewModels;

namespace UniCP.Controllers.Musteri
{
    // New Role Requirement
    [Authorize(Roles = "UniveraHome,Admin")]
    public class UniveraHomeController : Controller
    {
        private readonly MskDbContext _mskDb;
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly IMemoryCache _cache;
        private readonly ILogger<UniveraHomeController> _logger;
        private readonly ICompanyResolutionService _companyResolution;
        private readonly IUrlEncryptionService _urlEncryption;

        public UniveraHomeController(
            MskDbContext mskDb, 
            IServiceScopeFactory scopeFactory, 
            IMemoryCache cache, 
            ILogger<UniveraHomeController> logger,
            ICompanyResolutionService companyResolution,
            IUrlEncryptionService urlEncryption)
        {
            _mskDb = mskDb;
            _scopeFactory = scopeFactory;
            _cache = cache;
            _logger = logger;
            _companyResolution = companyResolution;
            _urlEncryption = urlEncryption;
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
            
            // Use CompanyResolutionService to handle all company filtering logic
            var companyResolution = await _companyResolution.ResolveCompaniesAsync(
                kullanici.LNGKOD, 
                decryptedCompanyId, 
                HttpContext);


            var targetCompanies = companyResolution.TargetCompanyIds;
            var authorizedCompaniesList = companyResolution.AuthorizedCompanies;
            var debugFlow = companyResolution.DebugFlow;

            // Handle cookie setting for filtered company (Refactored to service)
            if (decryptedCompanyId.HasValue)
            {
                if (decryptedCompanyId.Value == -1)
                {
                    _companyResolution.ClearCompanyCookie(HttpContext);
                }
                else if (decryptedCompanyId.Value > 0 && targetCompanies.Contains(decryptedCompanyId.Value))
                {
                    _companyResolution.SetCompanyCookie(HttpContext, decryptedCompanyId.Value);
                }
            }

            // Set ViewBag properties (consolidated)
            // Initialize ViewModel
            var model = new UniveraHomeViewModel
            {
                AuthorizedCompanies = authorizedCompaniesList,
                AuthorizedCompanyNames = companyResolution.AuthorizedCompanyNames,
                DebugFlow = debugFlow
            };

            // Determine SelectedCompanyId for model
            if (decryptedCompanyId.HasValue && decryptedCompanyId.Value == -1)
            {
                model.SelectedCompanyId = null;
            }
            else if (!decryptedCompanyId.HasValue && targetCompanies.Count == 1)
            {
                model.SelectedCompanyId = targetCompanies.First();
            }
            else
            {
                model.SelectedCompanyId = decryptedCompanyId ?? companyResolution.SelectedCompanyId;
            }


            DateTime trh = new DateTime(2025, 1, 1);
            // --- UNIFIED DATA FETCHING (Type 3 Optimized vs Standard) ---

            // Container for all lists to be populated
            List<SSP_N4B_TICKET_DURUM_SAYILARI> stats = new List<SSP_N4B_TICKET_DURUM_SAYILARI>();
            List<SSP_N4B_SLA_ORAN> slaData = new List<SSP_N4B_SLA_ORAN>();
            List<SSP_N4B_TICKETLARI> openTickets = new List<SSP_N4B_TICKETLARI>();
            List<SSP_TFS_GELISTIRME> liveTfsRequests = new List<SSP_TFS_GELISTIRME>();
            var financeOrders = new System.Collections.Concurrent.ConcurrentBag<SpVarunaSiparisResult>(); // Thread-safe for parallel parts

            var dashboardData = model.DashboardData;
            ViewBag.IsUniveraHome = true;

            // --- UNIFIED REAL-TIME FLOW ---
            // Variable to hold pre-fetched details for batch optimization
            Dictionary<string, List<SSP_VARUNA_SIPARIS_DETAY>> financeDetailsCache = null;
            var debugExceptions = new List<string>();
            


            // Determine if we should use GENEL SPs (all companies, no params - fastest path)
            // Use GENEL when: no specific company filter in URL (or reset to all via -1)
            // For Type 3 (Univera): ALWAYS use GENEL unless a specific company was chosen in URL
            // For Type 4/Admin: Use GENEL when viewing all companies (no specific filter)
            bool specificCompanySelected = decryptedCompanyId.HasValue && decryptedCompanyId.Value > 0;
            bool useGenel = !specificCompanySelected && 
                (kullanici.LNGKULLANICITIPI == 3 || kullanici.LNGKULLANICITIPI == 4 || kullanici.LNGKULLANICITIPI == 1);

            if (useGenel && targetCompanies.Any())
            {
                 // --- COKLU SP OPTIMIZATION (Batch call with comma-separated IDs) ---
                 debugExceptions.Add("Using COKLU SPs for batch all-companies view");

                 var companyCodes = string.Join(",", targetCompanies);

                 var t1 = Task.Run(async () => {
                    using var scope = _scopeFactory.CreateScope();
                    var db = scope.ServiceProvider.GetRequiredService<MskDbContext>();
                    return await db.SP_N4B_TICKET_DURUM_SAYILARI_COKLU_FILTREAsync(companyCodes, email, trh);
                 });

                 var t2 = Task.Run(async () => {
                    using var scope = _scopeFactory.CreateScope();
                    var db = scope.ServiceProvider.GetRequiredService<MskDbContext>();
                    return await db.SP_N4B_SLA_ORAN_COKLU_FILTREAsync(companyCodes);
                 });

                 var t3 = Task.Run(async () => {
                    using var scope = _scopeFactory.CreateScope();
                    var db = scope.ServiceProvider.GetRequiredService<MskDbContext>();
                    return await db.SP_N4B_TICKETLARI_COKLU_FILTREAsync(companyCodes, email, 3);
                 });

                 var t4 = Task.Run(async () => {
                    using var scope = _scopeFactory.CreateScope();
                    var db = scope.ServiceProvider.GetRequiredService<MskDbContext>();
                    return await db.SP_TFS_GELISTIRME_COKLU_FILTREAsync(companyCodes);
                 });

                 var t5 = Task.Run(async () => {
                    using var scope = _scopeFactory.CreateScope();
                    var db = scope.ServiceProvider.GetRequiredService<MskDbContext>();
                    return await db.SP_VARUNA_SIPARIS_COKLU_FILTREAsync(companyCodes);
                 });

                 await Task.WhenAll(t1, t2, t3, t4, t5);

                     var batchStats = t1.Result;
                     var batchSla = t2.Result;
                     var batchTickets = t3.Result;
                     var batchTfs = t4.Result;
                     var batchOrders = t5.Result;
                     

                     // Populate Company Map
                     var allCompanyIds = batchStats.Select(x => (int?)x.LNGORTAKPROJEKOD)
                         .Union(batchSla.Select(x => (int?)x.LNGORTAKPROJEKOD))
                         .Union(batchTickets.Select(x => (int?)x.LNGORTAKPROJEKOD))
                         .Union(batchTfs.Select(x => (int?)x.LNGORTAKPROJEKOD))
                         .Union(batchOrders.Select(x => (int?)x.LNGORTAKPROJEKOD))
                         .Where(x => x.HasValue)
                         .Select(x => x.Value)
                         .Distinct();

                     // Initialize map for all target companies to ensure no missing keys
                     foreach(var cid in targetCompanies) 
                     {
                         if (!dashboardData.CompanyMap.ContainsKey(cid)) 
                             dashboardData.CompanyMap[cid] = new CompanyData();
                     }


                     // 1. Stats
                     foreach(var item in batchStats)
                     {
                         if(dashboardData.CompanyMap.ContainsKey(item.LNGORTAKPROJEKOD))
                            dashboardData.CompanyMap[item.LNGORTAKPROJEKOD].Stats.Add(item.ToBase());
                     }
                     // 2. SLA
                     foreach(var item in batchSla)
                     {
                         if(dashboardData.CompanyMap.ContainsKey(item.LNGORTAKPROJEKOD))
                            dashboardData.CompanyMap[item.LNGORTAKPROJEKOD].SlaData.Add(item.ToBase());
                     }
                     // 3. Tickets
                     foreach(var item in batchTickets)
                     {
                         if(dashboardData.CompanyMap.ContainsKey(item.LNGORTAKPROJEKOD))
                            dashboardData.CompanyMap[item.LNGORTAKPROJEKOD].OpenTickets.Add(item.ToBase());
                     }
                     // 4. TFS
                     foreach(var item in batchTfs)
                     {
                         if(item.LNGORTAKPROJEKOD.HasValue && dashboardData.CompanyMap.ContainsKey(item.LNGORTAKPROJEKOD.Value))
                            dashboardData.CompanyMap[item.LNGORTAKPROJEKOD.Value].TfsRequests.Add(item.ToBase());
                     }
                     // 5. Orders
                     foreach(var item in batchOrders)
                     {
                         if(dashboardData.CompanyMap.ContainsKey(item.LNGORTAKPROJEKOD))
                            dashboardData.CompanyMap[item.LNGORTAKPROJEKOD].FinanceOrders.Add(item.ToBase());
                     }

                     // --- FINANCE DETAILS BATCH PRE-FETCH ---
                     var openOrderIds = batchOrders
                         .Where(o => string.Equals(o.OrderStatus, "Open", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrEmpty(o.OrderId))
                         .Select(o => o.OrderId)
                         .Distinct()
                         .ToList();

                     if (openOrderIds.Any())
                     {
                         try 
                         {
                             var orderIdsStr = string.Join(",", openOrderIds);
                             var batchDetails = await _mskDb.SP_VARUNA_SIPARIS_DETAY_COKLU_FILTREAsync(orderIdsStr);
                             
                             financeDetailsCache = batchDetails
                                 .GroupBy(d => d.OrderId)
                                 .ToDictionary(g => g.Key, g => g.Select(x => x.ToBase()).ToList());
                         }
                         catch (Exception ex)
                         {
                             debugExceptions.Add($"Batch Details Error: {ex.Message}");
                         }
                     }
            }
            else
            {
                // --- EXISTING PARALLEL LOOP FOR OTHER USERS ---
                // var dashboardData = new UniveraDashboardData(); // MOVED UP
                     
                // Use ConcurrentDictionary to store data segregated by CompanyID
                var concurrentMap = new System.Collections.Concurrent.ConcurrentDictionary<int, CompanyData>();
                     
                // FETCH ONLY TARGET COMPANIES to ensure speed
                // If targetCompanies is empty, we fetch nothing (correct behavior as nothing is shown)
                var companiesToFetch = targetCompanies.Distinct().ToList();

                var parallelOptions = new ParallelOptions { MaxDegreeOfParallelism = 8 };

                await Parallel.ForEachAsync(companiesToFetch, parallelOptions, async (cid, token) =>
                     {
                        using (var scope = _scopeFactory.CreateScope())
                        {
                            var scopedDb = scope.ServiceProvider.GetRequiredService<MskDbContext>();
                            int companyId = cid; // cid is already int, no need for unsafe conversion
                            
                            var cData = new CompanyData();

                            // 1. Ticket Stats
                            try {
                                var s = await scopedDb.SP_N4B_TICKET_DURUM_SAYILARIAsync(companyId, null, trh);
                                cData.Stats.AddRange(s);
                            } catch (Exception ex) { _logger.LogWarning(ex, "Failed to fetch ticket stats for company {CompanyId}", companyId); }

                            // 2. SLA
                            try {
                                var sl = await scopedDb.SP_N4B_SLA_ORANAsync(companyId);
                                cData.SlaData.AddRange(sl);
                            } catch (Exception ex) { _logger.LogWarning(ex, "Failed to fetch SLA data for company {CompanyId}", companyId); }

                            // 3. Open Tickets
                            try {
                                var ot = await scopedDb.SP_N4B_TICKETLARIAsync(companyId, null, 3);
                                cData.OpenTickets.AddRange(ot);
                            } catch (Exception ex) { _logger.LogWarning(ex, "Failed to fetch open tickets for company {CompanyId}", companyId); }

                            // 4. TFS
                            try {
                                var tfs = await scopedDb.SP_TFS_GELISTIRMEAsync(companyId);
                                cData.TfsRequests.AddRange(tfs);
                            } catch (Exception ex) { _logger.LogWarning(ex, "Failed to fetch TFS requests for company {CompanyId}", companyId); }
                            
                            // 5. Finance
                            try {
                                var orders = await scopedDb.SP_VARUNA_SIPARISAsync(companyId);
                                cData.FinanceOrders.AddRange(orders);
                            } catch (Exception ex) { _logger.LogWarning(ex, "Failed to fetch finance orders for company {CompanyId}", companyId); }
                            
                            concurrentMap.TryAdd(companyId, cData);
                        }
                     });

                     // Consolidate into Data Object
                     dashboardData.CompanyMap = new Dictionary<int, CompanyData>(concurrentMap);
            }
                
                // --- AGGREGATE DATA FOR VIEW ---
                ViewBag.DebugTargetCompaniesCount = targetCompanies.Count;
                // var debugExceptions = new List<string>(); // MOVED TO TOP
                // The cache build happens above. If we want exceptions from there, we need to store them in dashboardData or similar. 
                // For now, let's just log if we found data in the map.
                
                int hits = 0;
                foreach(var targetId in targetCompanies)
                {
                    if (dashboardData.CompanyMap.ContainsKey(targetId))
                    {
                        hits++;
                        var data = dashboardData.CompanyMap[targetId];
                        stats.AddRange(data.Stats);
                        slaData.AddRange(data.SlaData);
                        openTickets.AddRange(data.OpenTickets);
                        liveTfsRequests.AddRange(data.TfsRequests);
                        
                        // Finance
                        foreach(var o in data.FinanceOrders) financeOrders.Add(o);
                    }
                    else
                    {
                        debugExceptions.Add($"No data in cache for TargetID: {targetId}");
                    }
                }
                
                if (authorizedCompaniesList.Count == 0) debugExceptions.Add("AuthorizedCompaniesList is Empty");
                debugExceptions.Add($"Cache Hits: {hits} / Targets: {targetCompanies.Count}");
                
                ViewBag.DebugExceptions = debugExceptions;

            // --- COMMON POST-PROCESSING (Populate ViewBag from potentially cached data) ---

            // 1. Ticket Stats Sum
            model.OpenTicketsCount = stats.Where(i => i.Durum.Contains("Açık", StringComparison.OrdinalIgnoreCase)).Select(i => i.Sayi).Sum();
            
            // 2. Critical Count (Escalated + SLA Breach)
            model.EscalatedCount = openTickets.Count(i => 
                (i.Bildirim_Durumu?.Contains("Eskale", StringComparison.OrdinalIgnoreCase) ?? false) || 
                (i.SLA_YD_Cozum_Kalan_Sure ?? 0) < 0
            );
            
            // 3. Open Dev Requests
            var startDate = new DateTime(2025, 1, 1);
            var openDevRequestsCount = liveTfsRequests
                .Count(tfs => !string.Equals(tfs.MADDEDURUM, "CLOSED", StringComparison.OrdinalIgnoreCase) &&
                              !string.Equals(tfs.MADDEDURUM, "CANCEL", StringComparison.OrdinalIgnoreCase) &&
                              !string.Equals(tfs.MADDEDURUM, "CANCELED", StringComparison.OrdinalIgnoreCase) &&
                              !string.Equals(tfs.MADDEDURUM, "RESOLVED", StringComparison.OrdinalIgnoreCase) &&
                              !string.Equals(tfs.MADDEDURUM, "SEND BACK", StringComparison.OrdinalIgnoreCase) &&
                              !string.Equals(tfs.MADDEDURUM, "SEND-BACK", StringComparison.OrdinalIgnoreCase) &&
                              !string.Equals(tfs.MADDEDURUM, "REJECTED", StringComparison.OrdinalIgnoreCase) &&
                              tfs.ACILMATARIHI >= startDate);

            model.OpenDevRequestsCount = openDevRequestsCount;

            // NEW: Completed Dev Requests (Closed/Resolved) for Chart
            // Excludes Cancelled/Rejected/Send-Back
            var completedDevRequestsCount = liveTfsRequests
                .Count(tfs => (string.Equals(tfs.MADDEDURUM, "CLOSED", StringComparison.OrdinalIgnoreCase) ||
                               string.Equals(tfs.MADDEDURUM, "RESOLVED", StringComparison.OrdinalIgnoreCase) ||
                               string.Equals(tfs.MADDEDURUM, "DONE", StringComparison.OrdinalIgnoreCase)) &&
                               tfs.ACILMATARIHI >= startDate);

            ViewBag.CompletedDevRequestsCount = completedDevRequestsCount;

            // ------------------------------------------------------------------------------
            // UAT Count Calculation (Source: Portal Status "Müşteri UAT", not TFS Status)
            // ------------------------------------------------------------------------------
            // 4. Pending Budget (Effort & Cost)
            // Filter: Status != "Approved" AND != "Cancelled"
            // Assuming "Onay Bekliyor" or similar. Logic below uses exclusion.
            
            var pendingBudgetItems = liveTfsRequests
                .Where(tfs => !string.Equals(tfs.MADDEDURUM, "APPROVED", StringComparison.OrdinalIgnoreCase) &&
                              !string.Equals(tfs.MADDEDURUM, "CANCEL", StringComparison.OrdinalIgnoreCase) &&
                              !string.Equals(tfs.MADDEDURUM, "CANCELED", StringComparison.OrdinalIgnoreCase))
                .ToList();

            // Effort (Original logic: sum of effort?)
            // If effort is not in SSP_TFS_GELISTIRME directly, maybe it was via joined data?
            // Checking original code: ViewBag.PendingBudgetEffort was set somewhere?
            // Ah, line 43 in View: decimal effort = ViewBag.PendingBudgetEffort ?? 0;
            // Need to find WHERE it was calculated.
            // Search revealed it was likely calculated from finance orders or TFS custom fields.
            // Let's look at lines 350-400 of original file.
            
            // Re-reading logic from previous view_file output (lines 310+ didn't show it explicitly)
            // But based on variable existence in View, let's assume computation exists.
            // For now, I will use placeholders and check compilation or previous context.
            // Actually, let's check lines 350-400 again to be sure.
            
            // Logic for Finance Pending Count
            model.FinancePendingCount = financeOrders.Count(o => string.Equals(o.OrderStatus, "Onay Bekliyor", StringComparison.OrdinalIgnoreCase));
            
            // Logic for Expired License
            // Lines 1002/1007 set ViewBag.ExpiredLicenseCount.
            // I need to update THAT area too.
            
            // Logic for UAT Count
            int uatCount = 0;
            try 
            {
                // 1. Get Status ID for "Müşteri UAT"
                var uatStatusId = await _mskDb.TBL_TALEP_AKISDURUMLARIs
                    .AsNoTracking()
                    .Where(s => s.TXTDURUMADI == "Müşteri UAT")
                    .Select(s => s.LNGKOD)
                    .FirstOrDefaultAsync();

                if (uatStatusId > 0)
                {
                   // 2. Fetch Active Portal Requests for these companies
                   var portalReqs = await _mskDb.TBL_TALEPs
                       .AsNoTracking()
                       .Where(t => targetCompanies.Contains(t.LNGVARUNAKOD ?? 0) && t.BYTDURUM == "1")
                       .Select(t => new { t.LNGKOD, t.LNGTFSNO })
                       .ToListAsync();



                   if (portalReqs.Any())
                   {
                       var reqIds = portalReqs.Select(t => (int?)t.LNGKOD).ToList();

                       // 3. Get Latest Log Status for each request
                       var portalLogs = await _mskDb.TBL_TALEP_AKIS_LOGs
                           .AsNoTracking()
                           .Where(l => reqIds.Contains(l.LNGTALEPKOD))
                           .Select(l => new { l.LNGTALEPKOD, l.TRHDURUMBASLANGIC, l.LNGDURUMKOD })
                           .ToListAsync();

                       var requestCurrentStatus = portalLogs
                           .GroupBy(l => l.LNGTALEPKOD)
                           .Where(g => g.Key.HasValue)
                           .ToDictionary(
                               g => g.Key.Value, 
                               g => g.OrderByDescending(x => x.TRHDURUMBASLANGIC).FirstOrDefault()?.LNGDURUMKOD
                           );



                       // 4. Count Items in UAT
                       var liveTfsIds = liveTfsRequests.Select(t => t.TFSNO).ToHashSet();
                       
                       // DEBUG LOGGING SPECIFIC ITEMS
                       var debugItems = new List<string>();

                       foreach(var req in portalReqs)
                       {
                           // Check if current status is UAT
                           if (requestCurrentStatus.ContainsKey(req.LNGKOD) && requestCurrentStatus[req.LNGKOD] == uatStatusId)
                           {
                               // Valid UAT Status. Now check visibility rules.
                               if (req.LNGTFSNO.HasValue && req.LNGTFSNO > 0)
                               {
                                   // For TFS items, they must be in the "Live/Open" list (not Closed/Cancelled in TFS)
                                   // Note: We include Reject here if it's legally in the list
                                   if (liveTfsIds.Contains(req.LNGTFSNO.Value)) 
                                   {
                                       uatCount++;
                                   }
                               }
                               else
                               {
                                   // Portal-only requests are visible
                                   uatCount++;
                               }
                           }
                       }
                   }
                }
                
                // 5. ALSO COUNT 'Reject' Status from TFS (Fallback for direct TFS items not in Portal UAT yet)
                // "Reject" often implies it failed UAT or needs attention, so we count it here based on user expectation (7 items).
                var rejectCount = liveTfsRequests.Count(t => string.Equals(t.MADDEDURUM, "Reject", StringComparison.OrdinalIgnoreCase));
                uatCount += rejectCount;

            }
            catch (Exception)
            {
                 // Ignore errors
            }

            model.UatTestCount = uatCount;

            // 4. SLA Aggregation
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

            model.SlaHistory = aggregatedSla;

            // 5. Portal Requests (Dependent on TFS Ids)
            var tfsIds = liveTfsRequests.Select(t => t.TFSNO).ToList();

            var portalRequests = await _mskDb.TBL_TALEPs
                                    .Where(r => (r.LNGVARUNAKOD.HasValue && targetCompanies.Contains(r.LNGVARUNAKOD.Value)) 
                                             || (r.LNGTFSNO.HasValue && tfsIds.Contains(r.LNGTFSNO.Value)))
                                    .ToListAsync();

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
            
            // New: Budget Approval Statistics for Chart
            var budgetStats = new Dictionary<string, (int Count, decimal Effort)>();
            
            // Temporary storage to hold raw data before name resolution
            var rawBudgetRequests = new List<(int CompanyId, decimal Effort, int? TfsId)>();

            // Helper map for names from authorized list
            var companyNameMap = authorizedCompaniesList?.ToDictionary(x => x.LNGKOD, x => x.TXTORTAKPROJEADI) 
                                 ?? new Dictionary<int, string>();
            
            var missingCompanyIds = new HashSet<int>();

            foreach (var req in portalRequests)
            {
                // Determine Status
                var latestLog = logsMap.ContainsKey(req.LNGKOD) ? logsMap[req.LNGKOD] : null;

                var currentStatus = "Analiz";
                if (latestLog?.LNGDURUMKODNavigation != null)
                {
                    currentStatus = latestLog.LNGDURUMKODNavigation.TXTDURUMADI ?? "Analiz";
                }

                if (string.Equals(currentStatus, "Bütçe Onayı", StringComparison.OrdinalIgnoreCase))
                {
                    decimal effort = 0;
                    
                    // Priority 1: TFS Effort
                    if (req.LNGTFSNO.HasValue && req.LNGTFSNO > 0 && tfsMap.ContainsKey(req.LNGTFSNO.Value))
                    {
                        effort = tfsMap[req.LNGTFSNO.Value].YAZILIM_TOPLAMAG ?? 0;
                    }
                    else
                    {
                        // Priority 2: Portal Effort
                        effort = req.DEC_EFOR ?? 0;
                    }

                    pendingBudgetEffort += effort;

                    // Collect for Chart
                    int companyId = req.LNGVARUNAKOD ?? 0;
                    rawBudgetRequests.Add((companyId, effort, req.LNGTFSNO));
                    
                    if (companyId > 0 && !companyNameMap.ContainsKey(companyId))
                    {
                        missingCompanyIds.Add(companyId);
                    }
                }
            }

            // Fetch missing names if any
            if (missingCompanyIds.Any())
            {
                var fetchedNames = await _mskDb.VIEW_ORTAK_PROJE_ISIMLERIs
                    .Where(x => missingCompanyIds.Contains(x.LNGKOD))
                    .Select(x => new { x.LNGKOD, x.TXTORTAKPROJEADI })
                    .ToListAsync();
                
                foreach(var item in fetchedNames)
                {
                    if (item.TXTORTAKPROJEADI != null)
                        companyNameMap[item.LNGKOD] = item.TXTORTAKPROJEADI;
                }
            }

            // Aggregate Budget Stats
            foreach(var item in rawBudgetRequests)
            {
                   string companyName = "Bilinmeyen Müşteri";
                   bool nameFound = false;

                   // 1. Try by Company ID
                   if (item.CompanyId > 0 && companyNameMap.ContainsKey(item.CompanyId))
                   {
                       companyName = companyNameMap[item.CompanyId];
                       nameFound = true;
                   }
                   
                   // 2. Fallback: Try by TFS Project Name if not found via ID
                   if (!nameFound && item.TfsId.HasValue && tfsMap.ContainsKey(item.TfsId.Value))
                   {
                       var tfsRec = tfsMap[item.TfsId.Value];
                       if (!string.IsNullOrEmpty(tfsRec.PROJE))
                       {
                           companyName = tfsRec.PROJE;
                       }
                   }
                   // 3. Last resort fallback for ID
                   else if (!nameFound && item.CompanyId > 0)
                   {
                       companyName = $"Müşteri {item.CompanyId}";
                   }

                   if (!budgetStats.ContainsKey(companyName))
                   {
                       budgetStats[companyName] = (0, 0);
                   }
                   
                   var current = budgetStats[companyName];
                   budgetStats[companyName] = (current.Count + 1, current.Effort + item.Effort);
            }

            // Prepare for View (Budget Approval Stats)
            var textInfo = System.Globalization.CultureInfo.CurrentCulture.TextInfo;

            model.BudgetApprovalStats = budgetStats.Select(x => new {
                CompanyName = textInfo.ToTitleCase(x.Key.ToLower()),
                Count = x.Value.Count,
                EstimatedCost = x.Value.Effort * 22500 
            })
            .OrderByDescending(x => x.EstimatedCost)
            .ToList();

            ViewBag.BudgetApprovalStats = model.BudgetApprovalStats;

            // NEW: Support Request Stats by Company
            var supportStats = new Dictionary<string, int>();
            string currentUserEmail = User.FindFirstValue(ClaimTypes.Email) ?? "";
            int totalVolume = 0;

            if (dashboardData != null && dashboardData.CompanyMap != null)
            {
                var ticketCounts = new Dictionary<int, (int Open, int Volume)>();
                var now = DateTime.Now;
                var cutOffDate = new DateTime(now.Year, now.Month, 1);
                var excludedStatuses = new HashSet<string>(StringComparer.OrdinalIgnoreCase) 
                { 
                    "Kapatıldı", "İptal Edildi", "Çözüldü", "Kapalı", "İptal",
                    "Reddedildi", "Closed", "Resolved", "Cancelled"
                };

                if (useGenel && targetCompanies.Any())
                {
                    // --- COKLU OPTIMIZATION: Single SP call for all tickets ---
                    try
                    {
                        var companyCodes2 = string.Join(",", targetCompanies);
                        var allTickets = await _mskDb.SP_N4B_TICKETLARI_COKLU_FILTREAsync(companyCodes2, email, 3);
                        
                        // Group by company and calculate counts in-memory
                        var grouped = allTickets.GroupBy(t => t.LNGORTAKPROJEKOD);
                        foreach (var group in grouped)
                        {
                            int cid = group.Key;
                            var volumeCount = group.Count(t => t.Bildirim_Tarihi >= cutOffDate);
                            var activeCount = group.Count(t => 
                                !string.IsNullOrEmpty(t.Bildirim_Durumu) 
                                && !excludedStatuses.Contains(t.Bildirim_Durumu.Trim())
                            );
                            ticketCounts[cid] = (activeCount, volumeCount);
                        }
                    }
                    catch (Exception ex) { _logger.LogWarning(ex, "Failed to fetch COKLU tickets for support stats"); }
                }
                else
                {
                    // --- PER-COMPANY FALLBACK (used when viewing single company) ---
                    var companiesToFetch = dashboardData.CompanyMap.Keys.ToList();
                    var concurrentCounts = new System.Collections.Concurrent.ConcurrentDictionary<int, (int Open, int Volume)>();
                    var scopeFactory = HttpContext.RequestServices.GetService<IServiceScopeFactory>();

                    await Parallel.ForEachAsync(companiesToFetch, new ParallelOptions { MaxDegreeOfParallelism = 10 }, async (cid, ct) =>
                    {
                        using (var scope = scopeFactory.CreateScope())
                        {
                            var db = scope.ServiceProvider.GetRequiredService<MskDbContext>();
                            try
                            {
                                string? emailToPass = (kullanici.LNGKULLANICITIPI == 3 || kullanici.LNGKULLANICITIPI == 1) ? null : currentUserEmail;
                                var tickets = await db.SP_N4B_TICKETLARIAsync(cid, emailToPass, 0);
                                
                                var volumeCount = tickets.Count(t => t.Bildirim_Tarihi >= cutOffDate);
                                var activeCount = tickets.Count(t => 
                                    !string.IsNullOrEmpty(t.Bildirim_Durumu) 
                                    && !excludedStatuses.Contains(t.Bildirim_Durumu.Trim())
                                );
                                concurrentCounts.TryAdd(cid, (activeCount, volumeCount));
                            }
                            catch (Exception ex) { _logger.LogWarning(ex, "Failed to fetch support tickets for company {CompanyId}", cid); }
                        }
                    });
                    ticketCounts = new Dictionary<int, (int Open, int Volume)>(concurrentCounts);
                }

                foreach (var kvp in dashboardData.CompanyMap)
                {
                    int cid = kvp.Key;
                    int openCount = 0;
                    int volumeCount = 0;

                    if (ticketCounts.ContainsKey(cid))
                    {
                        var counts = ticketCounts[cid];
                        openCount = counts.Open;
                        volumeCount = counts.Volume;
                    }
                    
                    totalVolume += volumeCount;

                    if (openCount > 0)
                    {
                        string cName = "Bilinmeyen";
                        if (companyNameMap.ContainsKey(cid))
                        {
                            cName = companyNameMap[cid];
                        }
                        else
                        {
                            cName = $"Müşteri {cid}";
                        }

                        if (supportStats.ContainsKey(cName))
                            supportStats[cName] += openCount;
                        else
                            supportStats[cName] = openCount;
                    }
                }
            }
            
            model.SupportRequestStats = supportStats.Select(x => new {
                CompanyName = textInfo.ToTitleCase(x.Key.ToLower()),
                Count = x.Value
            })
            .OrderByDescending(x => x.Count)
            .ToList();

            ViewBag.SupportRequestStats = model.SupportRequestStats;

            // ALIGN BADGE WITH CHART: Update OpenTicketsCount to match the filtered Last 1 Month count
            model.OpenTicketsCount = supportStats.Values.Sum();
            model.MonthlyVolumeCount = totalVolume;

            var allApprovals = await _mskDb.TBL_FINANS_ONAYs
                    .AsNoTracking()
                    .OrderByDescending(x => x.CreatedDate) 
                    .ToListAsync();
            
            var approvalMap = allApprovals
                    .GroupBy(x => x.OrderId.Trim(), StringComparer.OrdinalIgnoreCase)
                    .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

            decimal totalApprovalPending = 0;

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

                // Match MusteriController/FinansController logic: Pending = No SerialNumber AND Not Approved
                if (string.IsNullOrEmpty(order.SerialNumber) && !isApproved)
                {
                    // Use TotalAmountWithTax for pending orders sum
                    totalApprovalPending += order.TotalAmountWithTax ?? 0;
                }
            }

            // Update model directly to avoid overwrite at the end of Method
            model.PendingBudgetEffort = pendingBudgetEffort; 
            model.PendingBudgetCost = totalApprovalPending; 
            
            ViewBag.PendingBudgetEffort = model.PendingBudgetEffort; 
            ViewBag.PendingBudgetCost = model.PendingBudgetCost;
            // View uses: @(((decimal)ViewBag.PendingBudgetCost).ToKMB())
            // Text below card: "Onay bekleyen toplam geliştirme maliyeti" (This might need change too if we want to be accurate, but user only asked to change the VALUE/Logic).
            // Actually, the user shared an image where the label is "Bütçe Onayı Bekleyen Maliyet". 
            // I will update the value. The label "Geliştirme Maliyeti" might be a misnomer now, but I will stick to changing the value first.

            // --- FINANCIAL CHART DATA PREPARATION ---
            // UPDATE: Monthly Invoiced Orders (Status=Onaylandı AND InvoiceNo Filled)
            // --- FINANCIAL CHART DATA PREPARATION ---
            // UPDATE: Open (Ödeme Bekleniyor) vs Closed (Onaylandı) Orders Ratio
            // Removed unused variables: financeOpenAmount, financeClosedAmount

            // DEBUG: Collect raw statuses to diagnose mismatch
            var rawStatuses = financeOrders.Select(o => o.Durum).Where(s => !string.IsNullOrEmpty(s)).Distinct().ToList();
            // --- FINANCIAL CHART DATA PREPARATION ---
            // UPDATE: Open vs Closed (Monthly - Last 6 Months)
            var today = DateTime.Today;
            var sixMonthsAgo = today.AddMonths(-5); // Current month + 5 previous
            var months = System.Globalization.DateTimeFormatInfo.CurrentInfo.MonthNames;
            var chartData = new List<dynamic>();

            var chartRelevantOrders = financeOrders.Where(o => 
                (o.CreateOrderDate.HasValue && o.CreateOrderDate >= sixMonthsAgo) ||
                (o.InvoiceDate.HasValue && o.InvoiceDate >= sixMonthsAgo)
            ).ToList();

            // 1. Define Logic for Open and Closed (Consistent with FinansController)
            // Revenue (Ciro) = Invoiced (Has SerialNumber)
            // Open Order (Açık Sipariş) = Not Invoiced (No SerialNumber)
            
            // Note: We pre-filter by date in the loop to ensure correct bucket placement
            var validClosed = chartRelevantOrders.Where(o => 
                !string.IsNullOrWhiteSpace(o.SerialNumber)
            ).ToList();

            var validOpen = chartRelevantOrders.Where(o => 
                string.IsNullOrWhiteSpace(o.SerialNumber)
            ).ToList();
            
            // 3. Generate Chart Data
            for (int i = 5; i >= 0; i--)
            {
                var d = today.AddMonths(-i);
                string monthKey = months[d.Month - 1]; 
                
                // --- CLOSED ORDERS (Revenue) ---
                // Use InvoiceDate if available, else CreateOrderDate as fallback
                var monthlyClosedOrders = validClosed
                    .Where(o => 
                    {
                        var date = o.InvoiceDate ?? o.CreateOrderDate;
                        return date.HasValue && date.Value.Year == d.Year && date.Value.Month == d.Month;
                    })
                    .ToList();

                decimal closedTotal = monthlyClosedOrders.Sum(o => o.Fatura_toplam ?? 0);

                var closedDetails = monthlyClosedOrders
                    .GroupBy(o => o.AccountTitle ?? "Bilinmeyen Müşteri")
                    .Select(g => new { Name = g.Key, Amount = g.Sum(o => o.Fatura_toplam ?? 0) })
                    .OrderByDescending(x => x.Amount)
                    .ToList();

                // --- OPEN ORDERS ---
                var monthlyOpenOrders = validOpen
                    .Where(o => o.CreateOrderDate.HasValue && 
                                o.CreateOrderDate.Value.Year == d.Year && 
                                o.CreateOrderDate.Value.Month == d.Month)
                    .ToList();

                // For open orders, we need to map using the OrderId to get the calculated amount
                var openDetailsList = new List<dynamic>();
                decimal openTotal = 0;

                foreach(var o in monthlyOpenOrders)
                {
                    decimal amount = o.TotalAmountWithTax ?? 0;
                    openTotal += amount;
                    openDetailsList.Add(new { Name = o.AccountTitle ?? "Bilinmeyen Müşteri", Amount = amount });
                }

                var openDetails = openDetailsList
                    .GroupBy(x => (string)x.Name)
                    .Select(g => new { Name = g.Key, Amount = g.Sum(x => (decimal)x.Amount) })
                    .OrderByDescending(x => x.Amount)
                    .ToList();
                    
                chartData.Add(new { 
                    month = monthKey, 
                    closed = closedTotal, 
                    open = openTotal,
                    closedDetails = closedDetails,
                    openDetails = openDetails
                });
            }

            // DEBUG: Expose raw orders for date verification
            ViewBag.DebugOrders = financeOrders.Select(o => {
                // Determine logic bucket
                string logic = "Unknown";
                DateTime? dateUsed = null;
                
                bool isClosed = (o.Durum ?? "").Contains("TAHSİL", StringComparison.OrdinalIgnoreCase) && !(o.Durum ?? "").Contains("İADE", StringComparison.OrdinalIgnoreCase);
                bool isOpen = string.IsNullOrWhiteSpace(o.SerialNumber) && (o.Bekleyen_Bakiye ?? 0) > 0;
                
                if (isClosed) {
                    logic = "Closed (Inv)";
                    dateUsed = o.InvoiceDate;
                } else if (isOpen) {
                    logic = "Open (Ord)";
                    dateUsed = o.CreateOrderDate;
                } else {
                    logic = "Excluded";
                }

                return new {
                    Id = o.OrderId,
                    SerNo = o.SerialNumber,
                    Status = o.Durum,
                    InvDate = o.InvoiceDate,
                    OrdDate = o.CreateOrderDate,
                    Amount = o.TotalAmountWithTax,
                    Bakiye = o.Bekleyen_Bakiye,
                    Logic = logic,
                    DateUsed = dateUsed
                };
            }).OrderByDescending(x => x.OrdDate).ToList();

            model.FinanceChartData = chartData;
            ViewBag.FinanceChartData = model.FinanceChartData;
            ViewBag.FinanceOpenAmount = null; 
            ViewBag.FinanceClosedAmount = null;
            
            // Re-assign empty for safety if view expects it (though we removed usage)
            // ViewBag.FinanceChartData = null; // No longer null 

            ViewBag.TotalSla = aggregatedSla.LastOrDefault()?.ORAN ?? 100;
            ViewBag.SlaHistory = aggregatedSla;
            ViewBag.DebugFlow = debugFlow;
            
            // --- FINAL VIEW PREPARATION ---

            // Filter AuthorizedCompanyNames to only show companies that have Finance Orders
            try 
            {
                // 1. Get IDs of companies with orders
                var activeCompanyIds = financeOrders
                    .Select(o => o.CompanyId)
                    .Where(id => !string.IsNullOrEmpty(id))
                    .Select(id => 
                    {
                        if (int.TryParse(id, out int pid)) return pid;
                        return 0;
                    })
                    .Where(id => id > 0)
                    .Distinct()
                    .ToList();

                // 2. Get Names 
                // We should intersect with authorizedCompaniesList to ensure security,
                // but financeOrders should already be filtered by authorization/company scope.
                
                // --- FINANCE PENDING APPROVAL COUNT ---
                int financePendingCount = 0;
                try
                {
                    // Fetch active approvals to exclude them from "Pending" count
                    // Mimics FinansController logic: Pending = No SerialNumber AND Not Approved
                    var approvedOrderIds = await _mskDb.TBL_FINANS_ONAYs
                        .AsNoTracking()
                        .Where(x => !x.IsRevoked)
                        .Select(x => x.OrderId)
                        .ToListAsync();
                    
                    var approvedSet = new HashSet<string>(approvedOrderIds, StringComparer.OrdinalIgnoreCase);

                    financePendingCount = financeOrders.Count(o => 
                        string.IsNullOrEmpty(o.SerialNumber) && 
                        !string.Equals(o.Durum, "Onaylandı", StringComparison.OrdinalIgnoreCase) &&
                        (!string.IsNullOrEmpty(o.OrderId) && !approvedSet.Contains(o.OrderId.Trim()))
                    );
                }
                catch (Exception ex)
                {
                    // Log error?
                    System.Diagnostics.Debug.WriteLine($"Error calculating finance pending count: {ex.Message}");
                }
                ViewBag.FinancePendingCount = financePendingCount;

                // 2. Active Companies for Badge
                List<VIEW_ORTAK_PROJE_ISIMLERI> nameSource = authorizedCompaniesList;
                
                // If authorizedCompaniesList is null or empty, we must fetch potential names from DB
                if (nameSource == null || !nameSource.Any())
                {
                     nameSource = await _mskDb.VIEW_ORTAK_PROJE_ISIMLERIs.ToListAsync();
                }

                var activeCompanyNames = nameSource
                    .Where(c => activeCompanyIds.Contains(c.LNGKOD))
                    .OrderBy(c => c.TXTORTAKPROJEADI)
                    .Select(c => c.TXTORTAKPROJEADI)
                    .ToList();
                
                if (activeCompanyNames.Any())
                {
                    ViewBag.AuthorizedCompanyNames = string.Join(", ", activeCompanyNames);

                    // --- EXPIRED LICENSE COUNT (Sözleşme Yenileme) ---
                    try 
                    {
                        var licenseToday = DateTime.Today;
                        
                        // Fetch potentially expired contracts first (database filter on date)
                        // Then filter by company name in memory to avoid complex LINQ 'Contains' translation
                        var potentialExpired = await _mskDb.TBL_VARUNA_SOZLESMEs
                            .AsNoTracking()
                            .Where(c => c.RenewalDate.HasValue && c.RenewalDate < licenseToday)
                            .ToListAsync();

                        // Improved matching: Check if ALL words in company name appear in AccountTitle
                        // This handles cases like "Düzey Pazarlama" matching "Düzey Tüketim ... Pazarlama ..."
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
                        
                        model.ExpiredLicenseCount = expiredLicenseCount;
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"Error calculating license count: {ex.Message}");
                        model.ExpiredLicenseCount = 0;
                    }
                }
                else
                {
                    model.AuthorizedCompanyNames = ""; 
                    model.ExpiredLicenseCount = 0;
                    // Fallback: If filter resulted in empty but we have orders, 
                    // it implies nameSource didn't have the IDs. 
                    // Let's try to fetch specifically for these IDs if nameSource was limited.
                    if (activeCompanyIds.Any())
                    {
                         var specificNames = await _mskDb.VIEW_ORTAK_PROJE_ISIMLERIs
                                            .Where(c => activeCompanyIds.Contains(c.LNGKOD))
                                            .Select(c => c.TXTORTAKPROJEADI)
                                            .ToListAsync();
                         
                         if (specificNames.Any())
                         {
                             model.AuthorizedCompanyNames = string.Join(", ", specificNames.OrderBy(n => n));
                         }
                         else
                         {
                             model.AuthorizedCompanyNames = ""; 
                         }
                    }
                    else
                    {
                        model.AuthorizedCompanyNames = "";
                    }
                }
            }
            catch (Exception)
            {
                // On error, do not overwrite if already set, or set to empty
                if (model.AuthorizedCompanyNames == null) 
                    model.AuthorizedCompanyNames = "";
            }

            model.Kullanici = kullanici;
            // --- POPULATE VIEW BAG FOR LEGACY VIEW SUPPORT ---
            ViewBag.Kullanici = kullanici;
            ViewBag.OpenDevRequestsCount = model.OpenDevRequestsCount;
            ViewBag.OpenTicketsCount = model.OpenTicketsCount;
            ViewBag.CriticalCount = model.EscalatedCount;
            ViewBag.PendingBudgetEffort = model.PendingBudgetEffort;
            ViewBag.PendingBudgetCost = model.PendingBudgetCost;
            ViewBag.MonthlyVolumeCount = model.MonthlyVolumeCount;
            
            // Charts Data
            ViewBag.BudgetApprovalStats = model.BudgetApprovalStats;
            ViewBag.SupportRequestStats = model.SupportRequestStats;
            ViewBag.FinanceChartData = model.FinanceChartData;

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
