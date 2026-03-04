using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using UniCP.DbData;
using UniCP.Services;

namespace UniCP.Controllers
{
    [Authorize]
    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public class SystemPerformanceController : Controller
    {
        private readonly MskDbContext _mskDb;
        private readonly ZabbixService _zabbixService;
        private readonly ICompanyResolutionService _companyResolution;
        private readonly IUrlEncryptionService _urlEncryption;

        public SystemPerformanceController(
            MskDbContext mskDb, 
            ZabbixService zabbixService,
            ICompanyResolutionService companyResolution,
            IUrlEncryptionService urlEncryption)
        {
            _mskDb = mskDb;
            _zabbixService = zabbixService;
            _companyResolution = companyResolution;
            _urlEncryption = urlEncryption;
        }

        public async Task<IActionResult> Index(string range = "1h", string? filteredCompanyId = null)
        {
            ViewData["Title"] = "Sistem Performansı";
            ViewBag.ActiveRange = range;

            // Decrypt Company ID
            int? decryptedCompanyId = _urlEncryption.DecryptId(filteredCompanyId);

            // Date Range Configuration
            long timeFrom = 0;
            long timeTo = DateTimeOffset.Now.ToUnixTimeSeconds();
            int downsampleIntervalMinutes = 0; // 0 = detailed

            switch (range.ToLower())
            {
                case "1d":
                case "24h":
                    timeFrom = DateTimeOffset.Now.AddDays(-1).ToUnixTimeSeconds();
                    downsampleIntervalMinutes = 15; // Every 15 mins
                    break;
                case "1w":
                case "7d":
                    timeFrom = DateTimeOffset.Now.AddDays(-7).ToUnixTimeSeconds();
                    downsampleIntervalMinutes = 60; // Every 1 hour
                    break;
                case "1h":
                default:
                    timeFrom = DateTimeOffset.Now.AddHours(-1).ToUnixTimeSeconds();
                    downsampleIntervalMinutes = 0;
                    break;
            }

            // 1. Get User
            var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userIdStr)) return RedirectToAction("Login", "Account");
            int userId = int.Parse(userIdStr);
            var kullanici = await _mskDb.TBL_KULLANICIs.FirstOrDefaultAsync(i => i.LNGIDENTITYKOD == userId);
            if (kullanici == null) return RedirectToAction("Login", "Account");

            // 2. Use CompanyResolutionService
            var companyResolution = await _companyResolution.ResolveCompaniesAsync(
                kullanici.LNGKOD,
                decryptedCompanyId,
                HttpContext);

            var projectCodes = companyResolution.TargetCompanyIds;
            var authorizedCompaniesList = companyResolution.AuthorizedCompanies;

            // Handle cookie setting for filtered company (Refactored to service)
            if (decryptedCompanyId.HasValue)
            {
                if (decryptedCompanyId.Value == -1)
                {
                    _companyResolution.ClearCompanyCookie(HttpContext);
                }
                else if (decryptedCompanyId.Value > 0 && projectCodes.Contains(decryptedCompanyId.Value))
                {
                    _companyResolution.SetCompanyCookie(HttpContext, decryptedCompanyId.Value);
                }
            }

            // Populate ViewBag for _CompanyFilter
            ViewBag.AuthorizedCompanies = authorizedCompaniesList;
            ViewBag.SelectedCompanyId = companyResolution.SelectedCompanyId;

            // 3. Get Zabbix Hosts
            var hosts = await _mskDb.TBL_ZABBIX_HOST_LISTs
                                .Where(h => h.LNGORTAKPROJEKOD.HasValue && projectCodes.Contains(h.LNGORTAKPROJEKOD.Value))
                                .ToListAsync();

            if (!hosts.Any())
            {
                if (decryptedCompanyId.HasValue)
                     ViewBag.Error = "Seçilen firmaya ait sistem izleme kaydı bulunamadı.";
                else
                     ViewBag.Error = "Projenize ait sistem izleme kaydı bulunamadı.";
                
                return View(new List<ZabbixHostViewModel>());
            }

            // 4. Fetch Data for Each Host (Parallel Execution)
            try
            {
                // Pre-authenticate once to avoid parallel login race conditions
                await _zabbixService.EnsureAuthenticatedAsync();

                // 5. Fetch Data for Each Host (Parallel Execution)
                var tasks = hosts.Select(async host =>
                {
                    var viewModel = new ZabbixHostViewModel
                    {
                        HostName = host.NAME,
                        HostId = host.HOSTID.ToString()
                    };



                    // Get Items
                    // CPU - Try broad search first (util*) or load*
                    // ... existing logic ...
                    var cpuItems = await _zabbixService.GetItemsAsync(host.HOSTID.ToString(), "system.cpu.util*");
                    if (!cpuItems.Any()) cpuItems = await _zabbixService.GetItemsAsync(host.HOSTID.ToString(), "system.cpu.load*");
                    
                    var cpuItem = cpuItems.FirstOrDefault(); // Pick first compatible
                    
                    if (cpuItem != null)
                    {
                        var history = await _zabbixService.GetHistoryAsync(cpuItem.ItemId, 0, timeFrom, timeTo);
                        
                        // Downsampling
                        if (downsampleIntervalMinutes > 0 && history.Any())
                        {
                             var grouped = history
                                .Select(h => new { Time = h.GetDateTime(), Val = h.GetValue() })
                                .GroupBy(x => new DateTime(x.Time.Ticks - (x.Time.Ticks % TimeSpan.FromMinutes(downsampleIntervalMinutes).Ticks)))
                                .Select(g => new DataPoint { Time = g.Key, Value = g.Max(x => x.Val) }) // Max for CPU
                                .OrderBy(x => x.Time)
                                .ToList();
                             viewModel.CpuHistory = grouped;
                        }
                        else
                        {
                            viewModel.CpuHistory = history.Select(h => new DataPoint { Time = h.GetDateTime(), Value = h.GetValue() }).ToList();
                        }
                        
                        // User Request: Show Max value for 1h filter
                        if (string.Equals(range, "1h", StringComparison.OrdinalIgnoreCase) && viewModel.CpuHistory.Any())
                        {
                             viewModel.CurrentCpu = viewModel.CpuHistory.Max(x => x.Value);
                        }
                        else
                        {
                             viewModel.CurrentCpu = viewModel.CpuHistory.LastOrDefault()?.Value ?? history.LastOrDefault()?.GetValue() ?? 0;
                        }
                    }

                    // RAM
                    var ramItem = (await _zabbixService.GetItemsAsync(host.HOSTID.ToString(), "vm.memory.util")).FirstOrDefault();
                    if (ramItem != null)
                    {
                         var history = await _zabbixService.GetHistoryAsync(ramItem.ItemId, 0, timeFrom, timeTo);
                         
                         // Downsampling
                        if (downsampleIntervalMinutes > 0 && history.Any())
                        {
                             var grouped = history
                                .Select(h => new { Time = h.GetDateTime(), Val = h.GetValue() })
                                .GroupBy(x => new DateTime(x.Time.Ticks - (x.Time.Ticks % TimeSpan.FromMinutes(downsampleIntervalMinutes).Ticks)))
                                .Select(g => new DataPoint { Time = g.Key, Value = g.Average(x => x.Val) })
                                .OrderBy(x => x.Time)
                                .ToList();
                             viewModel.RamHistory = grouped;
                        }
                        else
                        {
                             viewModel.RamHistory = history.Select(h => new DataPoint { Time = h.GetDateTime(), Value = h.GetValue() }).ToList();
                        }

                         viewModel.CurrentRam = viewModel.RamHistory.LastOrDefault()?.Value ?? history.LastOrDefault()?.GetValue() ?? 0;
                    }

                    // DISK LATENCY (Windows specific mostly, keep best effort)
                     var latencyItems = await _zabbixService.GetItemsAsync(host.HOSTID.ToString(), "perf_counter*Avg. Disk sec*");
                    var bestLatencyItem = latencyItems.FirstOrDefault(i => i.Key.Contains("Avg. Disk sec/Transfer") && i.Key.Contains("_Total")) 
                                        ?? latencyItems.FirstOrDefault(i => i.Key.Contains("Avg. Disk sec/Read") && i.Key.Contains("_Total"))
                                        ?? latencyItems.FirstOrDefault(i => i.Key.Contains("Avg. Disk sec/Read") && i.Key.Contains("C:"))
                                        ?? latencyItems.FirstOrDefault(i => i.Key.Contains("Avg. Disk sec/Read"));

                    if (bestLatencyItem != null && double.TryParse(bestLatencyItem.LastValue, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out double latencySeconds))
                    {
                         viewModel.DiskLatencyMs = latencySeconds * 1000;
                    }

                    // DISK Usage (Instant) - Support Linux (/) and Windows (C:)
                    // Try C: first (Windows)
                    var diskItem = (await _zabbixService.GetItemsAsync(host.HOSTID.ToString(), "vfs.fs.dependent.size[C:,pused]")).FirstOrDefault();
                    var diskTotalItem = (await _zabbixService.GetItemsAsync(host.HOSTID.ToString(), "vfs.fs.dependent.size[C:,total]")).FirstOrDefault();
                    
                    // Fallback to Root / (Linux)
                    if (diskItem == null) 
                    {
                        diskItem = (await _zabbixService.GetItemsAsync(host.HOSTID.ToString(), "vfs.fs.dependent.size[/,pused]")).FirstOrDefault()
                                   ?? (await _zabbixService.GetItemsAsync(host.HOSTID.ToString(), "vfs.fs.size[/,pused]")).FirstOrDefault();

                        diskTotalItem = (await _zabbixService.GetItemsAsync(host.HOSTID.ToString(), "vfs.fs.dependent.size[/,total]")).FirstOrDefault()
                                        ?? (await _zabbixService.GetItemsAsync(host.HOSTID.ToString(), "vfs.fs.size[/,total]")).FirstOrDefault();
                    }

                    // Original Windows Fallback (Dependent -> Standard)
                    if (diskTotalItem == null) diskTotalItem = (await _zabbixService.GetItemsAsync(host.HOSTID.ToString(), "vfs.fs.size[C:,total]")).FirstOrDefault();
                    if (diskItem == null) diskItem = (await _zabbixService.GetItemsAsync(host.HOSTID.ToString(), "vfs.fs.size[C:,pused]")).FirstOrDefault();

                    if (diskTotalItem != null && double.TryParse(diskTotalItem.LastValue, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out double totalBytes))
                    {
                        viewModel.DiskTotalBytes = totalBytes;
                    }
                    if (diskItem != null && double.TryParse(diskItem.LastValue, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out double val))
                    {
                        viewModel.DiskUsagePercent = val;
                    }

                    return viewModel;
                });

                var viewModels = (await Task.WhenAll(tasks)).ToList();
                return View(viewModels);
            }
            catch (Exception ex)
            {
                ViewBag.Error = "Zabbix hatası: " + ex.Message;
                return View(new List<ZabbixHostViewModel>());
            }
        }

        [Authorize]
        public async Task<IActionResult> Debug(string hostName)
        {
             var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
             if (string.IsNullOrEmpty(userIdStr)) return Content("Oturum bulunamadı.");
             int userId = int.Parse(userIdStr);
             var kullanici = await _mskDb.TBL_KULLANICIs.FirstOrDefaultAsync(i => i.LNGIDENTITYKOD == userId);
             if (kullanici == null) return Content("Kullanıcı bulunamadı.");

             var companyResolution = await _companyResolution.ResolveCompaniesAsync(kullanici.LNGKOD, null, HttpContext);
             var projectCodes = companyResolution.TargetCompanyIds;

             // Find Host ensuring it belongs to authorized projects
             var hosts = await _mskDb.TBL_ZABBIX_HOST_LISTs
                 .Where(h => h.LNGORTAKPROJEKOD.HasValue && projectCodes.Contains(h.LNGORTAKPROJEKOD.Value))
                 .ToListAsync();
                 
             var targetHost = hosts.FirstOrDefault(h => h.NAME.Contains(hostName, StringComparison.OrdinalIgnoreCase));
             
             if (targetHost == null) return Content($"Host '{hostName}' bulunamadı veya erişim yetkiniz yok.");

             // Get All Items
             var items = await _zabbixService.GetItemsAsync(targetHost.HOSTID.ToString(), "");
             
             var sb = new System.Text.StringBuilder();
             sb.AppendLine($"Host: {targetHost.NAME} (ID: {targetHost.HOSTID})");
             sb.AppendLine("Items Found: " + items.Count);
             sb.AppendLine("--------------------------------------------------");
             
             foreach(var item in items.OrderBy(i => i.Key))
             {
                 sb.AppendLine($"Key: {item.Key} | Name: {item.Name} | LastValue: {item.LastValue} | Units: {item.Units}");
             }
             
             return Content(sb.ToString());
        }
    }

    public class ZabbixHostViewModel
    {
        public string HostName { get; set; } = "";
        public string HostId { get; set; } = "";
        public List<DataPoint> CpuHistory { get; set; } = new();
        public double CurrentCpu { get; set; }
        public List<DataPoint> RamHistory { get; set; } = new();
        public double CurrentRam { get; set; }
        public double DiskUsagePercent { get; set; }
        public double DiskLatencyMs { get; set; }
        public double DiskTotalBytes { get; set; }
    }

    public class DataPoint
    {
        public DateTime Time { get; set; }
        public double Value { get; set; }
    }


}
