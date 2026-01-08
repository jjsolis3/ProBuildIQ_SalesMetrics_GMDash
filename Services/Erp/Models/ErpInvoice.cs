namespace SalesMetrics.Services.Erp.Models
{
    /// <summary>
    /// Represents an invoice in the ERP system
    /// </summary>
    public class ErpInvoice
    {
        public string InvoiceNumber { get; set; } = string.Empty;
        public string? OrderNumber { get; set; }
        public int CustomerId { get; set; }
        public string? CustomerNumber { get; set; }
        public string? CustomerName { get; set; }
        public DateTime? InvoiceDate { get; set; }
        public DateTime? DeliveryDate { get; set; }
        public decimal InvoiceAmount { get; set; }
        public decimal BalanceDue { get; set; }
        public DateTime? DueDate { get; set; }
        public DateTime? PaidInFullDate { get; set; }
        public bool IsPaid => PaidInFullDate.HasValue;
        public string? InvoiceType { get; set; }
        public int? WarehouseId { get; set; }
        public string? CustomerPO { get; set; }
        public int? SalesmanId { get; set; }
        public string? SalesmanName { get; set; }
        public string? ManagementCompany { get; set; }
    }

    /// <summary>
    /// Represents detailed invoice information
    /// </summary>
    public class ErpInvoiceDetails : ErpInvoice
    {
        public List<ErpOrderLineItem> LineItems { get; set; } = new();
        public List<ErpOrderNote> Notes { get; set; } = new();
        public int DaysPastDue { get; set; }
        public string? InvoiceAging { get; set; }
    }

    /// <summary>
    /// Represents an overdue invoice
    /// </summary>
    public class ErpOverdueInvoice
    {
        public string InvoiceNumber { get; set; } = string.Empty;
        public int CustomerId { get; set; }
        public string CustomerName { get; set; } = string.Empty;
        public string? ManagementCompany { get; set; }
        public decimal OutstandingAmount { get; set; }
        public DateTime DueDate { get; set; }
        public int DaysPastDue { get; set; }
        public string InvoiceAging { get; set; } = string.Empty;
        public int? SalesmanId { get; set; }
        public string? SalesmanName { get; set; }
    }
}
