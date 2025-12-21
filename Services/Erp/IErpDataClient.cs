using System.Data;
using System.Threading;

namespace SalesMetrics.Services.Erp
{
    public interface IErpDataClient
    {
        Task<DataTable> QueryAsync(string queryName, string sql, object parameters, ErpContext context, CancellationToken cancellationToken = default);
    }
}
