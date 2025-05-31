// GMDashController.cs (Optimized)
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using SalesMetrics.Models;
using SalesMetrics.Services.Helpers;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading.Tasks;

namespace SalesMetrics.Controllers
{
    public class GMDashController : Controller
    {
        private readonly IConfiguration _configuration;

        public GMDashController(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        [HttpGet]
        public async Task<IActionResult> Index(int locationId = 0)
        {
            var today = DateTime.Today;
            var mtdStart = new DateTime(today.Year, today.Month, 1);
            var ytdStart = new DateTime(today.Year, 1, 1);
            var locationFullName = Empty;

            var model = new GMFullDashboardViewModel
            {
                MTDStartDateRange = mtdStart,
                YTDStartDateRange = ytdStart,
                EndDateRange = today,
            };

            var locations = LocationHelper.GetLocationQueryList(locationId);
            
            // SALES METRICS - run all in parallel
            var salesTasks = locations.Select(loc => GetBranchSalesMetrics(loc, mtdStart, ytdStart, today));
            var salesResult = await Task.WhenAll(salesTasks);
            model.BranchSalesMetrics = salesResult.ToList();

            // AR DATA - aggregate across location
            var arTasks = locations.Select(loc => GetARDataAsync(loc));
            var arResults = await Task.WhenAll(arTasks);

            // Aggregate the AR data into a single GMARDataViewModel
            model.ARData = new GMARDataViewModel
            {
                DueUnder30 = arResults.Sum(x => x.DueUnder30),
                Due30to60 = arResults.Sum(x => x.Due30to60),
                Due60to90 = arResults.Sum(x => x.Due60to90),
                Due90to120 = arResults.Sum(x => x.Due90to120),
                DueOver120 = arResults.Sum(x => x.DueOver120),
                Location = "ALL"
            };

            ViewBag.SelectedLocationsId = locationId;
            ViewBag.LocationFullName = locationId == 0 ? "All Branches" : LocationHelper.GetLocationName(locationId);

            ViewBag.MTDStartDateRange = mtdStart.ToString("d") + " to " + today.ToString("d");
            ViewBag.YTDStartDateRange = ytdStart.ToString("d") + " to " + today.ToString("d");

            return View("Index", model);
        }

        // CONTROLLER METHOD - GMController.cs or GMDashController.cs
        private async Task<GMBranchSalesMetricsViewModel> GetBranchSalesMetrics(string location, DateTime mtdStart, DateTime ytdStart, DateTime today)
        {
            var result = new GMBranchSalesMetricsViewModel { Location = location };

            using var conn = new SqlConnection(_configuration.GetConnectionString(location));
            await conn.OpenAsync();

            var query = @"
                SELECT
                    -- Sales
                    SUM(CASE WHEN ARO_INVOICE_DATE BETWEEN @MTDStart AND @Today THEN ARO_INVOICE_AMOUNT ELSE 0 END) AS MTDSales,
                    SUM(CASE WHEN ARO_INVOICE_DATE BETWEEN @YTDStart AND @Today THEN ARO_INVOICE_AMOUNT ELSE 0 END) AS YTDSales,

                    -- Orders
                    COUNT(*) AS TotalOrders,
                    SUM(CASE WHEN S.SOH_OPERATOR = 'Online' THEN 1 ELSE 0 END) AS OnlineOrders,

                    -- Order Amounts
                    SUM(CASE WHEN A.ARO_INVOICE_AMOUNT IS NULL THEN S.SOH_TOTAL_AMOUNT ELSE A.ARO_INVOICE_AMOUNT END) AS TotalOrderAmount,
                    SUM(CASE WHEN S.SOH_OPERATOR = 'Online' THEN
                        CASE WHEN A.ARO_INVOICE_AMOUNT IS NULL THEN S.SOH_TOTAL_AMOUNT ELSE A.ARO_INVOICE_AMOUNT END
                    ELSE 0 END) AS OnlineOrderAmount
                FROM SALES_HEADER S
                LEFT JOIN AR_OPEN_ITEM A ON S.SOH_NUMBER = A.ARO_SALES_ORDER_NUMBER
                WHERE S.SOH_DELIVERY_DATE BETWEEN @YTDStart AND @Today
                  AND S.SOH_CANCELED_DATE IS NULL
                  AND S.SOH_WHSMAS_ID IN (1)
                  AND (@Location <> 'LAX' OR S.SOH_SMNMAS_ID <> 24);
            ";

            using var cmd = new SqlCommand(query, conn);
            cmd.Parameters.AddWithValue("@MTDStart", mtdStart);
            cmd.Parameters.AddWithValue("@YTDStart", ytdStart);
            cmd.Parameters.AddWithValue("@Today", today);
            cmd.Parameters.AddWithValue("@Location", location);

            using var reader = await cmd.ExecuteReaderAsync();
            if (await reader.ReadAsync())
            {
                result.MTDSales = reader.IsDBNull(0) ? 0 : Convert.ToDecimal(reader.GetDouble(0));
                result.YTDSales = reader.IsDBNull(1) ? 0 : Convert.ToDecimal(reader.GetDouble(1));
                result.TotalOrders = reader.IsDBNull(2) ? 0 : reader.GetInt32(2);
                result.OnlineOrders = reader.IsDBNull(3) ? 0 : reader.GetInt32(3);
                result.TotalOrderAmount = reader.IsDBNull(4) ? 0 : Convert.ToDecimal(reader.GetDouble(4));
                result.OnlineOrderAmount = reader.IsDBNull(5) ? 0 : Convert.ToDecimal(reader.GetDouble(5));
            }

            return result;
        }


        private async Task<GMARDataViewModel> GetARDataAsync(string location)
        {
            var result = new GMARDataViewModel { Location = location };
            using var conn = new SqlConnection(_configuration.GetConnectionString(location));
            await conn.OpenAsync();

            var cmd = new SqlCommand(@"
                SELECT 
                    SUM(CASE WHEN DATEDIFF(DAY, ARO_DUE_DATE, GETDATE()) < 30 THEN ARO_INVOICE_BALANCE_DUE ELSE 0 END) AS DueUnder30,
                    SUM(CASE WHEN DATEDIFF(DAY, ARO_DUE_DATE, GETDATE()) BETWEEN 30 AND 59 THEN ARO_INVOICE_BALANCE_DUE ELSE 0 END) AS Due30to60,
                    SUM(CASE WHEN DATEDIFF(DAY, ARO_DUE_DATE, GETDATE()) BETWEEN 60 AND 89 THEN ARO_INVOICE_BALANCE_DUE ELSE 0 END) AS Due60to90,
                    SUM(CASE WHEN DATEDIFF(DAY, ARO_DUE_DATE, GETDATE()) BETWEEN 90 AND 120 THEN ARO_INVOICE_BALANCE_DUE ELSE 0 END) AS Due90to120,
                    SUM(CASE WHEN DATEDIFF(DAY, ARO_DUE_DATE, GETDATE()) > 120 THEN ARO_INVOICE_BALANCE_DUE ELSE 0 END) AS DueOver120
                FROM AR_OPEN_ITEM ARO
                LEFT JOIN INVOICE_HEADER IHF ON ARO.ARO_INVOICE_NUMBER = IHF.IHF_INVOICE_NUMBER
                WHERE ARO_INVOICE_BALANCE_DUE > 0
                  AND ARO_DATE_PAID_IN_FULL IS NULL;
            
            ", conn); // Add AR query

            using var reader = await cmd.ExecuteReaderAsync();
            if (await reader.ReadAsync())
            {
                // Fill AR data
                result.DueUnder30 = reader.IsDBNull(0) ? 0 : Convert.ToDecimal(reader.GetDouble(0));
                result.Due30to60 = reader.IsDBNull(1) ? 0 : Convert.ToDecimal(reader.GetDouble(1));
                result.Due60to90 = reader.IsDBNull(2) ? 0 : Convert.ToDecimal(reader.GetDouble(2));
                result.Due90to120 = reader.IsDBNull(3) ? 0 : Convert.ToDecimal(reader.GetDouble(3));
                result.DueOver120 = reader.IsDBNull(4) ? 0 : Convert.ToDecimal(reader.GetDouble(4));
            }

            return result;
        }
              

        private async Task<GMInventoryDataViewModel> GetInventoryDataAsync(string location)
        {
            var result = new GMInventoryDataViewModel { Location = location };
            using var conn = new SqlConnection(_configuration.GetConnectionString(location));
            await conn.OpenAsync();

            var cmd = new SqlCommand(@"SELECT ...", conn); // Add inventory query
            using var reader = await cmd.ExecuteReaderAsync();
            if (await reader.ReadAsync())
            {
                // Fill inventory data
            }

            return result;
        }

        private async Task<GMInstallerMetricsViewModel> GetInstallerMetricsAsync(string location)
        {
            var result = new GMInstallerMetricsViewModel { Location = location };
            using var conn = new SqlConnection(_configuration.GetConnectionString(location));
            await conn.OpenAsync();

            var cmd = new SqlCommand(@"SELECT ...", conn); // Add installer usage query
            using var reader = await cmd.ExecuteReaderAsync();
            if (await reader.ReadAsync())
            {
                // Fill installer metrics
            }

            return result;
        }
    }
}
