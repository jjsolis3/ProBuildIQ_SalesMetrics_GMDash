// Create this file: Services/SalesMetricsService.cs

using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Caching.Memory;
using SalesMetrics.Controllers;
using SalesMetrics.Models;
using SalesMetrics.Services.Helpers;
using System.Data;

namespace SalesMetrics.Services
{
    public class SalesMetricsService : ISalesMetricsService
    {
        private readonly IConfiguration _configuration;
        private readonly IMemoryCache _cache;
        private readonly ILogger<SalesMetricsService> _logger;

        public SalesMetricsService(
            IConfiguration configuration,
            IMemoryCache cache,
            ILogger<SalesMetricsService> logger)
        {
            _configuration = configuration;
            _cache = cache;
            _logger = logger;
        }

        public async Task<List<GMBranchSalesMetricsViewModel>> GetSalesMetricsAsync(string range, int locationId, int whsId)
        {
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();

            try
            {
                var dateRange = DateRangeHelper.GetRange(range);
                DateTime startDate = dateRange.StartDate;
                DateTime endDate = dateRange.EndDate;
                var cacheKey = $"sales_metrics_{range}_{locationId}_{whsId}_{DateTime.Today:yyyyMMdd}";

                if (_cache.TryGetValue(cacheKey, out List<GMBranchSalesMetricsViewModel> cachedResult))
                {
                    _logger.LogInformation("Cache hit for {CacheKey} in {ElapsedMs}ms",
                        cacheKey, stopwatch.ElapsedMilliseconds);
                    return cachedResult;
                }

                List<GMBranchSalesMetricsViewModel> results;

                if (locationId == 0)
                {
                    results = await GetAllBranchesMetrics(startDate, endDate, whsId);
                }
                else
                {
                    var location = GetLocationString(locationId);
                    var singleResult = await GetSingleBranchMetricsAsync(location, startDate, endDate, whsId);
                    results = new List<GMBranchSalesMetricsViewModel> { singleResult };
                }

                // Cache without size limits to avoid the sizing error
                _cache.Set(cacheKey, results, TimeSpan.FromMinutes(5));

                _logger.LogInformation("Sales metrics retrieved for range {Range}, location {LocationId} in {ElapsedMs}ms",
                    range, locationId, stopwatch.ElapsedMilliseconds);

                return results;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving sales metrics for range {Range}, location {LocationId}",
                    range, locationId);
                throw;
            }
            finally
            {
                stopwatch.Stop();
            }
        }

        public async Task<List<GMBranchSalesMetricsViewModel>> GetDashboardMetricsAsync(int locationId, int whsId)
        {
            var today = DateTime.Today;
            var mtdStart = new DateTime(today.Year, today.Month, 1);
            var ytdStart = new DateTime(today.Year, 1, 1);

            var cacheKey = $"dashboard_metrics_{locationId}_{whsId}_{DateTime.Today:yyyyMMdd}";

            if (_cache.TryGetValue(cacheKey, out List<GMBranchSalesMetricsViewModel> cached) && cached?.Count > 0)
                return cached;

            var locations = LocationHelper.GetLocationQueryList(locationId);

            // compute per-branch dashboard rows (your code already does this)
            var tasks = locations.Select(loc => GetCorrectedDashboardMetricsForBranch(loc, mtdStart, ytdStart, today, whsId));
            var resultList = (await Task.WhenAll(tasks)).ToList();

            // >>> Only cache if we actually have data
            var hasAnyData = resultList.Any(r =>
                r.MTDSales != 0 || r.YTDSales != 0 || r.TotalOrders != 0 || r.TotalOrderAmount != 0);

            if (hasAnyData)
                _cache.Set(cacheKey, resultList, TimeSpan.FromMinutes(10));
            else
                _logger.LogWarning("Dashboard metrics for {LocationId}/{WhsId} were empty; skipping cache set.", locationId, whsId);

            return resultList;
        }

        public async Task<GMBranchSalesMetricsViewModel> GetSingleBranchMetricsAsync(
            string location, DateTime startDate, DateTime endDate, int whsId)
        {
            using var conn = new SqlConnection(_configuration.GetConnectionString(location));

            // Simple retry without complex policy - since you can't modify database
            var maxRetries = 2;
            for (int attempt = 0; attempt <= maxRetries; attempt++)
            {
                try
                {
                    await conn.OpenAsync();
                    return await ExecuteCorrectedSalesQuery(conn, location, startDate, endDate, whsId);
                }
                catch (SqlException ex) when (attempt < maxRetries && IsTransientError(ex))
                {
                    _logger.LogWarning("Database retry attempt {Attempt} for {Location}: {Error}",
                        attempt + 1, location, ex.Message);
                    await Task.Delay(500 * (attempt + 1)); // Simple backoff
                    continue;
                }
            }

            throw new InvalidOperationException($"Failed to connect to database for location {location}");
        }

        private async Task<List<GMBranchSalesMetricsViewModel>> GetAllBranchesMetrics(
            DateTime startDate, DateTime endDate, int whsId)
        {
            var locations = new[] { "LAX", "LSV", "CHN", "PHX", "SND" };

            // Limit concurrent connections to avoid overwhelming the database
            var semaphore = new SemaphoreSlim(3, 3);

            var tasks = locations.Select(async location =>
            {
                await semaphore.WaitAsync();
                try
                {
                    return await GetSingleBranchMetricsAsync(location, startDate, endDate, whsId);
                }
                finally
                {
                    semaphore.Release();
                }
            });

            var results = await Task.WhenAll(tasks);
            return results.ToList();
        }

        /// <summary>
        /// CORRECTED: Uses AR_OPEN_ITEM as primary source with proper JOINs for accurate data
        /// This addresses the data inconsistencies by:
        /// 1. Using IHF_INVOICE_DATE instead of SOH_DELIVERY_DATE for consistent date filtering
        /// 2. Using ARO_INVOICE_AMOUNT as the authoritative amount source
        /// 3. Properly handling ARO_DATE_PAID_IN_FULL as a string column
        /// 4. Using SALES_HEADER only for Online order identification via LEFT JOIN
        /// </summary>
        private async Task<GMBranchSalesMetricsViewModel> ExecuteCorrectedSalesQuery(
            SqlConnection conn, string location, DateTime startDate, DateTime endDate, int whsId)
        {
            var result = new GMBranchSalesMetricsViewModel { Location = location };

            var sql = @"
                SET TRANSACTION ISOLATION LEVEL READ UNCOMMITTED;

                -- Use AR_OPEN_ITEM as primary source for accurate invoice amounts
                SELECT 
                    COUNT(DISTINCT ARO.ARO_INVOICE_NUMBER) AS TotalOrders,
                    
                    -- Online orders: Must join to SALES_HEADER to check SOH_OPERATOR
                    SUM(CASE WHEN S.SOH_OPERATOR = 'Online' THEN 1 ELSE 0 END) AS OnlineOrders,

                    -- Use ARO_INVOICE_AMOUNT as the authoritative amount source
                    CAST(SUM(ARO.ARO_INVOICE_AMOUNT) AS DECIMAL(19,4)) AS TotalOrderAmount,
                    
                    -- Online order amount: Only sum when SOH_OPERATOR = 'Online'
                    CAST(SUM(CASE WHEN S.SOH_OPERATOR = 'Online' 
                             THEN ARO.ARO_INVOICE_AMOUNT 
                             ELSE 0 END) AS DECIMAL(19,4)) AS OnlineOrderAmount,

                    -- Open Orders: Check both NULL and empty string for ARO_DATE_PAID_IN_FULL
                    SUM(CASE WHEN (ARO.ARO_DATE_PAID_IN_FULL IS NULL OR ARO.ARO_DATE_PAID_IN_FULL = '') 
                             AND ARO.ARO_INVOICE_BALANCE_DUE > 0 THEN 1 ELSE 0 END) AS OpenOrders,
                    
                    -- Open Order Amount: Use balance due for unpaid orders
                    CAST(SUM(CASE WHEN (ARO.ARO_DATE_PAID_IN_FULL IS NULL OR ARO.ARO_DATE_PAID_IN_FULL = '') 
                             AND ARO.ARO_INVOICE_BALANCE_DUE > 0
                             THEN ARO.ARO_INVOICE_BALANCE_DUE 
                             ELSE 0 END) AS DECIMAL(19,4)) AS OpenOrderAmount

                FROM AR_OPEN_ITEM ARO WITH (NOLOCK)
                INNER JOIN INVOICE_HEADER IHF WITH (NOLOCK) 
                        ON ARO.ARO_INVOICE_NUMBER = IHF.IHF_INVOICE_NUMBER
                LEFT JOIN SALES_HEADER S WITH (NOLOCK) 
                        ON IHF.IHF_ORDER_NUMBER = S.SOH_NUMBER

                WHERE ARO.ARO_INVOICE_TYPE = 'I'  -- Only invoices, not credits/debits
                  AND IHF.IHF_CANCELED_DATE IS NULL  -- Exclude canceled invoices
                  AND IHF.IHF_INVOICE_DATE BETWEEN @StartDate AND @EndDate  -- Use invoice date for consistency
                  AND ARO.ARO_INVOICE_AMOUNT > 0  -- Exclude zero-amount entries
                  AND (
                      (@WhsId = 0 AND (
                            (@Location = 'LAX' AND ARO.ARO_WHSMAS_ID IN (1,3)) OR
                            (@Location = 'LSV' AND ARO.ARO_WHSMAS_ID IN (1,2,3,6)) OR
                            (@Location = 'PHX' AND ARO.ARO_WHSMAS_ID IN (1,3)) OR
                            (@Location IN ('SND','CHN') AND ARO.ARO_WHSMAS_ID = 1)
                      ))
                      OR (@WhsId <> 0 AND ARO.ARO_WHSMAS_ID = @WhsId)
                  )
                  AND NOT (@Location = 'LAX' AND IHF.IHF_SMNMAS_ORDER = 24);  -- Exclude specific salesman if needed
            ";

            await using var cmd = new SqlCommand(sql, conn) { CommandTimeout = 60 };
            cmd.Parameters.Add("@StartDate", SqlDbType.DateTime).Value = startDate;
            cmd.Parameters.Add("@EndDate", SqlDbType.DateTime).Value = endDate;
            cmd.Parameters.Add("@Location", SqlDbType.VarChar, 3).Value = location;
            cmd.Parameters.Add("@WhsId", SqlDbType.Int).Value = whsId;

            await using var reader = await cmd.ExecuteReaderAsync();
            if (await reader.ReadAsync())
            {
                result.TotalOrders = reader.IsDBNull("TotalOrders") ? 0 : reader.GetInt32("TotalOrders");
                result.OnlineOrders = reader.IsDBNull("OnlineOrders") ? 0 : reader.GetInt32("OnlineOrders");
                result.TotalOrderAmount = reader.IsDBNull("TotalOrderAmount") ? 0 : reader.GetDecimal("TotalOrderAmount");
                result.OnlineOrderAmount = reader.IsDBNull("OnlineOrderAmount") ? 0 : reader.GetDecimal("OnlineOrderAmount");
                result.OpenOrders = reader.IsDBNull("OpenOrders") ? 0 : reader.GetInt32("OpenOrders");
                result.OpenOrderAmount = reader.IsDBNull("OpenOrderAmount") ? 0 : reader.GetDecimal("OpenOrderAmount");
            }

            return result;
        }

        /// <summary>
        /// CORRECTED: Dashboard metrics using consistent AR_OPEN_ITEM approach
        /// This ensures MTD/YTD sales match the KPI bubbles and order summary data is consistent
        /// </summary>
        private async Task<GMBranchSalesMetricsViewModel> GetCorrectedDashboardMetricsForBranch(
            string location, DateTime mtdStart, DateTime ytdStart, DateTime today, int whsId)
        {
            var result = new GMBranchSalesMetricsViewModel { Location = location };

            await using var conn = new SqlConnection(_configuration.GetConnectionString(location));
            await conn.OpenAsync();

            var sql = @"
                SET TRANSACTION ISOLATION LEVEL READ UNCOMMITTED;

                -- Use AR_OPEN_ITEM and INVOICE_HEADER for consistent data
                SELECT
                    -- MTD/YTD Sales from invoiced amounts (matches KPI bubbles)
                    CAST(SUM(CASE WHEN IHF.IHF_INVOICE_DATE BETWEEN @MTDStart AND @Today 
                             THEN ARO.ARO_INVOICE_AMOUNT ELSE 0 END) AS DECIMAL(19,4)) AS MTDSales,
                    CAST(SUM(CASE WHEN IHF.IHF_INVOICE_DATE BETWEEN @YTDStart AND @Today 
                             THEN ARO.ARO_INVOICE_AMOUNT ELSE 0 END) AS DECIMAL(19,4)) AS YTDSales,

                    -- Order counts based on invoice dates (consistent with sales amounts)
                    COUNT(CASE WHEN IHF.IHF_INVOICE_DATE BETWEEN @MTDStart AND @Today THEN 1 END) AS MTDOrders,
                    COUNT(CASE WHEN IHF.IHF_INVOICE_DATE BETWEEN @YTDStart AND @Today THEN 1 END) AS YTDOrders,
                    COUNT(CASE WHEN IHF.IHF_INVOICE_DATE BETWEEN @YTDStart AND @Today THEN 1 END) AS TotalOrders,

                    -- Total order amount (YTD invoiced amount)
                    CAST(SUM(CASE WHEN IHF.IHF_INVOICE_DATE BETWEEN @YTDStart AND @Today 
                             THEN ARO.ARO_INVOICE_AMOUNT ELSE 0 END) AS DECIMAL(19,4)) AS TotalOrderAmount,

                    -- Online orders and amounts (requires join to SALES_HEADER for SOH_OPERATOR)
                    SUM(CASE WHEN S.SOH_OPERATOR = 'Online' 
                         AND IHF.IHF_INVOICE_DATE BETWEEN @YTDStart AND @Today THEN 1 ELSE 0 END) AS OnlineOrders,

                    CAST(SUM(CASE WHEN S.SOH_OPERATOR = 'Online' 
                             AND IHF.IHF_INVOICE_DATE BETWEEN @YTDStart AND @Today 
                             THEN ARO.ARO_INVOICE_AMOUNT ELSE 0 END) AS DECIMAL(19,4)) AS OnlineOrderAmount,

                    -- Open orders: unpaid invoices (string column handling)
                    COUNT(CASE WHEN (ARO.ARO_DATE_PAID_IN_FULL IS NULL OR ARO.ARO_DATE_PAID_IN_FULL = '')
                               AND ARO.ARO_INVOICE_BALANCE_DUE > 0 THEN 1 END) AS OpenOrders,
                    CAST(SUM(CASE WHEN (ARO.ARO_DATE_PAID_IN_FULL IS NULL OR ARO.ARO_DATE_PAID_IN_FULL = '')
                             AND ARO.ARO_INVOICE_BALANCE_DUE > 0
                             THEN ARO.ARO_INVOICE_BALANCE_DUE ELSE 0 END) AS DECIMAL(19,4)) AS OpenOrderAmount

                FROM AR_OPEN_ITEM ARO WITH (NOLOCK)
                INNER JOIN INVOICE_HEADER IHF WITH (NOLOCK) 
                        ON ARO.ARO_INVOICE_NUMBER = IHF.IHF_INVOICE_NUMBER
                LEFT JOIN SALES_HEADER S WITH (NOLOCK) 
                        ON IHF.IHF_ORDER_NUMBER = S.SOH_NUMBER

                WHERE ARO.ARO_INVOICE_TYPE = 'I'
                  AND IHF.IHF_CANCELED_DATE IS NULL
                  AND ARO.ARO_INVOICE_AMOUNT > 0
                  AND (
                       (@WhsId = 0 AND (
                           (@Location = 'LAX' AND ARO.ARO_WHSMAS_ID IN (1,3)) OR
                           (@Location = 'LSV' AND ARO.ARO_WHSMAS_ID IN (1,2,3,6)) OR
                           (@Location = 'PHX' AND ARO.ARO_WHSMAS_ID IN (1,3)) OR
                           (@Location IN ('SND','CHN') AND ARO.ARO_WHSMAS_ID = 1)
                       ))
                       OR (@WhsId <> 0 AND ARO.ARO_WHSMAS_ID = @WhsId)
                  )
                  AND NOT (@Location = 'LAX' AND IHF.IHF_SMNMAS_ORDER = 24);
            ";

            await using var cmd = new SqlCommand(sql, conn) { CommandTimeout = 60 };
            cmd.Parameters.Add("@MTDStart", SqlDbType.DateTime).Value = mtdStart;
            cmd.Parameters.Add("@YTDStart", SqlDbType.DateTime).Value = ytdStart;
            cmd.Parameters.Add("@Today", SqlDbType.DateTime).Value = today;
            cmd.Parameters.Add("@Location", SqlDbType.VarChar, 3).Value = location;
            cmd.Parameters.Add("@WhsId", SqlDbType.Int).Value = whsId;

            await using var reader = await cmd.ExecuteReaderAsync();
            if (await reader.ReadAsync())
            {
                result.MTDSales = reader.IsDBNull("MTDSales") ? 0 : reader.GetDecimal("MTDSales");
                result.YTDSales = reader.IsDBNull("YTDSales") ? 0 : reader.GetDecimal("YTDSales");
                result.MTDOrders = reader.IsDBNull("MTDOrders") ? 0 : reader.GetInt32("MTDOrders");
                result.YTDOrders = reader.IsDBNull("YTDOrders") ? 0 : reader.GetInt32("YTDOrders");
                result.TotalOrders = reader.IsDBNull("TotalOrders") ? 0 : reader.GetInt32("TotalOrders");
                result.OnlineOrders = reader.IsDBNull("OnlineOrders") ? 0 : reader.GetInt32("OnlineOrders");
                result.TotalOrderAmount = reader.IsDBNull("TotalOrderAmount") ? 0 : reader.GetDecimal("TotalOrderAmount");
                result.OnlineOrderAmount = reader.IsDBNull("OnlineOrderAmount") ? 0 : reader.GetDecimal("OnlineOrderAmount");
                result.OpenOrders = reader.IsDBNull("OpenOrders") ? 0 : reader.GetInt32("OpenOrders");
                result.OpenOrderAmount = reader.IsDBNull("OpenOrderAmount") ? 0 : reader.GetDecimal("OpenOrderAmount");
            }

            return result;
        }

        // Helper methods
        private string GetLocationString(int locationId)
        {
            return locationId switch
            {
                1 => "LAX",
                2 => "LSV",
                3 => "CHN",
                4 => "PHX",
                5 => "SND",
                _ => throw new ArgumentException($"Invalid locationId: {locationId}")
            };
        }

        private static bool IsTransientError(SqlException ex)
        {
            var transientErrorNumbers = new[] { 2, 53, 121, 233, 10053, 10054, 10060 };
            return transientErrorNumbers.Contains(ex.Number);
        }
    }
}