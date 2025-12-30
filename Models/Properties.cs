using System.Collections.Generic;

namespace SalesMetrics.Models
{
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
        public int? WhsId { get; set; }
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

    public class WorkOrder : CustomerPropertyViewModel
    {
        public string OrderNumber { get; set; }
        public DateTime ScheduledDate { get; set; }
        public string Description { get; set; }
        public decimal Amount { get; set; }
        public string Status { get; set; }
    }

    public class MonthlyInvoiceSummary
    {
        public string InvoiceMonthKey { get; set; } // "2024-09"
        public string InvoiceMonth { get; set; }    // "Sep"
        public string InvoiceYear { get; set; }     // "24"
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
