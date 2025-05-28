namespace SalesMetrics.Models
{
    public class DailyOrderCount
    {
        public string WeekdayName { get; set; } = string.Empty;
        public int OrdersCount { get; set; }
    }


    public class TransactionSummary
    {
        public int PendingInvoices { get; set; }
        public int DueUnder30 { get; set; }
        public int Due30to60 { get; set; }
        public int Due60to90 { get; set; }
        public int Due90to120 { get; set; }
        public int DueOver120 { get; set; }
        public double PendingInvoicesAmount { get; set; }
        public double DueUnder30Amount { get; set; }
        public double Due30to60Amount { get; set; }
        public double Due60to90Amount { get; set; }
        public double Due90to120Amount { get; set; }
        public double DueOver120Amount { get; set; }
        public List<CustomerOutstanding> TopDelinquentCustomers { get; set; }
    }

    public class CustomerOutstanding
    {
        public string CustomerName { get; set; }
        public int CustomerNumber { get; set; }
        public int CustomerId { get; set; }
        public decimal BalanceDue { get; set; }
        public decimal OutstandingAmount { get; set; }
    }

    public class OverdueInvoice
    {
        public int Invoice { get; set; }
        public int CustomerId { get; set; }
        public string CustomerName { get; set; }
        public string MgmtCo { get; set; }
        public decimal OutstandingAmount { get; set; }
        public DateTime DueDate { get; set; }
        public int DaysPastDue { get; set; }
        public string InvoiceAging { get; set; }
        public int SalespersonId { get; set; }
        public string Salesperson { get; set; }
    }
    public class InvoiceDetailViewModel
    {
        public InvoiceHeader Header { get; set; } = new InvoiceHeader();
        public List<OrderLineItem> LineItems { get; set; } = new List<OrderLineItem>();
        public List<OrderNotes> Notes { get; set; } = new List<OrderNotes>();
    }

    public class WorkOrderDetailViewModel
    {
        public SalesOrderHeader Header { get; set; } = new SalesOrderHeader();
        public List<OrderLineItem> LineItems { get; set; } = new List<OrderLineItem>();
        public List<OrderNotes> Notes { get; set; } = new List<OrderNotes>();
    }

    public class InvoiceHeader
    {
        public string CustomerName { get; set; }
        public int CustomerNumber { get; set; }
        public int InvoiceNumber { get; set; }
        public int OrderNumber { get; set; }
        public DateTime InvoiceDate { get; set; }
        public DateTime DeliveryDate { get; set; }
        public decimal OutstandingAmount { get; set; }
        public string? InvoiceAging { get; set; }
        public string? CustomerPO { get; set; }
    }

    public class SalesOrderHeader
    {
        public string CustomerName { get; set; }
        public int CustomerNumber { get; set; }
        public int InvoiceNumber { get; set; }
        public int OrderNumber { get; set; }
        public DateTime OrderDate { get; set; }
        public DateTime DeliveryDate { get; set; }
        public DateTime InvoiceDate { get; set; }
        public string? PaidInFullDate { get; set; }
        public decimal BalanceAmount { get; set; }
        public string? OrderAging { get; set; }
        public string? CustomerPO { get; set; }
        public string ShipAddress { get; set; }
        public string ShipState { get; set; }
        public string ShipZip { get; set; }
        public string Bldg { get; set; }
        public string AptNumber { get; set; }
        public int SalespersonId { get; set; }
        public string Salesperson { get; set; }
        public int PriceCode { get; set; }
        public string MgmtCo { get; set; }
        public string OrderedBy { get; set; }
        public string DeliveryAddress => ShipAddress + ", " + ShipState + ", " + ShipZip;
        public int WhsId { get; set; }
        
    }

    public class OrderLineItem
    {
        public string? Prod_Style { get; set; }
        public string? Prod_Color { get; set; }
        public decimal Quantity { get; set; }
        public string? UOM { get; set; }
        public string? ProdClass { get; set; }
        public string? Description { get; set; }
        public decimal? ProductTotal { get; set; }
    }

    public class OrderNotes
    {
        public int? LineNumber { get; set; }
        public string? Comment { get; set; }
    }

    public class WorkOrderViewModel
    {
        public int PropertyId { get; set; }
        public int PropertyNumber { get; set; }
        public string PropertyName { get; set; }
        public string City { get; set; }
        public string OrderType { get; set; }
        public int NumOfRooms { get; set; }
        public double Qty { get; set; }
        public string Notes { get; set; }
        public string UnitNumber { get; set; }
        public string UnitType { get; set; }
        public string DeliveryDate { get; set; }
        public string MoveInDate { get; set; }
        public string PaidInFullDate { get; set; }
        public string ProductClass { get; set; }
        public string ProductDescription { get; set; }
        public string OrderedBy { get; set; }
        public string OrderID { get; set; }
        public string OrderTime { get; set; }
        public int ManagementCode { get; set; }
        public string ManagementName { get; set; }
        public string Status { get; set; }
        public string Location { get; set; }
        public int SalesmanId { get; set; }
        public string SalesmanName { get; set; }

        private static readonly Dictionary<string, string> LocationMap = new()
        {
            { "1", "LAX" },
            { "2", "LSV" },
            { "3", "CHN" },
            { "4", "PHX" },
            { "5", "SND" }
        };

        public string LocationName => LocationMap.TryGetValue(Location, out var loc) ? loc : "Unknown";

    }

}