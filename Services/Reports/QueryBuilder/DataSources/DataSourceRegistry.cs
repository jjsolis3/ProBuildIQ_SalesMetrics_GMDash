using Microsoft.Extensions.Logging;

namespace SalesMetrics.Services.Reports.QueryBuilder.DataSources
{
    /// <summary>
    /// Registry implementation that manages all data source adapters
    /// </summary>
    public class DataSourceRegistry : IDataSourceRegistry
    {
        private readonly Dictionary<string, IDataSourceAdapter> _adapters = new();
        private readonly ILogger<DataSourceRegistry> _logger;

        public DataSourceRegistry(
            IEnumerable<IDataSourceAdapter> adapters,
            ILogger<DataSourceRegistry> logger)
        {
            _logger = logger;

            // Auto-register all injected adapters
            foreach (var adapter in adapters)
            {
                RegisterAdapter(adapter.SourceType, adapter);
                _logger.LogInformation(
                    "Registered data source adapter: {SourceType} - {SourceName} (Available: {IsAvailable})",
                    adapter.SourceType,
                    adapter.SourceName,
                    adapter.IsAvailable);
            }

            if (!_adapters.Any())
            {
                _logger.LogWarning("No data source adapters registered!");
            }
        }

        public IDataSourceAdapter GetAdapter(string sourceType)
        {
            if (string.IsNullOrWhiteSpace(sourceType))
                throw new ArgumentException("Source type cannot be null or empty", nameof(sourceType));

            if (!_adapters.TryGetValue(sourceType.ToUpper(), out var adapter))
            {
                var available = string.Join(", ", _adapters.Keys);
                throw new ArgumentException(
                    $"Data source adapter '{sourceType}' not registered. Available adapters: {available}");
            }

            return adapter;
        }

        public IDataSourceAdapter GetDefaultAdapter()
        {
            // Default to SQL (CompUFloor)
            return GetAdapter("SQL");
        }

        public List<DataSourceInfo> GetAvailableDataSources()
        {
            return _adapters.Values
                .Select(a => new DataSourceInfo
                {
                    SourceType = a.SourceType,
                    SourceName = a.SourceName,
                    IsAvailable = a.IsAvailable,
                    Capabilities = a.GetCapabilities()
                })
                .OrderByDescending(ds => ds.IsAvailable) // Available sources first
                .ThenBy(ds => ds.SourceName)
                .ToList();
        }

        public void RegisterAdapter(string sourceType, IDataSourceAdapter adapter)
        {
            if (string.IsNullOrWhiteSpace(sourceType))
                throw new ArgumentException("Source type cannot be null or empty", nameof(sourceType));

            if (adapter == null)
                throw new ArgumentNullException(nameof(adapter));

            var key = sourceType.ToUpper();

            if (_adapters.ContainsKey(key))
            {
                _logger.LogWarning("Replacing existing adapter for source type: {SourceType}", sourceType);
            }

            _adapters[key] = adapter;
        }
    }
}
