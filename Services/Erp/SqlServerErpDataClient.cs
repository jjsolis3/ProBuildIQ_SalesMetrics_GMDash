using System.Data;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Threading;

namespace SalesMetrics.Services.Erp
{
    public class SqlServerErpDataClient : IErpDataClient
    {
        private readonly IConfiguration _configuration;
        private readonly ILogger<SqlServerErpDataClient> _logger;

        public SqlServerErpDataClient(IConfiguration configuration, ILogger<SqlServerErpDataClient> logger)
        {
            _configuration = configuration;
            _logger = logger;
        }

        public async Task<DataTable> QueryAsync(string queryName, string sql, object parameters, ErpContext context, CancellationToken cancellationToken = default)
        {
            var connectionName = context.ConnectionName ?? context.LocationCode ?? "SalesMetrics";
            var connectionString = _configuration.GetConnectionString(connectionName);
            if (string.IsNullOrWhiteSpace(connectionString))
            {
                throw new InvalidOperationException($"Connection string '{connectionName}' was not found.");
            }

            _logger.LogInformation("Executing ERP query {QueryName} against connection {ConnectionName}", queryName, connectionName);

            await using var connection = new SqlConnection(connectionString);
            await using var command = connection.CreateCommand();
            command.CommandText = sql;
            command.CommandType = CommandType.Text;

            foreach (var property in parameters.GetType().GetProperties())
            {
                var value = property.GetValue(parameters) ?? DBNull.Value;
                command.Parameters.Add(new SqlParameter($"@{property.Name}", value));
            }

            await connection.OpenAsync(cancellationToken);
            var table = new DataTable(queryName);
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            table.Load(reader);
            return table;
        }
    }
}
