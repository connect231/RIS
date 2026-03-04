using UniCP.Models.MsK;

namespace UniCP.Models.Company
{
    /// <summary>
    /// Result of company resolution containing authorized companies and selected company
    /// </summary>
    public class CompanyResolutionResult
    {
        /// <summary>
        /// List of company IDs the user is authorized to access
        /// </summary>
        public List<int> TargetCompanyIds { get; set; } = new();
        
        /// <summary>
        /// Full company information for authorized companies
        /// </summary>
        public List<VIEW_ORTAK_PROJE_ISIMLERI> AuthorizedCompanies { get; set; } = new();
        
        /// <summary>
        /// Currently selected company ID (from filter, cookie, or default)
        /// </summary>
        public int? SelectedCompanyId { get; set; }
        
        /// <summary>
        /// Comma-separated list of authorized company names for display
        /// </summary>
        public string AuthorizedCompanyNames { get; set; } = "";
        
        /// <summary>
        /// Debug information about the resolution process
        /// </summary>
        public List<string> DebugFlow { get; set; } = new();
    }
}
