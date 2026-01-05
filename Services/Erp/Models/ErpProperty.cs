namespace SalesMetrics.Services.Erp.Models
{
    /// <summary>
    /// Represents a customer property/account in the ERP system
    /// </summary>
    public class ErpProperty
    {
        public int CustomerId { get; set; }
        public string CustomerNumber { get; set; } = string.Empty;
        public string CustomerName { get; set; } = string.Empty;
        public string? Address1 { get; set; }
        public string? Address2 { get; set; }
        public string? Address3 { get; set; }
        public string? City { get; set; }
        public string? State { get; set; }
        public string? Zip { get; set; }
        public string? PhoneNumber { get; set; }
        public string? Email { get; set; }
        public decimal? CreditLimit { get; set; }
        public decimal? ARBalance { get; set; }
        public int CreditHoldFlag { get; set; }
        public int? PriceCode { get; set; }
        public int? SalesmanId { get; set; }
        public string? SalesmanName { get; set; }
        public DateTime? EstablishedDate { get; set; }
        public string? AttentionTo { get; set; }
        public bool PONumberRequired { get; set; }
        public int? WarehouseId { get; set; }
        public string? ManagementCompany { get; set; }
    }

    /// <summary>
    /// Represents a property with additional details
    /// </summary>
    public class ErpPropertyDetails : ErpProperty
    {
        public decimal TotalRevenue { get; set; }
        public int CompletedOrdersCount { get; set; }
        public int PendingOrdersCount { get; set; }
        public int OverdueInvoicesCount { get; set; }
        public decimal AmountDue { get; set; }
    }

    /// <summary>
    /// Represents a simple property reference (for dropdowns/search)
    /// </summary>
    public class ErpPropertyReference
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? CustomerNumber { get; set; }
    }
}
