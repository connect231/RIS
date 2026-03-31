using System;
using Microsoft.AspNetCore.Http;
using System.Security.Claims;
using SOS.Models.MsK;

namespace SOS.Services
{
    public interface ITenantProvider
    {
        int? TenantId { get; }
        bool IsAdmin { get; }
    }

    public class TenantProvider : ITenantProvider
    {
        private readonly IHttpContextAccessor _httpContextAccessor;

        public TenantProvider(IHttpContextAccessor httpContextAccessor)
        {
            _httpContextAccessor = httpContextAccessor;
        }

        public int? TenantId
        {
            get
            {
                var user = _httpContextAccessor.HttpContext?.User;
                if (user == null || !user.Identity.IsAuthenticated) return null;

                // Priority 1: Check for explicit "FirmaKod" claim (safest)
                var firmaClaim = user.FindFirst("FirmaKod"); // Assuming we add this claim during login
                if (firmaClaim != null && int.TryParse(firmaClaim.Value, out int firmaKod))
                {
                    return firmaKod;
                }

                // Fallback (TEMPORARY): Check User DB via service (Avoid DB call here if possible)
                // Better approach for now: rely on existing claims if possible.
                // If the user hasn't logged in with new claim logic yet, this might return null, 
                // effectively blocking access (Fail Safe).
                
                return null; 
            }
        }

        public bool IsAdmin
        {
            get
            {
                var user = _httpContextAccessor.HttpContext?.User;
                if (user == null) return false;
                return user.IsInRole("Admin") || user.IsInRole("UniveraInternal");
            }
        }
    }
}

