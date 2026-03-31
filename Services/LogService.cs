using System;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using SOS.DbData;
using SOS.Models.MsK;

namespace SOS.Services
{
    public interface ILogService
    {
        Task LogAsync(string action, string details, string module = null);
    }

    public class DbLogService : ILogService
    {
        private readonly MskDbContext _context;
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly ILogger<DbLogService> _logger;

        public DbLogService(MskDbContext context, IHttpContextAccessor httpContextAccessor, ILogger<DbLogService> logger)
        {
            _context = context;
            _httpContextAccessor = httpContextAccessor;
            _logger = logger;
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

                // Aktif tenant: FirmaKod claim'inden okunur (login sırasında CustomUserClaimsPrincipalFactory tarafından set edilir)
                int? tenantId = null;
                var firmaKodStr = user?.FindFirstValue("FirmaKod");
                if (int.TryParse(firmaKodStr, out int fk)) tenantId = fk;

                string ipAddress = _httpContextAccessor.HttpContext?.Connection?.RemoteIpAddress?.ToString() ?? "Unknown";

                var log = new TBL_SISTEM_LOG
                {
                    TXTISLEM = action,
                    TXTDETAY = details,
                    TXTMODUL = module ?? "General",
                    TRHKAYIT = DateTime.Now,
                    TXTKULLANICIADI = userName ?? "Anonymous",
                    LNGKULLANICIKOD = userId,
                    TXTIP = ipAddress,
                    LNGORTAKFIRMAKOD = tenantId
                };

                _context.TBL_SISTEM_LOGs.Add(log);
                await _context.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                // Fallback: Don't crash the app if logging fails
                _logger.LogError(ex, "Logging failed");
            }
        }
    }
}

