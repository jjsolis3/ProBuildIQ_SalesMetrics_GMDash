using SalesMetrics.Models.Reports.QueryBuilder;

namespace SalesMetrics.Services.Reports.QueryBuilder.DataSources
{
    /// <summary>
    /// Registry for managing multiple data source adapters
    /// </summary>
    public interface IDataSourceRegistry
    {
        /// <summary>
        /// Get adapter by source type (SQL, API, Kudu)
        /// </summary>
        IDataSourceAdapter GetAdapter(string sourceType);

        /// <summary>
        /// Get the default adapter (SQL for now)
        /// </summary>
        IDataSourceAdapter GetDefaultAdapter();

        /// <summary>
        /// Get list of all available data sources
        /// </summary>
        List<DataSourceInfo> GetAvailableDataSources();

        /// <summary>
        /// Register a new adapter
        /// </summary>
        void RegisterAdapter(string sourceType, IDataSourceAdapter adapter);
    }

    public class DataSourceInfo
    {
        public string SourceType { get; set; } = string.Empty;
        public string SourceName { get; set; } = string.Empty;
        public bool IsAvailable { get; set; }
        public SourceCapabilities Capabilities { get; set; } = new();
    }
}
