using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using UniCP.Models.Kullanici;
using UniCP.DbData;
using UniCP.Models.Finans;
using UniCP.Models.MsK;
using UniCP.Models.MsK.SpModels;
using Microsoft.EntityFrameworkCore;
using PdfSharpCore.Drawing;
using PdfSharpCore.Pdf;
using System.IO;
using UniCP.Services;

namespace UniCP.Controllers
{
    [Authorize(Roles = UniCP.Constants.AppConstants.Roles.Finans + "," + UniCP.Constants.AppConstants.Roles.Admin)]
    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public class FinansController : Controller
    {
        private readonly MskDbContext _mskDb;
        private readonly UniCP.Models.IEmailService _emailService;
        private readonly UserManager<AppUser> _userManager;
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ICompanyResolutionService _companyResolution;
        private readonly ILogger<FinansController> _logger;
        private readonly IUrlEncryptionService _urlEncryption;

        public FinansController(
            MskDbContext mskDb, 
            UniCP.Models.IEmailService emailService, 
            UserManager<AppUser> userManager, 
            IServiceScopeFactory scopeFactory,
            ICompanyResolutionService companyResolution,
            ILogger<FinansController> logger,
            IUrlEncryptionService urlEncryption)
        {
            _mskDb = mskDb;
            _emailService = emailService;
            _userManager = userManager;
            _scopeFactory = scopeFactory;
            _companyResolution = companyResolution;
            _logger = logger;
            _urlEncryption = urlEncryption;
        }

        public async Task<IActionResult> Index(string filter = "3months", DateTime? startDate = null, DateTime? endDate = null, string? filteredCompanyId = null)
        {
            var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userIdStr)) return RedirectToAction("Login", "Account");

            // Decrypt Company ID
            int? decryptedCompanyId = _urlEncryption.DecryptId(filteredCompanyId);

            int userId = int.Parse(userIdStr);
            var kullanici = await _mskDb.TBL_KULLANICIs.FirstOrDefaultAsync(i => i.LNGIDENTITYKOD == userId);
            
            if (kullanici == null) return RedirectToAction("Login", "Account");

            // Use CompanyResolutionService
            var companyResolution = await _companyResolution.ResolveCompaniesAsync(
                kullanici.LNGKOD,
                decryptedCompanyId,
                HttpContext);

            var targetCompanies = companyResolution.TargetCompanyIds;
            var authorizedCompaniesList = companyResolution.AuthorizedCompanies;

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

            ViewBag.AuthorizedCompanies = authorizedCompaniesList;
            ViewBag.SelectedCompanyId = companyResolution.SelectedCompanyId;

            // 1. Parallel Company Fetching (Safe because GetFilteredOrdersAsync uses main context or we make it safe)
            // But main context _mskDb cannot be used concurrently.
            // So we must use Scope here too if we want parallel.
            // For now, let's keep Company Fetch sequential or use Scope if distinct.
            // Since we updated to DbContextPooling, main context is cheap to recreate but we need scopes for concurrency.
            
            var varunaSiparisler = new System.Collections.Concurrent.ConcurrentBag<SpVarunaSiparisResult>();
            
            // Thread-safe accumulators for global stats (ignoring date filter)
            decimal globalPendingTotal = 0;
            decimal globalOverdueTotal = 0;
            int globalOverdueCount = 0;
            int globalNotDueCount = 0;
            object statLock = new object();

            // Limit concurrency to 5 companies at a time
            var companyOptions = new ParallelOptions { MaxDegreeOfParallelism = 5 };
            
            await Parallel.ForEachAsync(targetCompanies, companyOptions, async (cid, token) =>
            {
                using (var scope = _scopeFactory.CreateScope())
                {
                    var scopedDb = scope.ServiceProvider.GetRequiredService<MskDbContext>();
                    try 
                    {
                        var orders = await scopedDb.SP_VARUNA_SIPARISAsync(cid);
                        var now = DateTime.Now;

                        // 1. Calculate Global Stats (Open Invoices) from FULL list
                        // regardless of the visual date filter
                        var openOrders = orders.Where(x => (x.Bekleyen_Bakiye ?? 0) > 0).ToList();
                        
                        decimal localPending = 0;
                        decimal localOverdue = 0;
                        int localOverdueCnt = 0;
                        int localNotDueCnt = 0;

                        foreach(var o in openOrders)
                        {
                            localPending += o.Bekleyen_Bakiye ?? 0;
                            if ((o.Gecikme_Gun ?? 0) > 0)
                            {
                                localOverdue += o.Bekleyen_Bakiye ?? 0;
                                localOverdueCnt++;
                            }
                            else
                            {
                                localNotDueCnt++;
                            }
                        }

                        lock(statLock)
                        {
                            globalPendingTotal += localPending;
                            globalOverdueTotal += localOverdue;
                            globalOverdueCount += localOverdueCnt;
                            globalNotDueCount += localNotDueCnt;
                        }

                        // 2. Apply Visual Filter for Grid/Charts
                        IEnumerable<SpVarunaSiparisResult> query = orders;

                        if (startDate.HasValue && endDate.HasValue)
                        {
                            var end = endDate.Value.Date.AddDays(1).AddTicks(-1);
                            query = query.Where(o => o.CreateOrderDate >= startDate.Value && o.CreateOrderDate <= end);
                        }
                        else
                        {
                            switch (filter.ToLower())
                            {
                                case "3months":
                                    query = query.Where(o => o.CreateOrderDate >= now.AddMonths(-3));
                                    break;
                                case "year":
                                    query = query.Where(o => o.CreateOrderDate >= now.AddYears(-1));
                                    break;
                                case "month":
                                default:
                                    query = query.Where(o => o.CreateOrderDate >= now.AddMonths(-1));
                                    break;
                            }
                        }
                        
                        foreach(var order in query) varunaSiparisler.Add(order);
                    }
                    catch (Exception ex) { _logger.LogWarning(ex, "Failed to fetch finance orders for company {CompanyId}", cid); }
                }
            });


            // Calculate Previous Period Stats (Optimization: Do this once effectively or parallelize if needed, keeping simple for now)
            CalculateComparisonStats(targetCompanies.FirstOrDefault(), filter, startDate, endDate, varunaSiparisler.Sum(x => x.TotalAmountWithTax ?? 0));

            var filteredList = varunaSiparisler.ToList();

            // Apply Approvals Override
            try 
            {
                 // Fetch ALL approvals (active and revoked) with User Info
                 // Optimization: Use AsNoTracking() since this is read-only for view projection
                var allApprovals = await _mskDb.TBL_FINANS_ONAYs
                    .AsNoTracking()
                    .Include(x => x.CreatedBy)
                    .Include(x => x.RevokedBy)
                    .OrderByDescending(x => x.CreatedDate) 
                    .ToListAsync();
                
                var approvalMap = allApprovals
                    .GroupBy(x => x.OrderId.Trim(), StringComparer.OrdinalIgnoreCase)
                    .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

                foreach (var order in filteredList)
                {
                   if (!string.IsNullOrEmpty(order.OrderId) && approvalMap.ContainsKey(order.OrderId.Trim()))
                   {
                       var approval = approvalMap[order.OrderId.Trim()];
                       if (!approval.IsRevoked)
                       {
                           order.Durum = "Onaylandı";
                       }
                   }
                }
                ViewBag.ApprovalMap = approvalMap;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Index] Error applying approvals: {ex.Message}");
            }

            // Custom Sorting: 
            // 1. "Onay Bekliyor" (No SerialNumber AND Not Approved) -> Top Priority
            // 2. "Ödeme Bekleniyor" (Durum is null or "Ödeme Bekleniyor") AND Has Serial Number -> Second Priority
            // 3. Then by Date Descending
            filteredList = filteredList
                .OrderByDescending(x => string.IsNullOrEmpty(x.SerialNumber) && x.Durum != "Onaylandı")
                .ThenByDescending(x => (x.Durum == null || x.Durum == "Ödeme Bekleniyor") && !string.IsNullOrEmpty(x.SerialNumber))
                .ThenByDescending(x => x.CreateOrderDate)
                .ToList();

            ViewBag.TotalFilteredAmount = filteredList.Sum(x => x.TotalAmountWithTax ?? 0);
            
            ViewBag.ActiveFilter = filter;
            if (startDate.HasValue) ViewBag.StartDate = startDate.Value.ToString("yyyy-MM-dd");
            if (endDate.HasValue) ViewBag.EndDate = endDate.Value.ToString("yyyy-MM-dd");

            // Chart Data Logic (Optimized Parallel Fetch)
            var chartGroups = new System.Collections.Concurrent.ConcurrentDictionary<string, decimal>();
            decimal totalFromDetails = 0;
            object lockObj = new object();

            // Limit concurrent DB calls to 15 to avoid saturation
            // This fixes the N+1 problem by running 15 calls in parallel instead of 1
            var parallelOptions = new ParallelOptions { MaxDegreeOfParallelism = 15 };
            
            await Parallel.ForEachAsync(filteredList, parallelOptions, async (order, token) =>
            {
                if (string.IsNullOrEmpty(order.OrderId)) return;

                using (var scope = _scopeFactory.CreateScope())
                {
                     var scopedDb = scope.ServiceProvider.GetRequiredService<MskDbContext>();
                     try 
                     {
                        // Use Raw SQL or SP if method is not async safe on context
                        // Sp calls on context are synchronous usually unless async generated
                        // Assuming SP_VARUNA_SIPARIS_DETAY is available as Sync extension usually, wrapping in Task.Run if needed
                        // But context is not thread safe, that's why we have SCOPE.
                        
                        // We need the data.
                        var details = scopedDb.SP_VARUNA_SIPARIS_DETAY(order.OrderId.Trim()).ToList();
                        
                        decimal calculatedTotal = 0;
                        foreach (var item in details)
                        {
                            var amount = item.NetLineTotalWithTax ?? 0;
                            calculatedTotal += amount;
                            
                            if (amount == 0) continue;

                            string group = "Diğer";
                            string pName = (item.ProductName ?? "").ToLower(new System.Globalization.CultureInfo("tr-TR"));

                            if (pName.Contains("bakım") || pName.Contains("destek") || pName.Contains("sla")) 
                                group = "Bakım ve Destek";
                            else if (pName.Contains("hizmet") || pName.Contains("danışmanlık") || pName.Contains("geliştirme") || pName.Contains("adam/gün") || pName.Contains("analiz") || pName.Contains("yazılım")) 
                                group = "Hizmet";
                            else if (pName.Contains("lisans"))
                                group = "Lisans";

                            chartGroups.AddOrUpdate(group, amount, (key, existingVal) => existingVal + amount);
                            lock(lockObj) { totalFromDetails += amount; }
                        }
                        
                        // Fix: If Header Total is 0, use Sum of Details
                        if ((order.TotalAmountWithTax ?? 0) == 0 && calculatedTotal > 0)
                        {
                            order.TotalAmountWithTax = calculatedTotal;
                        }
                     }
                     catch (Exception ex) { _logger.LogWarning(ex, "Failed to calculate total for order {OrderId}", order.OrderId); }
                }
            });

            // Recalculate Total Filtered Amount after correcting zero-values
            ViewBag.TotalFilteredAmount = filteredList.Sum(x => x.TotalAmountWithTax ?? 0);

            // Normalization: Ensure Chart Sum equals Header Sum
            decimal totalHeader = filteredList.Sum(x => x.TotalAmountWithTax ?? 0);
            decimal difference = totalHeader - totalFromDetails;

            if (difference > 0)
            {
                 chartGroups.AddOrUpdate("Diğer", difference, (key, existing) => existing + difference);
            }

            if (!chartGroups.IsEmpty)
            {
                 var sortedGroups = chartGroups.OrderByDescending(x => x.Value).ToList();
                 ViewBag.ChartData = new {
                    Labels = sortedGroups.Select(x => x.Key).ToList(),
                    Values = sortedGroups.Select(x => x.Value).ToList(),
                    Total = sortedGroups.Sum(x => x.Value)
                };
            }
            else
            {
                ViewBag.ChartData = null;
            }

            // Calculate Pending Payment (Global - Not Filtered)
            ViewBag.PendingPayment = globalPendingTotal; 
            ViewBag.OverdueCount = globalOverdueCount;
            ViewBag.WaitingForMaturityCount = globalNotDueCount;

            // Calculate Late Invoice Amount (Global - Not Filtered)
            ViewBag.LateInvoiceAmount = globalOverdueTotal;

            return View(filteredList);
        }

        public async Task<IActionResult> DownloadStatement(string filter = "month", DateTime? startDate = null, DateTime? endDate = null, string? filteredCompanyId = null)
        {
            // ... existing PDF logic ...
            // (Keeping the existing method as is, or I could remove it if the user wants purely replacement. 
            // The prompt said "PDF yerine Excel olsun" implies replacement, but keeping it as a legacy or alternative might be safer unless explicitly told to delete.
            // Actually, user said "buradaki indirme formatı da excel olsun", implies change. 
            // I will keep existing PDF method for now just in case, but the UI will point to Excel.)
            
            var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userIdStr)) return RedirectToAction("Login", "Account");

            int userId = int.Parse(userIdStr);
            var kullanici = _mskDb.TBL_KULLANICIs.FirstOrDefault(i => i.LNGIDENTITYKOD == userId);
            if (kullanici == null) return RedirectToAction("Login", "Account");

            int firmaKod = kullanici.LNGORTAKFIRMAKOD ?? 2;
            var projectClaim = User.FindFirst("ProjectCode");
            if (projectClaim != null && int.TryParse(projectClaim.Value, out int selectedProject))
            {
                firmaKod = selectedProject;
            }

            var orders = await GetFilteredOrdersAsync(firmaKod, filter, startDate, endDate, filteredCompanyId);

            using (var stream = new MemoryStream())
            {
                var document = new PdfDocument();
                // ... (existing PDF generation code) ...
                // Re-creating functionality inside block to ensure it compiles if I were replacing, 
                // but since I am appending/modifying, I should just ADD the new method.
                // However, `replace_file_content` replaces a block.
                // I will Add ExportExcel AFTER DownloadStatement.
                 var page = document.AddPage();
                var gfx = XGraphics.FromPdfPage(page);
                var fontTitle = new XFont("Arial", 18, XFontStyle.Bold);
                var fontHeader = new XFont("Arial", 10, XFontStyle.Bold);
                var fontRow = new XFont("Arial", 9, XFontStyle.Regular);
                var fontBoldRow = new XFont("Arial", 9, XFontStyle.Bold);
                var fontSmall = new XFont("Arial", 8, XFontStyle.Italic);

                double yPoint = 40;
                double margin = 40;
                double pageWidth = page.Width;
                double contentWidth = pageWidth - (2 * margin);

                // Title
                gfx.DrawString("Hesap Ekstresi", fontTitle, XBrushes.Black, new XRect(0, yPoint, pageWidth, 30), XStringFormats.TopCenter);
                yPoint += 40;
                
                // Filter Info
                string filterName = filter switch 
                {
                    "month" => "Aylık",
                    "3months" => "3 Aylık",
                    "year" => "Yıllık",
                    _ => filter
                };

                string dateRange = startDate.HasValue && endDate.HasValue 
                    ? $"{startDate.Value:dd.MM.yyyy} - {endDate.Value:dd.MM.yyyy}"
                    : $"Filtre: {filterName}";
                gfx.DrawString($"Rapor Tarihi: {DateTime.Now:dd.MM.yyyy HH:mm}", fontSmall, XBrushes.DarkGray, margin, yPoint);
                gfx.DrawString($"Aralık: {dateRange}", fontSmall, XBrushes.DarkGray, pageWidth - margin - 100, yPoint);
                yPoint += 20;

                gfx.DrawLine(XPens.Gray, margin, yPoint, pageWidth - margin, yPoint);
                yPoint += 20;

                decimal grandTotal = 0;

                foreach (var order in orders)
                {
                    // Check page break for Order Header + at least one item
                    if (yPoint > page.Height - 80)
                    {
                        page = document.AddPage();
                        gfx = XGraphics.FromPdfPage(page);
                        yPoint = 40;
                    }

                    // Order Header
                    var status = order.OrderStatus == "Closed" ? "Kapalı" : (order.OrderStatus ?? "-");
                    var orderDate = order.CreateOrderDate?.ToString("dd.MM.yyyy") ?? "-";
                    
                    // Removed gray background
                    // var rectHeader = new XRect(margin, yPoint, contentWidth, 20);
                    // gfx.DrawRectangle(XBrushes.WhiteSmoke, rectHeader);
                    
                    gfx.DrawString($"{order.SerialNumber} | Tar: {orderDate} | Durum: {status}", fontHeader, XBrushes.Black, margin + 5, yPoint + 5);
                    yPoint += 25;

                    // Fetch Details
                    var details = _mskDb.SP_VARUNA_SIPARIS_DETAY(order.OrderId).ToList();

                    // Table Header
                    // Layout: 
                    // Product: Left (margin + 10)
                    // Quantity: Right Aligned (Box: margin + 300, Width 50) -> Ends at 350
                    // Unit Price: Right Aligned (Box: margin + 360, Width 80) -> Ends at 440
                    // Total: Right Aligned (Box: margin + 450, Width 80) -> Ends at 530
                    
                    var brushHeader = XBrushes.DarkSlateGray; // Darker than details, lighter than black
                    var brushRow = XBrushes.SlateGray;      // Lighter detail color

                    gfx.DrawString("Ürün", fontBoldRow, brushHeader, margin + 10, yPoint);
                    gfx.DrawString("Miktar", fontBoldRow, brushHeader, new XRect(margin + 260, yPoint, 50, 15), XStringFormats.TopRight);
                    gfx.DrawString("Birim Fiyat", fontBoldRow, brushHeader, new XRect(margin + 320, yPoint, 80, 15), XStringFormats.TopRight); // Closer to quantity
                    gfx.DrawString("Satır Tutarı", fontBoldRow, brushHeader, new XRect(margin + 410, yPoint, 100, 15), XStringFormats.TopRight);
                    yPoint += 15;

                    foreach (var item in details)
                    {
                        if (yPoint > page.Height - 40)
                        {
                            page = document.AddPage();
                            gfx = XGraphics.FromPdfPage(page);
                            yPoint = 40;
                        }

                        var productName = item.ProductName?.Length > 40 ? item.ProductName.Substring(0, 37) + "..." : item.ProductName;
                        
                        gfx.DrawString(productName ?? "-", fontRow, brushRow, margin + 10, yPoint);
                        
                        // Right-aligned numeric values
                        gfx.DrawString((item.Quantity ?? 0).ToString("N0"), fontRow, brushRow, new XRect(margin + 260, yPoint, 50, 15), XStringFormats.TopRight);
                        gfx.DrawString((item.UnitPrice ?? 0).ToString("N2"), fontRow, brushRow, new XRect(margin + 320, yPoint, 80, 15), XStringFormats.TopRight);
                        gfx.DrawString((item.NetLineTotalWithTax ?? 0).ToString("N2"), fontRow, brushRow, new XRect(margin + 410, yPoint, 100, 15), XStringFormats.TopRight);
                        
                        yPoint += 15;
                    }

                    // Order Subtotal
                    var orderTotal = order.TotalAmountWithTax ?? 0;
                    grandTotal += orderTotal;

                    gfx.DrawLine(XPens.LightGray, margin + 300, yPoint, pageWidth - margin, yPoint);
                    yPoint += 5;
                    gfx.DrawString($"Sipariş Toplamı: {orderTotal:C2}", fontBoldRow, XBrushes.Black, new XRect(0, yPoint, pageWidth - margin, 15), XStringFormats.TopRight);
                    yPoint += 25; // Space between orders
                }

                // Grand Total
                yPoint += 10;
                if (yPoint > page.Height - 50) 
                {
                    page = document.AddPage();
                    gfx = XGraphics.FromPdfPage(page);
                    yPoint = 40;
                }
                
                gfx.DrawLine(XPens.Black, margin, yPoint, pageWidth - margin, yPoint);
                yPoint += 10;
                gfx.DrawString($"GENEL TOPLAM: {grandTotal:C2}", fontTitle, XBrushes.Black, new XRect(0, yPoint, pageWidth - margin, 30), XStringFormats.TopRight);

                document.Save(stream, false);
                return File(stream.ToArray(), "application/pdf", $"Ekstre_{DateTime.Now:yyyyMMdd}.pdf");
            }
        }

        public async Task<IActionResult> ExportExcel(string filter = "month", DateTime? startDate = null, DateTime? endDate = null, string? filteredCompanyId = null)
        {
            var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userIdStr)) return RedirectToAction("Login", "Account");

            int userId = int.Parse(userIdStr);
            var kullanici = _mskDb.TBL_KULLANICIs.FirstOrDefault(i => i.LNGIDENTITYKOD == userId);
            if (kullanici == null) return RedirectToAction("Login", "Account");

            int firmaKod = kullanici.LNGORTAKFIRMAKOD ?? 2;
            var projectClaim = User.FindFirst("ProjectCode");
            if (projectClaim != null && int.TryParse(projectClaim.Value, out int selectedProject))
            {
                firmaKod = selectedProject;
            }
            string firmaAdi = kullanici.TXTFIRMAADI ?? "";

            var orders = await GetFilteredOrdersAsync(firmaKod, filter, startDate, endDate, filteredCompanyId);

            var excelBytes = GenerateExcelBytes(orders, firmaAdi);
            var fileName = $"Finans_Ekstre_{DateTime.Now:yyyyMMdd_HHmm}.xlsx";

            return File(excelBytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", fileName);
        }

        public async Task<IActionResult> SendReconciliation(string filter = "month", DateTime? startDate = null, DateTime? endDate = null, string? filteredCompanyId = null)
        {
            var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userIdStr)) return Json(new { success = false, message = "Oturum bulunamadı." });

            int userId = int.Parse(userIdStr);
            var kullanici = _mskDb.TBL_KULLANICIs.FirstOrDefault(i => i.LNGIDENTITYKOD == userId);
            if (kullanici == null) return Json(new { success = false, message = "Kullanıcı bulunamadı." });
            
            if (string.IsNullOrEmpty(kullanici.TXTEMAIL))
                return Json(new { success = false, message = "Sistemde kayıtlı e-posta adresiniz bulunmuyor. Lütfen profil ayarlarına gidiniz." });

            // Override with Project Selection
            int firmaKod = kullanici.LNGORTAKFIRMAKOD ?? 2;
            var projectClaim = User.FindFirst("ProjectCode");
            if (projectClaim != null && int.TryParse(projectClaim.Value, out int selectedProject))
            {
                firmaKod = selectedProject;
            }
            string firmaAdi = kullanici.TXTFIRMAADI ?? "";

            try 
            {
                var orders = await GetFilteredOrdersAsync(firmaKod, filter, startDate, endDate, filteredCompanyId);
                var excelBytes = GenerateExcelBytes(orders, firmaAdi);
                
                string subject = $"{firmaAdi} Mutabakat Formu";
                string message = $@"
                    <h3>Sayın {kullanici.TXTADSOYAD},</h3>
                    <p>Talep ettiğiniz finansal mutabakat formu ektedir.</p>
                    <p>İyi çalışmalar dileriz.</p>
                ";

                await _emailService.SendEmailAsync(kullanici.TXTEMAIL, subject, message, excelBytes, $"Mutabakat_{DateTime.Now:yyyyMMdd}.xlsx");

                return Json(new { success = true, message = $"Mutabakat formu başarıyla {kullanici.TXTEMAIL} adresine gönderildi." });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Gönderim sırasında hata oluştu: " + ex.Message });
            }
        }

        private byte[] GenerateExcelBytes(List<UniCP.Models.MsK.SpModels.SpVarunaSiparisResult> orders, string firmaAdi)
        {
            using (var workbook = new ClosedXML.Excel.XLWorkbook())
            {
                var worksheet = workbook.Worksheets.Add("Ekstre");

                // Headers
                var headers = new[] { "Firma", "Sipariş No", "Tarih", "Müşteri", "Durum", "Tutar", "Bekleyen Bakiye" };
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
                foreach (var item in orders)
                {
                    worksheet.Cell(row, 1).Value = firmaAdi;
                    worksheet.Cell(row, 2).Value = item.SerialNumber ?? "";
                    worksheet.Cell(row, 3).Value = item.CreateOrderDate?.ToString("dd.MM.yyyy") ?? "";
                    worksheet.Cell(row, 4).Value = item.AccountTitle ?? "";
                    worksheet.Cell(row, 5).Value = item.OrderStatus ?? "";
                    worksheet.Cell(row, 6).Value = item.TotalAmountWithTax ?? 0;
                    worksheet.Cell(row, 7).Value = item.Bekleyen_Bakiye ?? 0;

                    // Formatting
                    worksheet.Cell(row, 6).Style.NumberFormat.Format = "#,##0.00 ₺";
                    worksheet.Cell(row, 7).Style.NumberFormat.Format = "#,##0.00 ₺";
                    
                    row++;
                }

                worksheet.Columns().AdjustToContents();

                using (var stream = new MemoryStream())
                {
                    workbook.SaveAs(stream);
                    return stream.ToArray();
                }
            }
        }

        private async Task<List<UniCP.Models.MsK.SpModels.SpVarunaSiparisResult>> GetFilteredOrdersAsync(int firmaKod, string filter, DateTime? startDate, DateTime? endDate, string? filteredCompanyId = null)
        {
            int? decryptedCompanyId = _urlEncryption.DecryptId(filteredCompanyId);
            var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
            int userId = userIdStr != null ? int.Parse(userIdStr) : 0;
            
            // Determine effective target companies
            List<int> targetCompanyIds = new List<int>();
            
            // Check UserType and Project Selection
            // We need to fetch user type here to be sure, using AsNoTracking for performance
            var kullanici = await _mskDb.TBL_KULLANICIs.AsNoTracking().FirstOrDefaultAsync(i => i.LNGIDENTITYKOD == userId);
            var projectClaim = User.FindFirst("ProjectCode");
            
            // Explicit Selection Logic:
            // 1. If explicit 'filteredCompanyId' is passed from UI -> Use it (Highest Priority)
            // 2. If 'ProjectCode' claim exists (Locked context) -> Use it
            bool isExplicitlyFiltered = decryptedCompanyId.HasValue;
            bool isProjectLocked = projectClaim != null;

            if (kullanici != null && kullanici.LNGKULLANICITIPI == 3 && !isExplicitlyFiltered && !isProjectLocked)
            {
                 // Fetch all authorized firms (AGGREGATE MODE)
                 var authorizedIndices = await _mskDb.TBL_KULLANICI_FIRMAs
                                     .AsNoTracking()
                                     .Where(f => f.LNGKULLANICIKOD == kullanici.LNGKOD)
                                     .Select(f => f.LNGFIRMAKOD)
                                     .Distinct()
                                     .ToListAsync();
                                     
                 if (authorizedIndices.Any())
                 {
                     targetCompanyIds.AddRange(authorizedIndices);
                 }
                 else
                 {
                     targetCompanyIds.Add(firmaKod);
                 }
            }
            else
            {
                // SINGLE COMPANY MODE
                // If filteredCompanyId is present, create using that.
                // Else use the firmaKod (which defaults to user's firm or project claim)
                if (decryptedCompanyId.HasValue)
                {
                    targetCompanyIds.Add(decryptedCompanyId.Value);
                }
                else
                {
                    targetCompanyIds.Add(firmaKod);
                }
            }

            // Parallel Data Fetching
            var resultBag = new System.Collections.Concurrent.ConcurrentBag<UniCP.Models.MsK.SpModels.SpVarunaSiparisResult>();
            var parallelOptions = new ParallelOptions { MaxDegreeOfParallelism = 10 };
            
            await Parallel.ForEachAsync(targetCompanyIds, parallelOptions, async (cid, token) => 
            {
                using (var scope = _scopeFactory.CreateScope())
                {
                    var scopedDb = scope.ServiceProvider.GetRequiredService<MskDbContext>();
                    try 
                    {
                        var orders = await scopedDb.SP_VARUNA_SIPARISAsync(cid);
                        if (orders != null)
                        {
                            foreach(var o in orders) resultBag.Add(o);
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"[Export] Error fetching for company {cid}: {ex.Message}");
                    }
                }
            });

            var varunaSiparisler = resultBag.ToList();
            var now = DateTime.Now;

            // In-Memory Filtering
            IEnumerable<SpVarunaSiparisResult> query = varunaSiparisler;

            if (startDate.HasValue && endDate.HasValue)
            {
                 var end = endDate.Value.Date.AddDays(1).AddTicks(-1);
                 query = query.Where(o => o.CreateOrderDate >= startDate.Value && o.CreateOrderDate <= end);
            }
            else
            {
                switch (filter.ToLower())
                {
                    case "3months":
                        var threeMonthsAgo = now.AddMonths(-3);
                        query = query.Where(o => o.CreateOrderDate >= threeMonthsAgo);
                        break;
                    case "year":
                        var oneYearAgo = now.AddYears(-1);
                        query = query.Where(o => o.CreateOrderDate >= oneYearAgo);
                        break;
                    case "month":
                    default:
                        var oneMonthAgo = now.AddMonths(-1);
                        query = query.Where(o => o.CreateOrderDate >= oneMonthAgo);
                        break;
                }
            }
            
            return query.OrderByDescending(x => x.CreateOrderDate).ToList();
        }

        private void CalculateComparisonStats(int firmaKod, string filter, DateTime? startDate, DateTime? endDate, decimal currentTotal)
        {
             var now = DateTime.Now;
             decimal previousTotal = 0;
             string comparisonLabel = "";

            if (startDate.HasValue && endDate.HasValue)
            {
                 var end = endDate.Value.Date.AddDays(1).AddTicks(-1);
                 var duration = end - startDate.Value;
                 var prevStart = startDate.Value.Subtract(duration);
                 var prevEnd = startDate.Value.AddTicks(-1);
                 
                 var prevPeriodOrders = _mskDb.SP_VARUNA_SIPARIS(firmaKod).AsQueryable()
                    .Where(o => o.CreateOrderDate >= prevStart && o.CreateOrderDate <= prevEnd)
                    .ToList();
                 
                 previousTotal = prevPeriodOrders.Sum(x => x.TotalAmountWithTax ?? 0);
                 comparisonLabel = "önceki döneme göre";
            }
            else
            {
                var allOrders = _mskDb.SP_VARUNA_SIPARIS(firmaKod).AsQueryable();

                switch (filter.ToLower())
                {
                    case "3months":
                        var threeMonthsAgo = now.AddMonths(-3);
                        var prev3MonthsStart = threeMonthsAgo.AddMonths(-3);
                        var prev3MonthsEnd = threeMonthsAgo.AddTicks(-1);
                        previousTotal = allOrders
                            .Where(o => o.CreateOrderDate >= prev3MonthsStart && o.CreateOrderDate <= prev3MonthsEnd)
                            .Sum(x => x.TotalAmountWithTax ?? 0);
                        comparisonLabel = "geçen 3 aya göre";
                        break;

                    case "year":
                        var oneYearAgo = now.AddYears(-1);
                        var prevYearStart = oneYearAgo.AddYears(-1);
                        var prevYearEnd = oneYearAgo.AddTicks(-1);
                        previousTotal = allOrders
                            .Where(o => o.CreateOrderDate >= prevYearStart && o.CreateOrderDate <= prevYearEnd)
                            .Sum(x => x.TotalAmountWithTax ?? 0);
                        comparisonLabel = "geçen yıla göre";
                        break;

                    case "month":
                    default:
                        var oneMonthAgo = now.AddMonths(-1);
                        var prevMonthStart = oneMonthAgo.AddMonths(-1);
                        var prevMonthEnd = oneMonthAgo.AddTicks(-1);
                        previousTotal = allOrders
                            .Where(o => o.CreateOrderDate >= prevMonthStart && o.CreateOrderDate <= prevMonthEnd)
                            .Sum(x => x.TotalAmountWithTax ?? 0);
                        comparisonLabel = "geçen aya göre";
                        break;
                }
            }

            ViewBag.PreviousTotal = previousTotal;
            ViewBag.ComparisonLabel = comparisonLabel;

            double percentageChange = 0;
            if (previousTotal != 0)
            {
                percentageChange = (double)((currentTotal - previousTotal) / previousTotal) * 100;
            }
            else if (currentTotal > 0)
            {
                percentageChange = 100;
            }
            
            ViewBag.PercentageChange = percentageChange;
        }




        [HttpGet]
        public IActionResult GetOrderDetails(string orderId)
        {
            if (string.IsNullOrEmpty(orderId))
                return Json(new { success = false, message = "Sipariş ID eksik" });

            try
            {
                var siparisDetay = _mskDb.SP_VARUNA_SIPARIS_DETAY(orderId).ToList();
                return Json(new { success = true, details = siparisDetay });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Veri hatası: " + ex.Message });
            }
        }
        [HttpPost]
        public IActionResult UpdatePO(string orderId, string poNumber)
        {
            try
            {
                Console.WriteLine($"[UpdatePO] Received OrderId: '{orderId}', PO: '{poNumber}'");

                if (string.IsNullOrEmpty(orderId)) return Json(new { success = false, message = "Sipariş No boş olamaz." });
                orderId = orderId.Trim();
                Console.WriteLine($"[UpdatePO] Trimmed OrderId: '{orderId}'");

                var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
                int? userId = !string.IsNullOrEmpty(userIdStr) ? int.Parse(userIdStr) : null;

                // Validate User ID exists to prevent FK Constraint Error
                if (userId.HasValue)
                {
                    bool userExists = _mskDb.TBL_KULLANICIs.Any(u => u.LNGKOD == userId.Value);
                    if (!userExists)
                    {
                         // Provide fallback or just nullify
                         Console.WriteLine($"[UpdatePO] User ID {userId} not found in TBL_KULLANICI. Setting CreatedBy to null.");
                         userId = null;
                    }
                }

                var existing = _mskDb.TBL_FINANS_ONAYs.FirstOrDefault(x => x.OrderId == orderId);
                Console.WriteLine($"[UpdatePO] Existing found: {existing != null}");
                if (existing != null)
                {
                    existing.PONumber = poNumber;
                    existing.CreatedDate = DateTime.Now;
                    existing.CreatedBy = userId;
                }
                else
                {
                    var approval = new TBL_FINANS_ONAY
                    {
                        OrderId = orderId,
                        PONumber = poNumber,
                        CreatedDate = DateTime.Now,
                        CreatedBy = userId
                    };
                    _mskDb.TBL_FINANS_ONAYs.Add(approval);
                }

                _mskDb.SaveChanges();
                
                return Json(new { success = true, message = "Sipariş onaylandı ve PO numarası kaydedildi." });
            }
            catch (Exception ex)
            {
                var innerMsg = ex.InnerException != null ? ex.InnerException.Message : "";
                Console.WriteLine($"[UpdatePO] Error: {ex.Message} Inner: {innerMsg}");
                return Json(new { success = false, message = "Db Hatası: " + ex.Message + " " + innerMsg });
            }
        }
        [HttpPost]
        public IActionResult RevokePO(string orderId)
        {
            try
            {
                if (string.IsNullOrEmpty(orderId)) return Json(new { success = false, message = "Sipariş No boş olamaz." });
                orderId = orderId.Trim();

                var existing = _mskDb.TBL_FINANS_ONAYs.FirstOrDefault(x => x.OrderId == orderId && !x.IsRevoked);
                if (existing != null)
                {
                    // Soft Delete
                    existing.IsRevoked = true;
                    existing.RevokedDate = DateTime.Now;

                    var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
                    int? userId = !string.IsNullOrEmpty(userIdStr) ? int.Parse(userIdStr) : null;
                    
                    // Validate User ID for FK
                    if (userId.HasValue)
                    {
                        bool userExists = _mskDb.TBL_KULLANICIs.Any(u => u.LNGKOD == userId.Value);
                        if (!userExists) userId = null;
                    }
                    existing.RevokedBy = userId;

                    _mskDb.SaveChanges();
                    return Json(new { success = true, message = "Onay başarıyla geri alındı." });
                }

                return Json(new { success = false, message = "Aktif onay kaydı bulunamadı." });
            }
            catch (Exception ex)
            {
                var inner = ex.InnerException != null ? ex.InnerException.Message : "";
                return Json(new { success = false, message = "Hata oluştu: " + ex.Message + " " + inner });
            }
        }

        [HttpGet]
        public async Task<IActionResult> Credits()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return RedirectToAction("Login", "Account");
            
            ViewBag.CurrentBalance = user.TokenBalance;
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> PurchaseTokens(int amount)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Json(new { success = false, message = "Kullanıcı bulunamadı." });

            // Demo Logic: Directly add tokens. In real app, integrate payment gateway here.
            user.TokenBalance += amount;
            await _userManager.UpdateAsync(user);

            return Json(new { success = true, newBalance = user.TokenBalance, message = $"{amount} Token hesabınıza yüklendi." });
        }

        [HttpGet]
        public IActionResult DebugApprovals()
        {
            var approvals = _mskDb.TBL_FINANS_ONAYs.ToList();
            return Json(approvals);
        }
    }
}
