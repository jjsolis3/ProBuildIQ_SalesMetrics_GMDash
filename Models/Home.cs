using Microsoft.Identity.Client;

namespace SalesMetrics.Models
{
    public class DashboardViewModel
    {
        public string Greeting { get; set; }
        public string FullName { get; set; }
        public Dictionary<string, object> SalesData { get; set; }
        public List<DailyOrderCount> WeeklyOrders { get; set; }
        public TransactionSummary TransactionSummary { get; set; }
        public List<OverdueInvoice> OverdueInvoices { get; set; }
        public List<SalesTask> TodayTasks { get; set; }
    
    }

    public class SalesRanking
    {
        public string SalespersonName { get; set; }
        public int SalespersonID { get; set; }
        public decimal MTDSales { get; set; }
        public decimal YTDSales { get; set; }
    }


}
