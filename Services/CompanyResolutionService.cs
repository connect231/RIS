using Microsoft.EntityFrameworkCore;
using SOS.DbData;
using SOS.Models.Company;
using SOS.Models.MsK;
using SOS.Models.Enums;

namespace SOS.Services
{
    public interface ICompanyResolutionService
    {
        /// <summary>
        /// Resolves authorized companies for a user based on their type and selected filter
        /// </summary>
        /// <param name="userId">User ID from claims</param>
        /// <param name="filteredCompanyId">Company ID from query parameter or form</param>
        /// <param name="httpContext">HTTP context for cookie access</param>
        /// <returns>Company resolution result with authorized companies and selected company</returns>
        Task<CompanyResolutionResult> ResolveCompaniesAsync(
            int userId, 
            int? filteredCompanyId, 
            HttpContext httpContext);

        /// <summary>
        /// Sets the selected company ID in a cookie
        /// </summary>
        void SetCompanyCookie(HttpContext httpContext, int companyId);

        /// <summary>
        /// Clears the selected company ID cookie
        /// </summary>
        void ClearCompanyCookie(HttpContext httpContext);
    }

    public class CompanyResolutionService : ICompanyResolutionService
    {
        private readonly MskDbContext _mskDb;
        private readonly ILogger<CompanyResolutionService> _logger;

        public CompanyResolutionService(MskDbContext mskDb, ILogger<CompanyResolutionService> logger)
        {
            _mskDb = mskDb;
            _logger = logger;
        }

        public async Task<CompanyResolutionResult> ResolveCompaniesAsync(
            int userId, 
            int? filteredCompanyId, 
            HttpContext httpContext)
        {
            var result = new CompanyResolutionResult();
            
            // 1. Get user information
            var kullanici = await _mskDb.TBL_KULLANICIs
                .AsNoTracking()
                .FirstOrDefaultAsync(k => k.LNGKOD == userId);

            if (kullanici == null)
                return result;

            // 2. Determine authorized companies based on user type
            // Read cookie for persistent filtering
            int? cookieCompanyId = null;
            var cookieVal = httpContext.Request.Cookies[SOS.Constants.AppConstants.Cookies.SelectedCompanyId];
            if (!string.IsNullOrEmpty(cookieVal) && int.TryParse(cookieVal, out int parsedCookie) && parsedCookie > 0)
            {
                cookieCompanyId = parsedCookie;
            }

            if (kullanici.LNGKULLANICITIPI == (int)UserType.Admin)
            {
                // Only Admins can see all companies
                var allProjects = await _mskDb.VIEW_ORTAK_PROJE_ISIMLERIs
                    .AsNoTracking()
                    .ToListAsync();

                result.AuthorizedCompanies = allProjects;
                var allIds = allProjects.Select(p => p.LNGKOD).ToList();

                // Priority: explicit filter param > cookie > all
                if (filteredCompanyId.HasValue && filteredCompanyId.Value > 0)
                    result.TargetCompanyIds = new List<int> { filteredCompanyId.Value };
                else if (filteredCompanyId.HasValue && filteredCompanyId.Value == -1)
                    result.TargetCompanyIds = allIds;
                else if (cookieCompanyId.HasValue && allIds.Contains(cookieCompanyId.Value))
                    result.TargetCompanyIds = new List<int> { cookieCompanyId.Value };
                else
                    result.TargetCompanyIds = allIds;
            }
            else if (kullanici.LNGKULLANICITIPI == (int)UserType.UniveraInternal ||
                     kullanici.LNGKULLANICITIPI == (int)UserType.UniveraCustomer)
            {
                // Both UniveraInternal (3) and UniveraCustomer (4) use TBL_KULLANICI_FIRMA
                var authorizedProjectIds = await _mskDb.TBL_KULLANICI_FIRMAs
                    .AsNoTracking()
                    .Where(kp => kp.LNGKULLANICIKOD == userId)
                    .Select(kp => kp.LNGFIRMAKOD)
                    .ToListAsync();

                if (authorizedProjectIds.Any())
                {
                    var authorizedProjects = await _mskDb.VIEW_ORTAK_PROJE_ISIMLERIs
                        .Where(p => authorizedProjectIds.Contains(p.LNGKOD))
                        .AsNoTracking()
                        .ToListAsync();

                    result.AuthorizedCompanies = authorizedProjects;

                    // Priority: explicit filter param > cookie > all authorized
                    if (filteredCompanyId.HasValue && filteredCompanyId.Value > 0 && authorizedProjectIds.Contains(filteredCompanyId.Value))
                        result.TargetCompanyIds = new List<int> { filteredCompanyId.Value };
                    else if (filteredCompanyId.HasValue && filteredCompanyId.Value == -1)
                        result.TargetCompanyIds = authorizedProjectIds;
                    else if (cookieCompanyId.HasValue && authorizedProjectIds.Contains(cookieCompanyId.Value))
                        result.TargetCompanyIds = new List<int> { cookieCompanyId.Value };
                    else
                        result.TargetCompanyIds = authorizedProjectIds;
                }
            }
            else // Regular customer (Type 2 - RegularCustomer)
            {
                
                // Regular customers only see their own company
                var userCompanyId = kullanici.LNGORTAKFIRMAKOD;
                
                if (userCompanyId.HasValue)
                {
                    var userCompany = await _mskDb.VIEW_ORTAK_PROJE_ISIMLERIs
                        .Where(p => p.LNGKOD == userCompanyId.Value)
                        .AsNoTracking()
                        .FirstOrDefaultAsync();

                    if (userCompany != null)
                    {
                        result.AuthorizedCompanies = new List<VIEW_ORTAK_PROJE_ISIMLERI> { userCompany };
                        result.TargetCompanyIds = new List<int> { userCompanyId.Value };
                    }
                }
            }

            // 3. Resolve selected company (priority: filter param > cookie > default)
            result.SelectedCompanyId = ResolveSelectedCompany(
                filteredCompanyId,
                httpContext,
                result.TargetCompanyIds);

            // 4. Build company names string
            if (result.AuthorizedCompanies.Any())
            {
                result.AuthorizedCompanyNames = string.Join(", ",
                    result.AuthorizedCompanies.Select(c => c.TXTORTAKPROJEADI));
            }

            return result;
        }

        private int? ResolveSelectedCompany(
            int? filteredCompanyId,
            HttpContext httpContext,
            List<int> authorizedCompanyIds)
        {
            // Priority 1: Query parameter
            if (filteredCompanyId.HasValue && authorizedCompanyIds.Contains(filteredCompanyId.Value))
                return filteredCompanyId.Value;

            // Priority 2: Cookie
            var cookieVal = httpContext.Request.Cookies[SOS.Constants.AppConstants.Cookies.SelectedCompanyId];
            if (!string.IsNullOrEmpty(cookieVal) &&
                int.TryParse(cookieVal, out int cookiePid) &&
                authorizedCompanyIds.Contains(cookiePid))
                return cookiePid;

            // Priority 3: Default to single company only when there is exactly one
            if (authorizedCompanyIds.Count == 1)
                return authorizedCompanyIds.First();

            return null;
        }

        public void SetCompanyCookie(HttpContext httpContext, int companyId)
        {
            var cookieOptions = new CookieOptions
            {
                Expires = DateTime.Now.AddDays(30),
                HttpOnly = true,                          // JavaScript erişimi engellendi (XSS koruması)
                Secure = httpContext.Request.IsHttps,     // HTTPS ortamında otomatik aktif, dev HTTP'de bozulmaz
                SameSite = SameSiteMode.Strict,           // CSRF koruması: başka domain'den istek gelirse cookie gönderilmez
                IsEssential = true
            };

            httpContext.Response.Cookies.Append(SOS.Constants.AppConstants.Cookies.SelectedCompanyId, companyId.ToString(), cookieOptions);
            _logger.LogDebug("Set company cookie: {CompanyId}", companyId);
        }

        public void ClearCompanyCookie(HttpContext httpContext)
        {
            httpContext.Response.Cookies.Delete(SOS.Constants.AppConstants.Cookies.SelectedCompanyId);
            _logger.LogDebug("Cleared company cookie");
        }
    }
}


