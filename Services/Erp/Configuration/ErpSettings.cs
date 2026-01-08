namespace SalesMetrics.Services.Erp.Configuration
{
    /// <summary>
    /// Root configuration for ERP system settings
    /// </summary>
    public class ErpSettings
    {
        /// <summary>
        /// Global default ERP provider (can be overridden per location)
        /// </summary>
        public string DefaultProvider { get; set; } = "CompUFloor";

        /// <summary>
        /// Location-specific ERP configurations
        /// Key: LocationCode (LAX, LSV, CHN, PHX, SND)
        /// </summary>
        public Dictionary<string, LocationErpSettings> Locations { get; set; } = new();

        /// <summary>
        /// Warehouse ID mappings per location (for CompUFloor)
        /// </summary>
        public Dictionary<string, int[]> WarehouseMappings { get; set; } = new()
        {
            { "LAX", new[] { 1, 3 } },
            { "LSV", new[] { 1, 2, 3, 6 } },
            { "CHN", new[] { 1 } },
            { "PHX", new[] { 1, 3 } },
            { "SND", new[] { 1 } }
        };

        /// <summary>
        /// Get warehouse IDs for a location code
        /// </summary>
        public int[] GetWarehouseIds(string locationCode)
        {
            return WarehouseMappings.TryGetValue(locationCode, out var ids)
                ? ids
                : Array.Empty<int>();
        }
    }

    /// <summary>
    /// ERP settings for a specific location
    /// </summary>
    public class LocationErpSettings
    {
        /// <summary>
        /// Location code (LAX, LSV, CHN, PHX, SND)
        /// </summary>
        public string LocationCode { get; set; } = string.Empty;

        /// <summary>
        /// ERP provider for this location: "CompUFloor", "Kudu", etc.
        /// </summary>
        public string Provider { get; set; } = "CompUFloor";

        /// <summary>
        /// Connection string (for SQL-based providers like CompUFloor)
        /// </summary>
        public string? ConnectionString { get; set; }

        /// <summary>
        /// API configuration (for API-based providers like Kudu)
        /// </summary>
        public ErpApiSettings? ApiSettings { get; set; }
    }

    /// <summary>
    /// API configuration for API-based ERP providers
    /// </summary>
    public class ErpApiSettings
    {
        /// <summary>
        /// Base URL for the ERP API (e.g., https://api.kudupro.com/v1)
        /// </summary>
        public string BaseUrl { get; set; } = string.Empty;

        /// <summary>
        /// API Key for authentication
        /// </summary>
        public string? ApiKey { get; set; }

        /// <summary>
        /// Tenant ID (for multi-tenant APIs)
        /// </summary>
        public string? TenantId { get; set; }

        /// <summary>
        /// OAuth settings (if using OAuth instead of API key)
        /// </summary>
        public ErpOAuthSettings? OAuth { get; set; }

        /// <summary>
        /// Request timeout in seconds
        /// </summary>
        public int TimeoutSeconds { get; set; } = 30;

        /// <summary>
        /// Number of retry attempts for failed requests
        /// </summary>
        public int RetryAttempts { get; set; } = 3;
    }

    /// <summary>
    /// OAuth configuration for API authentication
    /// </summary>
    public class ErpOAuthSettings
    {
        public string Authority { get; set; } = string.Empty;
        public string ClientId { get; set; } = string.Empty;
        public string ClientSecret { get; set; } = string.Empty;
        public string Scope { get; set; } = string.Empty;
    }
}
