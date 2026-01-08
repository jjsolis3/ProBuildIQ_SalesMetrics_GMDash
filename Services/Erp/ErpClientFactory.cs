using Microsoft.Extensions.Options;
using SalesMetrics.Services.Erp.Configuration;
using SalesMetrics.Services.Erp.Clients;

namespace SalesMetrics.Services.Erp
{
    /// <summary>
    /// Factory for creating the appropriate IErpDataClient based on location and provider configuration
    /// </summary>
    public class ErpClientFactory
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ErpSettings _erpSettings;
        private readonly ILogger<ErpClientFactory> _logger;

        public ErpClientFactory(
            IServiceProvider serviceProvider,
            IOptions<ErpSettings> erpSettings,
            ILogger<ErpClientFactory> logger)
        {
            _serviceProvider = serviceProvider;
            _erpSettings = erpSettings.Value;
            _logger = logger;
        }

        /// <summary>
        /// Get the appropriate ERP client for a location
        /// </summary>
        public IErpDataClient GetClient(string locationCode)
        {
            // Get location-specific settings
            if (!_erpSettings.Locations.TryGetValue(locationCode, out var locationSettings))
            {
                _logger.LogWarning(
                    "No ERP settings found for location {LocationCode}, using default provider {DefaultProvider}",
                    locationCode, _erpSettings.DefaultProvider);

                return GetClientByProvider(_erpSettings.DefaultProvider, locationCode, null);
            }

            _logger.LogDebug(
                "Creating {Provider} ERP client for location {LocationCode}",
                locationSettings.Provider, locationCode);

            return GetClientByProvider(locationSettings.Provider, locationCode, locationSettings);
        }

        /// <summary>
        /// Get the appropriate ERP client from ErpContext
        /// </summary>
        public IErpDataClient GetClient(ErpContext context)
        {
            var locationCode = context.LocationCode ?? ResolveLocationCode(context.LocationId);
            return GetClient(locationCode);
        }

        /// <summary>
        /// Create client instance based on provider type
        /// </summary>
        private IErpDataClient GetClientByProvider(
            string provider,
            string locationCode,
            LocationErpSettings? settings)
        {
            return provider.ToLowerInvariant() switch
            {
                "compufloor" or "cuf" => _serviceProvider.GetRequiredService<CompUFloorErpClient>(),
                "kudu" or "kudupro" => _serviceProvider.GetRequiredService<KuduErpClient>(),
                _ => throw new NotSupportedException(
                    $"ERP provider '{provider}' is not supported. " +
                    $"Supported providers: CompUFloor, Kudu")
            };
        }

        /// <summary>
        /// Resolve location code from location ID
        /// </summary>
        private string ResolveLocationCode(int locationId)
        {
            return locationId switch
            {
                1 => "LAX",
                2 => "LSV",
                3 => "CHN",
                4 => "PHX",
                5 => "SND",
                _ => throw new ArgumentException($"Invalid location ID: {locationId}", nameof(locationId))
            };
        }

        /// <summary>
        /// Get the provider type for a location
        /// </summary>
        public string GetProviderType(string locationCode)
        {
            if (_erpSettings.Locations.TryGetValue(locationCode, out var settings))
            {
                return settings.Provider;
            }

            return _erpSettings.DefaultProvider;
        }

        /// <summary>
        /// Check if a location uses a specific provider
        /// </summary>
        public bool UsesProvider(string locationCode, string provider)
        {
            var actualProvider = GetProviderType(locationCode);
            return string.Equals(actualProvider, provider, StringComparison.OrdinalIgnoreCase);
        }
    }
}
