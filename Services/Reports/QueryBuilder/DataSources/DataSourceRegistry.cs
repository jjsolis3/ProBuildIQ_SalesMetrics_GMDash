using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace SalesMetrics.Services.Reports.QueryBuilder.DataSources
{
    /// <summary>
    /// Registry implementation that manages all data source adapters
    /// Uses IServiceProvider to resolve scoped adapters on-demand
    /// </summary>
    public class DataSourceRegistry : IDataSourceRegistry
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<DataSourceRegistry> _logger;

        // Map of source type to adapter type
        private readonly Dictionary<string, Type> _adapterTypes = new()
        {
            { "SQL", typeof(SqlDataSourceAdapter) },
            { "API", typeof(ApiDataSourceAdapter) },
            { "KUDU", typeof(KuduDataSourceAdapter) }
        };

        public DataSourceRegistry(
            IServiceProvider serviceProvider,
            ILogger<DataSourceRegistry> logger)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;

            _logger.LogInformation("DataSourceRegistry initialized with {Count} adapter types", _adapterTypes.Count);
        }

        public IDataSourceAdapter GetAdapter(string sourceType)
        {
            if (string.IsNullOrWhiteSpace(sourceType))
                throw new ArgumentException("Source type cannot be null or empty", nameof(sourceType));

            var key = sourceType.ToUpper();

            if (!_adapterTypes.TryGetValue(key, out var adapterType))
            {
                var available = string.Join(", ", _adapterTypes.Keys);
                throw new ArgumentException(
                    $"Data source adapter '{sourceType}' not registered. Available adapters: {available}");
            }

            // Resolve adapter from service provider (allows scoped resolution)
            var adapter = _serviceProvider.GetRequiredService(adapterType) as IDataSourceAdapter;

            if (adapter == null)
            {
                throw new InvalidOperationException(
                    $"Failed to resolve adapter for source type: {sourceType}");
            }

            _logger.LogDebug(
                "Resolved adapter: {SourceType} - {SourceName} (Available: {IsAvailable})",
                adapter.SourceType,
                adapter.SourceName,
                adapter.IsAvailable);

            return adapter;
        }

        public IDataSourceAdapter GetDefaultAdapter()
        {
            // Default to SQL (CompUFloor)
            return GetAdapter("SQL");
        }

        public List<DataSourceInfo> GetAvailableDataSources()
        {
            var dataSources = new List<DataSourceInfo>();

            foreach (var kvp in _adapterTypes)
            {
                try
                {
                    var adapter = _serviceProvider.GetRequiredService(kvp.Value) as IDataSourceAdapter;
                    if (adapter != null)
                    {
                        dataSources.Add(new DataSourceInfo
                        {
                            SourceType = adapter.SourceType,
                            SourceName = adapter.SourceName,
                            IsAvailable = adapter.IsAvailable,
                            Capabilities = adapter.GetCapabilities()
                        });
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to resolve adapter: {AdapterType}", kvp.Key);
                }
            }

            return dataSources
                .OrderByDescending(ds => ds.IsAvailable) // Available sources first
                .ThenBy(ds => ds.SourceName)
                .ToList();
        }

        public void RegisterAdapter(string sourceType, IDataSourceAdapter adapter)
        {
            // This method is kept for interface compatibility
            // In this implementation, adapters are registered via DI in Program.cs
            _logger.LogWarning(
                "RegisterAdapter called for {SourceType}, but registration should be done via DI in Program.cs",
                sourceType);
        }
    }
}
