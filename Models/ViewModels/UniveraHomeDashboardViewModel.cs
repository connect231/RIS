using UniCP.Models.MsK;
using UniCP.Models.MsK.SpModels;

namespace UniCP.Models.ViewModels
{
    /// <summary>
    /// ViewModel for Univera Home Dashboard
    /// Replaces ViewBag usage with type-safe properties
    /// </summary>
    public class UniveraHomeDashboardViewModel
    {
        // ===== Company & User Info =====
        public List<VIEW_ORTAK_PROJE_ISIMLERI> AuthorizedCompanies { get; set; } = new();
        public string AuthorizedCompanyNames { get; set; } = string.Empty;
        public int? SelectedCompanyId { get; set; }
        public TBL_KULLANICI? Kullanici { get; set; }

        // ===== Ticket Statistics =====
        public int OpenTicketsCount { get; set; }
        public int CriticalCount { get; set; }

        // ===== Development Requests =====
        public int OpenDevRequestsCount { get; set; }
        public int CompletedDevRequestsCount { get; set; }
        public int UatTestCount { get; set; }

        // ===== Budget & Finance =====
        public decimal PendingBudgetEffort { get; set; }
        public decimal PendingBudgetCost { get; set; }
        public int FinancePendingCount { get; set; }

        // ===== SLA =====
        public decimal TotalSla { get; set; }
        public List<SSP_N4B_SLA_ORAN> SlaHistory { get; set; } = new();

        // ===== Chart Data =====
        public object? BudgetApprovalStats { get; set; }
        public object? SupportRequestStats { get; set; }
        public object? FinanceChartData { get; set; }
        public decimal? FinanceOpenAmount { get; set; }
        public decimal? FinanceClosedAmount { get; set; }
        public int MonthlyVolumeCount { get; set; }

        // ===== License =====
        public int ExpiredLicenseCount { get; set; }

        // ===== Debug Info (optional - can be removed in production) =====
        public List<string> DebugFlow { get; set; } = new();
        public int DebugTargetCompaniesCount { get; set; }
        public List<string> DebugExceptions { get; set; } = new();
        public object? DebugOrders { get; set; }
    }
}
