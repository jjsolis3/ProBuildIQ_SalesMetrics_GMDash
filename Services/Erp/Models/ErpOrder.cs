namespace SalesMetrics.Services.Erp.Models
{
    /// <summary>
    /// Represents a sales order in the ERP system
    /// </summary>
    public class ErpOrder
    {
        public int OrderId { get; set; }
        public string OrderNumber { get; set; } = string.Empty;
        public int CustomerId { get; set; }
        public string? CustomerName { get; set; }
        public string? CustomerNumber { get; set; }
        public DateTime? OrderDate { get; set; }
        public DateTime? DeliveryDate { get; set; }
        public decimal? TotalAmount { get; set; }
        public int? WarehouseId { get; set; }
        public string? Operator { get; set; }  // "Online" flag
        public bool IsOnlineOrder { get; set; }
        public DateTime? CanceledDate { get; set; }
        public bool IsCanceled { get; set; }
        public string? InvoiceType { get; set; }
        public int? SalesmanId { get; set; }
        public string? SalesmanName { get; set; }
        public int? PriceCode { get; set; }
        public string? CustomerPO { get; set; }
        public string? ShipAddress { get; set; }
        public string? ShipCity { get; set; }
        public string? ShipState { get; set; }
        public string? ShipZip { get; set; }
        public string? Building { get; set; }
        public string? AptNumber { get; set; }
        public string? OrderedBy { get; set; }
        public string? ManagementCompany { get; set; }
    }

    /// <summary>
    /// Represents detailed order information including line items
    /// </summary>
    public class ErpOrderDetails : ErpOrder
    {
        public List<ErpOrderLineItem> LineItems { get; set; } = new();
        public List<ErpOrderNote> Notes { get; set; } = new();
        public DateTime? InvoiceDate { get; set; }
        public DateTime? PaidInFullDate { get; set; }
        public decimal? BalanceAmount { get; set; }
        public string? OrderAging { get; set; }
    }

    /// <summary>
    /// Represents an order line item
    /// </summary>
    public class ErpOrderLineItem
    {
        public int LineId { get; set; }
        public int OrderId { get; set; }
        public string? ProductStyle { get; set; }
        public string? ProductColor { get; set; }
        public decimal Quantity { get; set; }
        public string? UOM { get; set; }
        public string? ProductClass { get; set; }
        public string? Description { get; set; }
        public decimal? ProductTotal { get; set; }
    }

    /// <summary>
    /// Represents an order note/comment
    /// </summary>
    public class ErpOrderNote
    {
        public int? LineNumber { get; set; }
        public string? Comment { get; set; }
    }

    /// <summary>
    /// Represents a simple order reference (for dropdowns)
    /// </summary>
    public class ErpOrderReference
    {
        public int Id { get; set; }
        public string Display { get; set; } = string.Empty;
        public DateTime? OrderDate { get; set; }
        public decimal? Amount { get; set; }
    }
}
