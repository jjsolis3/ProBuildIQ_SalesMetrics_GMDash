namespace SalesMetrics.Services.Erp.Models
{
    /// <summary>
    /// Represents aggregated sales metrics for a location/period
    /// </summary>
    public class ErpSalesMetrics
    {
        public int TotalOrders { get; set; }
        public int OnlineOrders { get; set; }
        public int InStoreOrders { get; set; }
        public decimal TotalOrderAmount { get; set; }
        public decimal AverageOrderValue { get; set; }
        public int TotalInvoices { get; set; }
        public decimal TotalRevenue { get; set; }
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public string? LocationCode { get; set; }
    }

    /// <summary>
    /// Represents daily order counts
    /// </summary>
    public class ErpDailyOrderCount
    {
        public DateTime Date { get; set; }
        public string WeekdayName { get; set; } = string.Empty;
        public int OrdersCount { get; set; }
        public decimal TotalOrderAmount { get; set; }
    }

    /// <summary>
    /// Represents AR aging summary
    /// </summary>
    public class ErpARAgingSummary
    {
        public int PendingInvoices { get; set; }
        public decimal PendingInvoicesAmount { get; set; }
        public int DueUnder30 { get; set; }
        public decimal DueUnder30Amount { get; set; }
        public int Due30to60 { get; set; }
        public decimal Due30to60Amount { get; set; }
        public int Due60to90 { get; set; }
        public decimal Due60to90Amount { get; set; }
        public int Due90to120 { get; set; }
        public decimal Due90to120Amount { get; set; }
        public int DueOver120 { get; set; }
        public decimal DueOver120Amount { get; set; }
        public List<ErpCustomerOutstanding> TopDelinquentCustomers { get; set; } = new();
    }

    /// <summary>
    /// Represents a customer with outstanding balance
    /// </summary>
    public class ErpCustomerOutstanding
    {
        public string CustomerName { get; set; } = string.Empty;
        public int CustomerNumber { get; set; }
        public int CustomerId { get; set; }
        public decimal BalanceDue { get; set; }
        public decimal OutstandingAmount { get; set; }
    }

    /// <summary>
    /// Represents salesman performance metrics
    /// </summary>
    public class ErpSalesmanMetrics
    {
        public int SalesmanId { get; set; }
        public string SalesmanName { get; set; } = string.Empty;
        public decimal MTDSales { get; set; }
        public decimal YTDSales { get; set; }
        public int MTDOrderCount { get; set; }
        public int YTDOrderCount { get; set; }
    }

    /// <summary>
    /// Represents monthly invoice summary
    /// </summary>
    public class ErpMonthlyInvoiceSummary
    {
        public string InvoiceMonthKey { get; set; } = string.Empty;
        public string InvoiceMonth { get; set; } = string.Empty;
        public string InvoiceYear { get; set; } = string.Empty;
        public decimal InvoiceAmount { get; set; }
        public int InvoiceCount { get; set; }
    }
}
