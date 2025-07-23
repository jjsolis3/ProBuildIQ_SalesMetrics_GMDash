using Microsoft.AspNetCore.Mvc.Rendering;

namespace SalesMetrics.Models
{
    public class Salesperson
    {
    }

    public class SalesRepProfilePageViewModel
    {
        public int SelectedSalesmanId { get; set; }
        public UserProfileViewModel UserProfile { get; set; }
        public SalesRepMetricsViewModel Metrics { get; set; }
        public List<SelectListItem> SalesReps { get; set; } = new();

        public DateTime StartDate { get; set; } = DateTime.UtcNow.AddMonths(-1); // Default last month
        public DateTime EndDate { get; set; } = DateTime.UtcNow; // Default today
    }



    public class SalesRepNewAccountSummaryViewModel
    {
        public int PropertyId { get; set; }
        public string Property { get; set; }
        public int ManagmentCoID { get; set; }
        public string ManagementCompany { get; set; }
        public DateTime EstablishedDate { get; set; }
        public int Orders { get; set; }
        public decimal TotalSalesAmount { get; set; }
        public int Invoices { get; set; }
        public decimal TotalInvoiceAmount { get; set; }
    }

    public class SalesRepMetricsViewModel
    {
        public int NewAccounts { get; set; }
        public int OrdersCount { get; set; }
        public int InvoicesCount { get; set; }
        public int WithoutOrders { get; set; }
        public decimal TotalSalesAmount { get; set; }
        public decimal TotalInvoiceAmount { get; set; }

        public List<SalesRepNewAccountSummaryViewModel> PropertyDetails { get; set; }
        public SalesRepPerformanceViewModel PerformanceKPI { get; set; }
    }

    public class SalesRepPerformanceViewModel
    {
        public decimal MTDSales { get; set; }
        public decimal YTDSales { get; set; }
        public int TaskCount { get; set; }
        public int CompletedTasks { get; set; }
        public double TaskCompletionRate => TaskCount > 0 ? (CompletedTasks * 100.0 / TaskCount) : 0;
        public int InactiveAccounts { get; set; }
        public decimal TargetSalesGoal { get; set; }
        public string PerformanceRating { get; set; } // e.g. "Excellent", "Needs Improvement"
        public List<(string MonthLabel, decimal MonthlyTotal)> MonthlyTrend { get; set; } = new();
        public List<MonthlyPerformanceTrend> MonthlyPerformance { get; set; } = new();
    }

    public class MonthlyPerformanceTrend
    {
        public int InvoiceCount { get; set; } 
        public string MonthLabel { get; set; }
        public decimal Actual { get; set; }
        public decimal Average { get; set; }
        public decimal Goal { get; set; }
        public decimal Difference => Actual - Average;
        public double PercentToGoal => Goal == 0 ? 0 : (double)(Actual / Goal) * 100;
    }



}
