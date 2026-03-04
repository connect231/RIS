using UniCP.Models.MsK;
using UniCP.Models.MsK.SpModels;

namespace UniCP.Models.ViewModels
{
    public class UniveraHomeViewModel
    {
        // Company & Filter Info
        public List<VIEW_ORTAK_PROJE_ISIMLERI> AuthorizedCompanies { get; set; } = new();
        public string AuthorizedCompanyNames { get; set; }
        public int? SelectedCompanyId { get; set; }
        
        // Debug Info
        public List<string> DebugFlow { get; set; } = new();

        // User Info
        public TBL_KULLANICI Kullanici { get; set; }

        // KPI Counts & Metrics
        public int OpenTicketsCount { get; set; }
        public int EscalatedCount { get; set; }
        public int OpenDevRequestsCount { get; set; }
        public decimal PendingBudgetEffort { get; set; }
        public decimal PendingBudgetCost { get; set; }
        public int FinancePendingCount { get; set; }
        public int ExpiredLicenseCount { get; set; }
        public int UatTestCount { get; set; }
        
        public int MonthlyVolumeCount { get; set; }
        
        public object BudgetApprovalStats { get; set; }
        public object SupportRequestStats { get; set; }
        public object FinanceChartData { get; set; }

        // Charts Data
        public List<SSP_N4B_SLA_ORAN> SlaHistory { get; set; } = new();

        // Dashboard Data
        public UniveraDashboardData DashboardData { get; set; } = new();
    }

    public class UniveraDashboardData
    {
        public List<UniCP.Models.MsK.SpModels.SSP_N4B_TICKET_DURUM_SAYILARI_COKLU> Stats { get; set; } = new();
        public List<UniCP.Models.MsK.SpModels.SSP_N4B_SLA_ORAN_COKLU> SlaStats { get; set; } = new();
        public List<UniCP.Models.MsK.SpModels.SSP_TFS_GELISTIRME_COKLU> TfsStats { get; set; } = new();
        public List<UniCP.Models.MsK.SpModels.SSP_VARUNA_SIPARIS_COKLU> FinanceOrders { get; set; } = new();
        public List<TBL_VARUNA_SOZLESME> Contracts { get; set; } = new();
        
        // For Univera User (Type 3) - Batched Data
        public Dictionary<int, CompanyData> CompanyMap { get; set; } = new();

        // Error Flags
        public string ErrorMessage { get; set; }
        public bool HasError => !string.IsNullOrEmpty(ErrorMessage);
    }

    public class CompanyData
    {
        public List<UniCP.Models.MsK.SpModels.SSP_N4B_TICKET_DURUM_SAYILARI> Stats { get; set; } = new();
        public List<UniCP.Models.MsK.SpModels.SSP_N4B_SLA_ORAN> SlaData { get; set; } = new();
        public List<UniCP.Models.MsK.SpModels.SSP_N4B_TICKETLARI> OpenTickets { get; set; } = new();
        public List<UniCP.Models.MsK.SpModels.SSP_TFS_GELISTIRME> TfsRequests { get; set; } = new();
        public List<UniCP.Models.MsK.SpModels.SpVarunaSiparisResult> FinanceOrders { get; set; } = new();
    }
}
