using Microsoft.EntityFrameworkCore;
using UniCP.DbData;
using UniCP.Models.Company;
using UniCP.Models.MsK;
using UniCP.Models.Enums;

namespace UniCP.Services
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
                .FirstOrDefaultAsync(k => k.LNGKOD == userId);

            if (kullanici == null)
            {
                result.DebugFlow.Add("User not found");
                return result;
            }

            result.DebugFlow.Add($"User Type: {kullanici.LNGKULLANICITIPI}");

            // 2. Determine authorized companies based on user type
            // Read cookie for persistent filtering
            int? cookieCompanyId = null;
            var cookieVal = httpContext.Request.Cookies[UniCP.Constants.AppConstants.Cookies.SelectedCompanyId];
            if (!string.IsNullOrEmpty(cookieVal) && int.TryParse(cookieVal, out int parsedCookie) && parsedCookie > 0)
            {
                cookieCompanyId = parsedCookie;
            }

            if (kullanici.LNGKULLANICITIPI == (int)UserType.Admin || 
                kullanici.LNGKULLANICITIPI == (int)UserType.UniveraInternal)
            {
                result.DebugFlow.Add("Admin/Internal user - all companies");
                
                // Admins/Internals can see all companies
                var allProjects = await _mskDb.VIEW_ORTAK_PROJE_ISIMLERIs
                    .AsNoTracking()
                    .ToListAsync();
                
                result.AuthorizedCompanies = allProjects;
                var allIds = allProjects.Select(p => p.LNGKOD).ToList();
                
                // Priority: explicit filter param > cookie > all
                if (filteredCompanyId.HasValue && filteredCompanyId.Value > 0)
                {
                    result.TargetCompanyIds = new List<int> { filteredCompanyId.Value };
                    result.DebugFlow.Add($"Filtered to company (param): {filteredCompanyId.Value}");
                }
                else if (filteredCompanyId.HasValue && filteredCompanyId.Value == -1)
                {
                    // Explicit reset to all
                    result.TargetCompanyIds = allIds;
                    result.DebugFlow.Add("Reset to all companies (param -1)");
                }
                else if (cookieCompanyId.HasValue && allIds.Contains(cookieCompanyId.Value))
                {
                    // Cookie-based persistent filter
                    result.TargetCompanyIds = new List<int> { cookieCompanyId.Value };
                    result.DebugFlow.Add($"Filtered to company (cookie): {cookieCompanyId.Value}");
                }
                else
                {
                    result.TargetCompanyIds = allIds;
                    result.DebugFlow.Add("Viewing all companies");
                }
            }
            else if (kullanici.LNGKULLANICITIPI == (int)UserType.UniveraCustomer)
            {
                result.DebugFlow.Add("Univera Customer - authorized companies only");
                
                // Get authorized companies from TBL_KULLANICI_FIRMA
                var authorizedProjectIds = await _mskDb.TBL_KULLANICI_FIRMAs
                    .Where(kp => kp.LNGKULLANICIKOD == userId)
                    .Select(kp => kp.LNGFIRMAKOD)
                    .ToListAsync();

                result.DebugFlow.Add($"Authorized project count: {authorizedProjectIds.Count}");

                if (authorizedProjectIds.Any())
                {
                    var authorizedProjects = await _mskDb.VIEW_ORTAK_PROJE_ISIMLERIs
                        .Where(p => authorizedProjectIds.Contains(p.LNGKOD))
                        .AsNoTracking()
                        .ToListAsync();

                    result.AuthorizedCompanies = authorizedProjects;
                    
                    // Priority: explicit filter param > cookie > all authorized
                    if (filteredCompanyId.HasValue && filteredCompanyId.Value > 0 && authorizedProjectIds.Contains(filteredCompanyId.Value))
                    {
                        result.TargetCompanyIds = new List<int> { filteredCompanyId.Value };
                        result.DebugFlow.Add($"Univera Customer filtered to (param): {filteredCompanyId.Value}");
                    }
                    else if (filteredCompanyId.HasValue && filteredCompanyId.Value == -1)
                    {
                        // Explicit reset to all authorized
                        result.TargetCompanyIds = authorizedProjectIds;
                        result.DebugFlow.Add("Univera Customer reset to all authorized (param -1)");
                    }
                    else if (cookieCompanyId.HasValue && authorizedProjectIds.Contains(cookieCompanyId.Value))
                    {
                        // Cookie-based persistent filter
                        result.TargetCompanyIds = new List<int> { cookieCompanyId.Value };
                        result.DebugFlow.Add($"Univera Customer filtered to (cookie): {cookieCompanyId.Value}");
                    }
                    else
                    {
                        result.TargetCompanyIds = authorizedProjectIds;
                        result.DebugFlow.Add("Univera Customer viewing all authorized");
                    }
                }
            }
            else // Regular customer (Type 2 - RegularCustomer)
            {
                result.DebugFlow.Add("Regular customer - single company");
                
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
                result.TargetCompanyIds,
                result.DebugFlow);

            // 4. Build company names string
            if (result.AuthorizedCompanies.Any())
            {
                result.AuthorizedCompanyNames = string.Join(", ", 
                    result.AuthorizedCompanies.Select(c => c.TXTORTAKPROJEADI));
            }

            result.DebugFlow.Add($"Selected Company: {result.SelectedCompanyId}");
            result.DebugFlow.Add($"Target Companies: {string.Join(",", result.TargetCompanyIds)}");

            return result;
        }

        private int? ResolveSelectedCompany(
            int? filteredCompanyId, 
            HttpContext httpContext, 
            List<int> authorizedCompanyIds,
            List<string> debugFlow)
        {
            // Priority 1: Query parameter
            if (filteredCompanyId.HasValue && authorizedCompanyIds.Contains(filteredCompanyId.Value))
            {
                debugFlow.Add($"Selected from query param: {filteredCompanyId}");
                return filteredCompanyId.Value;
            }

            // Priority 2: Cookie
            var cookieVal = httpContext.Request.Cookies[UniCP.Constants.AppConstants.Cookies.SelectedCompanyId];
            if (!string.IsNullOrEmpty(cookieVal) && 
                int.TryParse(cookieVal, out int cookiePid) && 
                authorizedCompanyIds.Contains(cookiePid))
            {
                debugFlow.Add($"Selected from cookie: {cookiePid}");
                return cookiePid;
            }

            // Priority 3: Only default to a single company if there IS only one
            // If multiple companies are authorized and no explicit filter, return null
            // This prevents the sidebar from passing a specific company as filter
            if (authorizedCompanyIds.Count == 1)
            {
                var defaultCompany = authorizedCompanyIds.First();
                debugFlow.Add($"Selected default (single company): {defaultCompany}");
                return defaultCompany;
            }

            debugFlow.Add("No company selected (multiple companies, no filter)");
            return null;
        }

        public void SetCompanyCookie(HttpContext httpContext, int companyId)
        {
            var cookieOptions = new CookieOptions
            {
                Expires = DateTime.Now.AddDays(30),
                HttpOnly = false, // Accessible to JavaScript for client-side filtering
                Secure = false,   // Set to true in production with HTTPS
                SameSite = SameSiteMode.Lax,
                IsEssential = true
            };

            httpContext.Response.Cookies.Append(UniCP.Constants.AppConstants.Cookies.SelectedCompanyId, companyId.ToString(), cookieOptions);
            _logger.LogDebug("Set company cookie: {CompanyId}", companyId);
        }

        public void ClearCompanyCookie(HttpContext httpContext)
        {
            httpContext.Response.Cookies.Delete(UniCP.Constants.AppConstants.Cookies.SelectedCompanyId);
            _logger.LogDebug("Cleared company cookie");
        }
    }
}
