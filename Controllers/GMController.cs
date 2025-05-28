// GMController.cs
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using SalesMetrics.Models.ViewModels;
using System.Data;
using System.Security.Claims;
using SalesMetrics.Services.Helpers;
using SalesMetrics.Models;

namespace SalesMetrics.Controllers
{
    //[Authorize(Roles = "GENERAL MANAGER")]
    public class GMController : Controller
    {
        private readonly IConfiguration _configuration;

        public GMController(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        private string LocationFullName(int locationId)
        {
            return locationId switch
            {
                1 => "Los Angeles",
                2 => "Las Vegas",
                3 => "Chino",
                4 => "Phoenix",
                5 => "San Diego",
                _ => "" // fallback to initials if no match
            };
        }

        [HttpGet]
        public IActionResult Index(int locationId = 0)
        {
            ViewBag.SelectedLocationId = locationId;

            if (locationId > 0)
                ViewBag.LocationFullName = LocationFullName(locationId);
                       
            // AR DASH
            var model = GetGMARSummary(locationId);  
            ViewBag.TotalAR = model.TotalAR;
            ViewBag.ARChartData = model.AgingChartData;

            // SALES DATA
            PopulateSalesMetrics(model, locationId);
            ViewBag.MTDSales = string.Format("{0:N2}", model.MTDSales);
            ViewBag.YTDSales = string.Format("{0:N2}", model.YTDSales);

            // OFFICE SALES RANKINGS
            //PopulateSalesRankingPerOffice(model, locationId);
            PopulateSalesRankingByBranch(model);

            // Online Orders
            PopulateOnlineOrderMetrics(model, locationId);


            return View("Index", model);
        }
        // AR DATA CHART DATE EXTRACT
        private GMDashboardViewModel GetGMARSummary(int locationId)
        {
            var summary = new GMDashboardViewModel();
            var locationToQuery = LocationHelper.GetLocationQueryList(locationId);

            foreach (var location in locationToQuery)
            {
                var connectionString = _configuration.GetConnectionString(location);
                using (var conn = new SqlConnection(connectionString))
                {
                    conn.Open();
                    var query = @"
                        SELECT 
                            SUM(CASE WHEN DATEDIFF(DAY, ARO_DUE_DATE, GETDATE()) < 30 THEN A.ARO_INVOICE_BALANCE_DUE ELSE 0 END) AS DueUnder30Amount,
                            SUM(CASE WHEN DATEDIFF(DAY, ARO_DUE_DATE, GETDATE()) BETWEEN 30 AND 59 THEN A.ARO_INVOICE_BALANCE_DUE ELSE 0 END) AS Due30to60Amount,
                            SUM(CASE WHEN DATEDIFF(DAY, ARO_DUE_DATE, GETDATE()) BETWEEN 60 AND 89 THEN A.ARO_INVOICE_BALANCE_DUE ELSE 0 END) AS Due60to90Amount,
                            SUM(CASE WHEN DATEDIFF(DAY, ARO_DUE_DATE, GETDATE()) BETWEEN 90 AND 120 THEN A.ARO_INVOICE_BALANCE_DUE ELSE 0 END) AS Due90to120Amount,
                            SUM(CASE WHEN DATEDIFF(DAY, ARO_DUE_DATE, GETDATE()) > 120 THEN A.ARO_INVOICE_BALANCE_DUE ELSE 0 END) AS DueOver120Amount
                        FROM AR_OPEN_ITEM A
                            LEFT JOIN INVOICE_HEADER I ON A.ARO_INVOICE_NUMBER = I.IHF_INVOICE_NUMBER
                        WHERE A.ARO_INVOICE_BALANCE_DUE > 0 
                            AND A.ARO_DATE_PAID_IN_FULL IS NULL
                    ";

                    using var cmd = new SqlCommand(query, conn);
                    using var reader = cmd.ExecuteReader();

                    if (reader.Read())
                    {
                        summary.DueUnder30Amount += reader.IsDBNull(0) ? 0 : Convert.ToDecimal(reader.GetValue(0));
                        summary.Due30to60Amount += reader.IsDBNull(1) ? 0 : Convert.ToDecimal(reader.GetValue(1));
                        summary.Due60to90Amount += reader.IsDBNull(2) ? 0 : Convert.ToDecimal(reader.GetValue(2));
                        summary.Due90to120Amount += reader.IsDBNull(3) ? 0 : Convert.ToDecimal(reader.GetValue(3));
                        summary.DueOver120Amount += reader.IsDBNull(4) ? 0 : Convert.ToDecimal(reader.GetValue(4));
                    }
                }
            }

            return summary;
        }

        private void PopulateSalesMetrics(GMDashboardViewModel model, int locationId = 0)
        {
            decimal mtdTotal = 0;
            decimal ytdTotal = 0;

            var locationToQuery = LocationHelper.GetLocationQueryList(locationId);

            foreach (var location in locationToQuery)
            {
                var connectionString = _configuration.GetConnectionString(location);

                DateTime today = DateTime.Today;
                DateTime mtdStart = new DateTime(today.Year, today.Month, 1);
                DateTime ytdStart = new DateTime(today.Year, 1, 1);

                using var conn = new SqlConnection(connectionString);
                conn.Open();

                var cmd = new SqlCommand(@"
                    SELECT
                        SUM(CASE WHEN ARO_INVOICE_DATE BETWEEN @MTDStart AND @Today THEN ARO_INVOICE_AMOUNT ELSE 0 END) AS MTDSales,
                        SUM(CASE WHEN ARO_INVOICE_DATE BETWEEN @YTDStart AND @Today THEN ARO_INVOICE_AMOUNT ELSE 0 END) AS YTDSales
                    FROM AR_OPEN_ITEM ARO
                    LEFT JOIN INVOICE_HEADER IHF ON ARO.ARO_INVOICE_NUMBER = IHF.IHF_INVOICE_NUMBER
                    WHERE ARO.ARO_INVOICE_TYPE = 'I'
                        AND ARO.ARO_INVOICE_AMOUNT <> 0
                        AND IHF.IHF_CANCELED_DATE IS NULL
                ", conn);

                cmd.Parameters.AddWithValue("@MTDStart", mtdStart);
                cmd.Parameters.AddWithValue("@YTDStart", ytdStart);
                cmd.Parameters.AddWithValue("@Today", today);

                using var reader = cmd.ExecuteReader();
                if (reader.Read())
                {
                    mtdTotal += reader.IsDBNull(0) ? 0 : Convert.ToDecimal(reader.GetValue(0));
                    ytdTotal += reader.IsDBNull(1) ? 0 : Convert.ToDecimal(reader.GetValue(1));
                }
            }

            model.MTDSales = mtdTotal;
            model.YTDSales = ytdTotal;
            //ViewBag.MTDSales = string.Format("{0:N0}", mtdTotal); // ✅ Format with commas and no decimals
        }

        // SALES RANKING KPI TABLE
        private void PopulateSalesRankingByBranch(GMDashboardViewModel model)
        {
            var locationMap = LocationHelper.GetLocationMap();

            DateTime today = DateTime.Today;
            DateTime mtdStart = new DateTime(today.Year, today.Month, 1);
            DateTime ytdStart = new DateTime(today.Year, 1, 1);

            foreach (var kvp in locationMap)
            {
                string locationName = kvp.Value;
                var connectionString = _configuration.GetConnectionString(locationName);

                using var conn = new SqlConnection(connectionString);
                conn.Open();

                var cmd = new SqlCommand(@"
                    SELECT
                        SUM(CASE WHEN ARO_INVOICE_DATE BETWEEN @MTDStart AND @Today THEN ARO_INVOICE_AMOUNT ELSE 0 END) AS MTDSales,
                        SUM(CASE WHEN ARO_INVOICE_DATE BETWEEN @YTDStart AND @Today THEN ARO_INVOICE_AMOUNT ELSE 0 END) AS YTDSales
                    FROM AR_OPEN_ITEM ARO
                    LEFT JOIN INVOICE_HEADER IHF ON ARO.ARO_INVOICE_NUMBER = IHF.IHF_INVOICE_NUMBER
                    WHERE ARO.ARO_INVOICE_TYPE = 'I'
                        AND ARO.ARO_INVOICE_AMOUNT <> 0
                        AND IHF.IHF_CANCELED_DATE IS NULL;
                ", conn);

                cmd.Parameters.AddWithValue("@MTDStart", mtdStart);
                cmd.Parameters.AddWithValue("@YTDStart", ytdStart);
                cmd.Parameters.AddWithValue("@Today", today);

                using var reader = cmd.ExecuteReader();
                if (reader.Read())
                {
                    model.GMOfficeSalesRankings.Add(new GMOfficeSalesRanking
                    {
                        LocationID = kvp.Key,
                        LocationName = locationName,
                        MTDSales = reader.IsDBNull(0) ? 0 : Convert.ToDecimal(reader.GetValue(0)),
                        YTDSales = reader.IsDBNull(1) ? 0 : Convert.ToDecimal(reader.GetValue(1))
                    });
                }
            }

            ViewBag.MTDStartDateRange = mtdStart.ToString("d") + " to " + today.ToString("d");
            ViewBag.YTDStartDateRange = ytdStart.ToString("d") + " to " + today.ToString("d");
        }
        // SALES ORDER MADE ONLINE KPI BUBBLE
        private void PopulateOnlineOrderMetrics(GMDashboardViewModel model, int locationId = 0)
        {
            var locationToQuery = LocationHelper.GetLocationQueryList(locationId);
            DateTime startDate = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1); // MTD
            DateTime endDate = DateTime.Today;

            foreach (var location in locationToQuery)
            {
                var connectionString = _configuration.GetConnectionString(location);

                using var conn = new SqlConnection(connectionString);
                conn.Open();

                var query = @"
                    SELECT 
                        COUNT(*) AS TotalOrders,
                        SUM(CASE WHEN S.SOH_OPERATOR = 'Online' THEN 1 ELSE 0 END) AS OnlineOrders,
	                    SUM(CASE WHEN A.ARO_INVOICE_AMOUNT IS NULL THEN SOH_TOTAL_AMOUNT ELSE A.ARO_INVOICE_AMOUNT END) as [Total Order Amount],
	                    SUM(CASE WHEN S.SOH_OPERATOR = 'Online' THEN
			                    CASE WHEN A.ARO_INVOICE_AMOUNT IS NULL THEN SOH_TOTAL_AMOUNT ELSE A.ARO_INVOICE_AMOUNT END
		                    ELSE 0 END) as [Total Online Order Amount]

                    FROM SALES_HEADER S
	                    LEFT JOIN AR_OPEN_ITEM as A on S.SOH_NUMBER = A.ARO_SALES_ORDER_NUMBER
                    WHERE S.SOH_DELIVERY_DATE BETWEEN @StartDate AND @EndDate
                        AND S.SOH_CANCELED_DATE IS NULL
                        AND S.SOH_WHSMAS_ID IN (1)
                ";

                if (location == "LAX")
                    query += " AND S.SOH_SMNMAS_ID<> 24";

                var cmd = new SqlCommand(query, conn);

                cmd.Parameters.AddWithValue("@StartDate", startDate);
                cmd.Parameters.AddWithValue("@EndDate", endDate);

                using var reader = cmd.ExecuteReader();
                if (reader.Read())
                {
                    var onlineOrderMetrics = new GMOnlineOrders
                    {
                        LocationID = LocationHelper.GetLocationId(location),
                        LocationName = location,
                        TotalOrders = reader.IsDBNull(0) ? 0 : reader.GetInt32(0),
                        OnlineOrders = reader.IsDBNull(1) ? 0 : reader.GetInt32(1),
                        TotalOrdersAmount = reader.IsDBNull(2) ? 0 : Convert.ToDecimal(reader.GetValue(2)),
                        OnlineOrdersAmount = reader.IsDBNull(3) ? 0 : Convert.ToDecimal(reader.GetValue(3))
                    };

                    model.OnlineOrders.Add(onlineOrderMetrics);
                }
            }
        }

        // *** END OF CLASS ***
    }
}
