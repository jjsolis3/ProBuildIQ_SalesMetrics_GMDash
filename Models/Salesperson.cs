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

        public LostAndReturningCustomerSummaryViewModel LostAndReturningCustomers { get; set; }
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

    /// <summary>
    /// Enhanced model that includes AR balance information for better financial tracking
    /// </summary>
    public class LostAndReturningCustomerViewModel
    {
        public int CustomerId { get; set; }
        public int CustomerNumber { get; set; }
        public string CustomerName { get; set; }
        public string ManagementCompany { get; set; }

        // Last order before the gap
        public DateTime LastOrderBeforeGap { get; set; }
        public string PreviousSalesperson { get; set; }
        public int PreviousSalesmanId { get; set; }
        public decimal LastOrderAmount { get; set; }

        // First order after returning
        public DateTime FirstOrderAfterReturn { get; set; }
        public string ReturningSalesperson { get; set; }
        public int ReturningSalesmanId { get; set; }
        public decimal ReturningOrderAmount { get; set; }

        // NEW: AR balance information
        public decimal ReturningBalanceDue { get; set; }
        public decimal TotalBalanceDue { get; set; }

        // Gap analysis
        public int MonthsInactive { get; set; }
        public bool SalespersonChanged => PreviousSalesmanId != ReturningSalesmanId;

        // Summary stats for the returning period
        public int OrdersSinceReturn { get; set; }
        public decimal TotalAmountSinceReturn { get; set; }

        // Helper properties for display
        public string GapDescription => $"{MonthsInactive} months";
        public string SalespersonChangeStatus => SalespersonChanged ? "Changed" : "Same";
        public decimal RevenueRecovered => TotalAmountSinceReturn;

        // NEW: Financial health indicators
        public decimal CollectionRate => TotalAmountSinceReturn > 0 ?
            ((TotalAmountSinceReturn - TotalBalanceDue) / TotalAmountSinceReturn) * 100 : 0;
        public bool HasOutstandingBalance => TotalBalanceDue > 0;
        public string PaymentStatus => TotalBalanceDue == 0 ? "Paid in Full" :
            TotalBalanceDue >= TotalAmountSinceReturn ? "Unpaid" : "Partially Paid";
    }

    /// <summary>
    /// Enhanced summary with AR balance tracking
    /// </summary>
    public class LostAndReturningCustomerSummaryViewModel
    {
        public int TotalReturningCustomers { get; set; }
        public int CustomersWithSalespersonChange { get; set; }
        public int CustomersWithSameSalesperson { get; set; }
        public decimal TotalRevenueRecovered { get; set; }
        public decimal AverageGapInMonths { get; set; }
        public decimal AverageReturningOrderValue { get; set; }

        // NEW: AR tracking
        public decimal TotalOutstandingBalance { get; set; }
        public decimal OverallCollectionRate { get; set; }
        public int CustomersWithOutstandingBalance { get; set; }

        // Insights
        public double PercentageWithSalespersonChange =>
            TotalReturningCustomers > 0 ? (CustomersWithSalespersonChange * 100.0 / TotalReturningCustomers) : 0;

        // NEW: Financial insights
        public double PercentageWithOutstandingBalance =>
            TotalReturningCustomers > 0 ? (CustomersWithOutstandingBalance * 100.0 / TotalReturningCustomers) : 0;

        public List<LostAndReturningCustomerViewModel> CustomerDetails { get; set; } = new();
    }
}

