using System;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using UniCP.DbData;
using UniCP.Models.MsK;

namespace UniCP.Services
{
    public interface ILogService
    {
        Task LogAsync(string action, string details, string module = null);
    }

    public class DbLogService : ILogService
    {
        private readonly MskDbContext _context;
        private readonly IHttpContextAccessor _httpContextAccessor;

        public DbLogService(MskDbContext context, IHttpContextAccessor httpContextAccessor)
        {
            _context = context;
            _httpContextAccessor = httpContextAccessor;
        }

        public async Task LogAsync(string action, string details, string module = null)
        {
            try
            {
                var user = _httpContextAccessor.HttpContext?.User;
                string? userName = user?.Identity?.Name;
                string? userIdStr = user?.FindFirstValue(ClaimTypes.NameIdentifier);
                int? userId = null;
                if (int.TryParse(userIdStr, out int uid)) userId = uid;

                string ipAddress = _httpContextAccessor.HttpContext?.Connection?.RemoteIpAddress?.ToString() ?? "Unknown";

                var log = new TBL_SISTEM_LOG
                {
                    TXTISLEM = action,
                    TXTDETAY = details,
                    TXTMODUL = module ?? "General",
                    TRHKAYIT = DateTime.Now,
                    TXTKULLANICIADI = userName ?? "Anonymous",
                    LNGKULLANICIKOD = userId,
                    TXTIP = ipAddress
                };

                _context.TBL_SISTEM_LOGs.Add(log);
                await _context.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                // Fallback: Don't crash the app if logging fails
                System.Console.WriteLine($"Logging Failed: {ex.Message}");
            }
        }
    }
}
