namespace SalesMetrics.Services.Erp.Models
{
    /// <summary>
    /// Represents a salesman/sales representative
    /// </summary>
    public class ErpSalesman
    {
        public int SalesmanId { get; set; }
        public string SalesmanName { get; set; } = string.Empty;
        public string? SalesmanNumber { get; set; }
        public bool IsActive { get; set; }
    }

    /// <summary>
    /// Represents a price code
    /// </summary>
    public class ErpPriceCode
    {
        public int PriceCode { get; set; }
        public string Description { get; set; } = string.Empty;
    }

    /// <summary>
    /// Represents a warehouse/location
    /// </summary>
    public class ErpWarehouse
    {
        public int WarehouseId { get; set; }
        public string WarehouseNumber { get; set; } = string.Empty;
        public string WarehouseName { get; set; } = string.Empty;
        public string? LocationCode { get; set; }
    }

    /// <summary>
    /// Represents a product/item
    /// </summary>
    public class ErpProduct
    {
        public string ProductId { get; set; } = string.Empty;
        public string? ProductClass { get; set; }
        public string? Description { get; set; }
        public bool IsPrivateLabel { get; set; }
    }

    /// <summary>
    /// Common result wrapper for paged data
    /// </summary>
    public class PagedResult<T>
    {
        public List<T> Items { get; set; } = new();
        public int TotalCount { get; set; }
        public int PageSize { get; set; }
        public int CurrentPage { get; set; }
        public int TotalPages => PageSize > 0 ? (int)Math.Ceiling(TotalCount / (double)PageSize) : 0;
        public bool HasNextPage => CurrentPage < TotalPages;
        public bool HasPreviousPage => CurrentPage > 1;
    }
}
