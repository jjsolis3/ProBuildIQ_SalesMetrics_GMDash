
using SalesMetrics.Models;

namespace SalesMetrics.Services
{
    public interface ISalesMetricsService
    {
        /// <summary>
        /// Gets sales metrics for the specified date range and location
        /// </summary>
        Task<List<GMBranchSalesMetricsViewModel>> GetSalesMetricsAsync(string range, int locationId, int whsId);

        /// <summary>
        /// Gets sales metrics for a single branch
        /// </summary>
        Task<GMBranchSalesMetricsViewModel> GetSingleBranchMetricsAsync(string location, DateTime startDate, DateTime endDate, int whsId);

        /// <summary>
        /// Gets the main dashboard metrics (MTD/YTD) for all branches
        /// </summary>
        Task<List<GMBranchSalesMetricsViewModel>> GetDashboardMetricsAsync(int locationId, int whsId);
    }
}