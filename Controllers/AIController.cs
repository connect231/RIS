using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using UniCP.DbData;
using UniCP.Models.MsK;
using Microsoft.AspNetCore.Authorization;
using Ganss.Xss;

namespace UniCP.Controllers
{
    [Authorize(Roles = "AI,Admin")]
    public class AIController : Controller
    {
        private readonly UniCP.Services.AI.AIService _aiService;
        private readonly MskDbContext _mskDb;

        public AIController(UniCP.Services.AI.AIService aiService, MskDbContext mskDb)
        {
            _aiService = aiService;
            _mskDb = mskDb;
        }

        [HttpPost]
        public async Task<IActionResult> Chat([FromBody] ChatRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.Message))
            {
                return Json(new { text = "Lütfen bir mesaj yazın." });
            }

            // Sanitize User Input (XSS Prevention)
            var sanitizer = new Ganss.Xss.HtmlSanitizer();
            request.Message = sanitizer.Sanitize(request.Message);

            // Double check after sanitization
            if (string.IsNullOrWhiteSpace(request.Message))
            {
                 return Json(new { text = "Lütfen geçerli bir mesaj yazın." });
            }

            // 1. Get User Info & Financial Data
            var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
            int firmaKod = 2; // Default
            string kullaniciAdi = "Misafir";
            string userEmail = "";
            int userIdInt = 0;
            TBL_KULLANICI? kullanici = null;

            if (!string.IsNullOrEmpty(userIdStr) && int.TryParse(userIdStr, out int userId))
            {
                userIdInt = userId;
                kullanici = _mskDb.TBL_KULLANICIs.FirstOrDefault(i => i.LNGIDENTITYKOD == userId);
                if (kullanici != null)
                {
                    firmaKod = kullanici.LNGORTAKFIRMAKOD ?? 2;
                    var projectClaim = User.FindFirst("ProjectCode");
                    if (projectClaim != null && int.TryParse(projectClaim.Value, out int selectedProject))
                    {
                        firmaKod = selectedProject;
                    }
                    kullaniciAdi = kullanici.TXTADSOYAD ?? "Kullanıcı";
                    userEmail = kullanici.TXTEMAIL;
                }
            }
            
            // If user is not logged in / mapped, block or handle appropriately
            if (userIdInt == 0)
            {
                 return Json(new { text = "AI servisini kullanmak için giriş yapmalısınız." });
            }


            // Fetch Orders (Using the Stored Procedure mapped in DbContext)
            // We fetch all for the company and filter in memory for the context summary
            var allOrders = _mskDb.SP_VARUNA_SIPARIS(firmaKod).ToList();
            
            var now = DateTime.Now;
            var thirtyDaysAgo = now.AddMonths(-1);
            var oneYearAgo = now.AddYears(-1);

            var monthOrders = allOrders.Where(x => x.CreateOrderDate.HasValue && x.CreateOrderDate.Value >= thirtyDaysAgo).ToList();
            var yearOrders = allOrders.Where(x => x.CreateOrderDate.HasValue && x.CreateOrderDate.Value >= oneYearAgo).ToList();

            decimal monthTotal = monthOrders.Sum(x => x.TotalAmountWithTax ?? 0);
            decimal yearTotal = yearOrders.Sum(x => x.TotalAmountWithTax ?? 0);

            // --- 1. Support Tickets Context ---
            string ticketSummary = "Destek Talebi Bilgisi Bulunamadı.";
            try
            {
                // Assuming '1' means 'Open' or fetching all statuses to count open ones.
                // Or using the Status Count SP directly.
                // Let's use the Status Count SP as it's lighter
                if (!string.IsNullOrEmpty(userEmail))
                {
                    var ticketCounts = _mskDb.SP_N4B_TICKET_DURUM_SAYILARI(firmaKod, userEmail, DateTime.Now).ToList();
                    var openTickets = ticketCounts.Where(x => x.Durum != "Kapatıldı" && x.Durum != "Çözüldü" && x.Durum != "İptal Edildi").Sum(x => x.Sayi);
                    ticketSummary = $"{openTickets} adet açık destek talebiniz bulunuyor.";
                    
                    if (openTickets > 0)
                    {
                        var statusBreakdown = string.Join(", ", ticketCounts.Where(x => x.Sayi > 0).Select(x => $"{x.Sayi} {x.Durum}"));
                        ticketSummary += $" ({statusBreakdown})";
                    }
                }
            }
            catch { /* Ignore errors for context */ }

            // --- 2. Recent Orders Context ---
            var recentOrders = allOrders
                .Where(x => x.CreateOrderDate.HasValue)
                .OrderByDescending(x => x.CreateOrderDate)
                .Take(5)
                .ToList();

            var recentOrdersText = new System.Text.StringBuilder();
            if (recentOrders.Any())
            {
                foreach (var order in recentOrders)
                {
                    recentOrdersText.AppendLine($"- Tarih: {order.CreateOrderDate:dd.MM.yyyy}, Tutar: {order.TotalAmountWithTax:N2} TL (Sipariş No: {order.OrderId})");
                }
            }
            else
            {
                recentOrdersText.AppendLine("- Son dönemde sipariş bulunamadı.");
            }


            // 2. Build Context Data
            var contextData = $@"
            User Name: {kullaniciAdi}
            Customer ID: {firmaKod}
            Date: {now:yyyy-MM-dd}
            
            Financial Summary:
            - Son 30 Günlük Toplam Sipariş: {monthTotal:N2} TL ({monthOrders.Count} adet)
            - Son 365 Günlük Toplam Sipariş: {yearTotal:N2} TL ({yearOrders.Count} adet)

            Support Tickets Status:
            - {ticketSummary}

            Last 5 Orders:
            {recentOrdersText}
            ";

            // 3. Generate Response with Context via AIService
            var response = await _aiService.ProcessRequestAsync(userIdInt, request.Message, contextData);
            
            // Output sanitization (Defense in Depth)
            response.Text = sanitizer.Sanitize(response.Text);
            
            return Json(new { text = response.Text, action = response.Action, payload = response.Payload });
        }

        public class ChatRequest
        {
            public string Message { get; set; } = string.Empty;
        }
    }
}
