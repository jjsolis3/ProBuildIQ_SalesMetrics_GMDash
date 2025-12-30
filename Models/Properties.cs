using System.Collections.Generic;

namespace SalesMetrics.Models
{   
    // ============================================================
    // DASHBOARD VIEWMODEL - Contains aggregate KPIs for the entire property list
    // ============================================================
    /// <summary>
    /// This ViewModel holds all the aggregate metrics shown in the KPI cards
    /// at the top of the dashboard. It provides a high-level overview of
    /// all properties in the system.
    /// </summary>
    public class PropertyDashboardViewModel
    {
        // === PROPERTY COUNTS ===
        public int TotalProperties { get; set; }
        public int ActiveProperties { get; set; }  // Properties not on credit hold
        public int CreditHoldProperties { get; set; }  // Properties currently on credit hold

        // === FINANCIAL METRICS ===
        public decimal TotalARBalance { get; set; }  // Sum of all outstanding AR balances
        public decimal TotalCreditLimit { get; set; }  // Sum of all credit limits
        public decimal AverageCreditLimit { get; set; }  // Average credit limit per property

        // === UTILIZATION METRICS ===
        /// <summary>
        /// Credit Utilization Rate = (Total AR / Total Credit Limit) * 100
        /// Shows what percentage of available credit is being used across all properties
        /// </summary>
        public decimal CreditUtilizationRate { get; set; }

        /// <summary>
        /// Collection Rate = ((Total Credit - Total AR) / Total Credit) * 100
        /// Shows what percentage of credit has been collected/is not outstanding
        /// </summary>
        public decimal CollectionRate { get; set; }

        // === BREAKDOWN BY CREDIT STATUS ===
        public decimal ARBalanceActive { get; set; }  // AR from properties NOT on credit hold
        public decimal ARBalanceCreditHold { get; set; }  // AR from properties ON credit hold

        // === THE ACTUAL PROPERTY LIST WITH RANKINGS ===
        public List<CustomerPropertyEnhancedViewModel> Properties { get; set; } = new();

        // === TOP PERFORMERS (for the special highlight section) ===
        public List<CustomerPropertyEnhancedViewModel> TopPerformers { get; set; } = new();
    }

    // ============================================================
    // ENHANCED PROPERTY VIEWMODEL - Extends the base with ranking and percentage calculations
    // ============================================================
    /// <summary>
    /// This extends the base CustomerPropertyViewModel with additional
    /// calculated fields for the dashboard display, including rankings
    /// and percentage-based metrics.
    /// </summary>
    public class CustomerPropertyEnhancedViewModel : CustomerPropertyViewModel
    {
        /// <summary>
        /// Rank based on AR Balance (1 = highest AR balance)
        /// This allows us to show badges and identify top/bottom performers
        /// </summary>
        public int Rank { get; set; }

        /// <summary>
        /// What percentage of the total AR does this property represent?
        /// Useful for identifying which properties contribute most to outstanding receivables
        /// </summary>
        public decimal ARBalancePercentage { get; set; }

        /// <summary>
        /// Credit Utilization = (ARBalance / CreditLimit) * 100
        /// Shows how much of this property's credit limit is currently used
        /// High values (>80%) might indicate a risk
        /// </summary>
        public decimal? CreditUtilization { get; set; }

        /// <summary>
        /// How many days since the property was established?
        /// Useful for understanding property age and lifecycle
        /// </summary>
        public int? DaysSinceEstablished { get; set; }

        /// <summary>
        /// Badge color for the rank (gold, silver, bronze, default)
        /// Makes it easy to visually identify top performers in the view
        /// </summary>
        public string RankBadgeColor
        {
            get
            {
                return Rank switch
                {
                    1 => "gold",      // #FFD700 - First place
                    2 => "silver",    // #C0C0C0 - Second place  
                    3 => "bronze",    // #CD7F32 - Third place
                    _ => "secondary"  // Gray - Everyone else
                };
            }
        }

        /// <summary>
        /// CSS class for credit utilization indicator
        /// Helps show visual warnings for high utilization
        /// </summary>
        public string CreditUtilizationClass
        {
            get
            {
                if (!CreditUtilization.HasValue) return "text-muted";
                if (CreditUtilization.Value >= 90) return "text-danger fw-bold";  // Critical
                if (CreditUtilization.Value >= 75) return "text-warning fw-bold"; // Warning
                return "text-success";  // Healthy
            }
        }
    }

    // ============================================================
    // BASE VIEWMODELS (from your original file - keeping for reference)
    // ============================================================

    public class CustomerPropertyViewModel
    {
        public int CustomerId { get; set; }
        public string CustomerNumber { get; set; }
        public string CustomerName { get; set; }
        public int PriceCode { get; set; }
        public string MgmtCo { get; set; }
        public string Address { get; set; }
        public string City { get; set; }
        public string State { get; set; }
        public string Zip { get; set; }
        public string PhoneNumber { get; set; }
        public string Email { get; set; }
        public decimal? CreditLimit { get; set; }
        public DateTime? EstablishedDate { get; set; }
        public int CreditHold { get; set; }
        public decimal? ARBalance { get; set; }
        public string AttentionTo { get; set; }
        public bool PONumberRequired { get; set; }
        public int SalesmanID { get; set; }
        public string? Salesperson { get; set; }
        public int WhsID { get; set; }
    }

    public class PropertyDetailsViewModel
    {
        public int PropertyId { get; set; }
        public string CustomerName { get; set; }
        public string CustomerNumber { get; set; }
        public string Address { get; set; }
        public string City { get; set; }
        public string State { get; set; }
        public string Zip { get; set; }

        public List<Invoice> Invoices { get; set; }
        public List<Invoice> InvoicesLastYear => Invoices
            .Where(i => i.Date >= DateTime.Today.AddYears(-1)).ToList();
        public List<WorkOrderViewModel> WorkOrders { get; set; } = new();
        public List<MonthlyInvoiceSummary> MonthlyInvoices { get; set; } = new();
        public List<PropertyNote> PropertyNotes { get; set; } = new();

        public DateTime? EstablishedDate { get; set; }
        public double? CreditLimit { get; set; }
        public int CreditHold { get; set; }
        public int PriceCode { get; set; }
        public string MgmtCo { get; set; }
        public string AttnTo { get; set; }
        public int SalesmanID { get; set; }
        public string SalemanName { get; set; }
        public string SalemanNumber { get; set; }

        // AR Sales Data
        public decimal ARAmountDue { get; set; }
        public decimal ARTotalSales { get; set; }
        public int ARInvoiceCount { get; set; }
        public int AROverdueInvoiceCount { get; set; }

        // new metrics for the top card only
        public decimal TotalRevenueLastYear => InvoicesLastYear.Sum(i => i.Amount);
        public int CompletedOrdersLastYear => InvoicesLastYear.Count(i => i.IsPaid);
        public int PendingOrdersLastYear => InvoicesLastYear.Count(i => !i.IsPaid);

        // existing KPI metrics
        public decimal TotalRevenue => Invoices.Sum(i => i.Amount);
        public int CompletedOrders => Invoices.Count(i => i.IsPaid);
        public int PendingOrders => Invoices.Count(i => !i.IsPaid);
    }

    public class Invoice
    {
        public DateTime Date { get; set; }
        public decimal Amount { get; set; }
        public bool IsPaid { get; set; }
    }

    //public class WorkOrderViewModel
    //{
    //    public string OrderID { get; set; }
    //    public int ManagementCode { get; set; }
    //    public string ManagementName { get; set; }
    //    public int PropertyId { get; set; }
    //    public int PropertyNumber { get; set; }
    //    public string PropertyName { get; set; }
    //    public string City { get; set; }
    //    public string OrderType { get; set; }
    //    public double Qty { get; set; }
    //    public string UnitNumber { get; set; }
    //    public string UnitType { get; set; }
    //    public string DeliveryDate { get; set; }
    //    public string PaidInFullDate { get; set; }
    //    public string ProductClass { get; set; }
    //    public string ProductDescription { get; set; }
    //    public string OrderedBy { get; set; }
    //    public string Status { get; set; }
    //    public string Location { get; set; }
    //}

    public class MonthlyInvoiceSummary
    {
        public string InvoiceMonthKey { get; set; }
        public string InvoiceMonth { get; set; }
        public string InvoiceYear { get; set; }
        public double InvoiceAmount { get; set; }
    }

    public class InactiveCustomerViewModel : CustomerPropertyViewModel
    {
        public int Units { get; set; }
        public DateTime? LastOrderDate { get; set; }
        public DateTime? LastInstallDate { get; set; }
        public int DaysSinceLastOrder { get; set; }
        public int DaysSinceLastInstall { get; set; }
        public double YTDCurrent { get; set; }
        public double YTDPrevious { get; set; }
        public double Balance { get; set; }
    }

    public class PropertyNote
    {
        public int NoteID { get; set; }
        public string PropertyId { get; set; }
        public int CustomerNumber { get; set; }
        public string NoteText { get; set; }
        public string CreatedBy { get; set; }
        public DateTime CreatedDate { get; set; }
        public string ModifiedBy { get; set; }
        public DateTime? ModifiedDate { get; set; }
        public bool IsActive { get; set; } = true;
        public int LocationId { get; set; }
    }
}
