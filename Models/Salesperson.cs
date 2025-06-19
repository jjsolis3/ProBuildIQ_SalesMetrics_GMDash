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
    }

}
