using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using SalesMetrics.Models;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using SalesMetrics.Services.Helpers;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using SalesMetrics.Models.EFCore;

namespace SalesMetrics.Controllers
{
    public class SalespersonController : Controller
    {
        private readonly IConfiguration _config;

        public SalespersonController(IConfiguration config)
        {
            _config = config;
        }

        [HttpGet]
        public IActionResult Index(int? selectedSalesmanId, DateTime? startDate, DateTime? endDate)
        {
            var viewModel = new SalesRepProfilePageViewModel()
            {
                // Dropdown list of all sales reps
                SalesReps = GetAllSalesReps(),

                StartDate = startDate ?? DateTime.UtcNow.AddMonths(-1).AddDays(-(DateTime.UtcNow.Day)),
                EndDate = endDate ?? DateTime.UtcNow
            };

            if (selectedSalesmanId.HasValue)
            {
                viewModel.Metrics = GetSalesMetrics(selectedSalesmanId.Value, viewModel.StartDate, viewModel.EndDate);
                viewModel.UserProfile = GetUserProfileBySalesmanId(selectedSalesmanId.Value);
                viewModel.SelectedSalesmanId = selectedSalesmanId.Value;
            }

            return View(viewModel);
        }

        private List<SelectListItem> GetAllSalesReps()
        {
            var roleId = Convert.ToInt32(HttpContext.Session.GetString("RoleId"));
            var locationId = Convert.ToInt32(HttpContext.Session.GetString("LocationId"));

            var list = new List<SelectListItem>();

            string sqlQuery = @"
                SELECT 
                    DISTINCT SalesmanID, 
                    FirstName + ' ' + LastName AS FullName 
                    FROM Users 
                    WHERE RoleID = 2";

            if(roleId == 1 || roleId == 4)
                sqlQuery += " AND Location = @locationId";
            
            sqlQuery += @" ORDER BY FullName";

            using var conn = new SqlConnection(_config.GetConnectionString("SalesMetrics"));
            conn.Open();
            
            var cmd = new SqlCommand(sqlQuery, conn);

            if (roleId == 1 || roleId == 4)
                cmd.Parameters.AddWithValue("@locationId", locationId);

            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                list.Add(new SelectListItem
                {
                    Value = reader["SalesmanID"].ToString(),
                    Text = reader["FullName"].ToString()
                });
            }

            return list;
        }

        private UserProfileViewModel GetUserProfileBySalesmanId(int salesmanId)
        {
            var model = new UserProfileViewModel();

            using var conn = new SqlConnection(_config.GetConnectionString("SalesMetrics"));
            conn.Open();

            var cmd = new SqlCommand(@"
                SELECT FirstName, LastName, Email, GoogleEmail, GoogleAccessToken, GoogleRefreshToken,
                       SalesmanID, SalesmanNumber, Location
                FROM Users
                WHERE SalesmanID = @SalesmanID
            ", conn);
            cmd.Parameters.AddWithValue("@SalesmanID", salesmanId);

            using var reader = cmd.ExecuteReader();
            if (reader.Read())
            {
                model.FirstName = reader["FirstName"]?.ToString();
                model.LastName = reader["LastName"]?.ToString();
                model.Email = reader["Email"]?.ToString();
                model.FullName = $"{model.FirstName} {model.LastName}";
                model.GoogleEmail = reader["GoogleEmail"]?.ToString();
                model.GoogleAccessToken = reader["GoogleAccessToken"]?.ToString();
                model.GoogleRefreshToken = reader["GoogleRefreshToken"]?.ToString();
                model.SalesmanID = reader["SalesmanID"] != DBNull.Value ? Convert.ToInt32(reader["SalesmanID"]) : 0;
                model.SalesmanNumber = reader["SalesmanNumber"]?.ToString();
                model.LocationId = reader["Location"] != DBNull.Value ? Convert.ToInt32(reader["Location"]) : 0;
            }

            return model;
        }


        private SalesRepMetricsViewModel GetSalesMetrics(int salesmanId, DateTime startDate, DateTime endDate)
        {
            var metrics = new SalesRepMetricsViewModel
            {
                PropertyDetails = new List<SalesRepNewAccountSummaryViewModel>()
            };

            // Get salesperson full name for query
            string salesmanName = GetSalespersonFullName(salesmanId);
            if (string.IsNullOrEmpty(salesmanName))
                return metrics;

            string officeLocation = LocationHelper.GetCurrentOfficeCode(HttpContext);
            string connectionString = _config.GetConnectionString(officeLocation);

            using var conn = new SqlConnection(connectionString);
            conn.Open();

            var cmd = new SqlCommand(@"
        WITH NewAccounts AS (
            SELECT
                C.CUM_CUMMAS_ID as PropertyID,
                C.CUM_CUSTOMER_NAME AS Property,
                PC.IPC_DESCRIPTION AS [Mgmt Co],
                SM.SMN_SALESMAN_NAME AS Salesperson,
                SH.SOH_NUMBER AS [Order#],
                IH.IHF_INVOICE_NUMBER AS [Invoice#],
                CAST(C.CUM_ESTABLISHED_DATE AS DATE) AS [Date Created],
                CAST(SH.SOH_DELIVERY_DATE AS DATE) AS [Date Installed],
                ISNULL(SOH_TOTAL_AMOUNT, 0) AS OrderAmount,
                ISNULL(IHF_TOTAL_AMOUNT, 0) AS InvoiceAmount
            FROM CUSTOMER_MASTER C
            LEFT JOIN SALESMAN_MASTER SM ON C.CUM_SMNMAS_ID = SM.SMN_SMNMAS_ID
            LEFT JOIN PRICE_CODES PC ON C.CUM_PRICE_CODE = PC.IPC_PRICE_CODE
            LEFT JOIN SALES_HEADER SH ON C.CUM_CUSTOMER_NUMBER = SH.SOH_CUSTOMER_NUMBER
            LEFT JOIN INVOICE_HEADER IH ON SH.SOH_NUMBER = IH.IHF_ORDER_NUMBER
            WHERE
                C.CUM_ESTABLISHED_DATE >= @startDate
                AND C.CUM_ESTABLISHED_DATE < @endDate
                AND SH.SOH_CANCELED_DATE IS NULL
                AND SH.SOH_WHSMAS_ID = 1
                AND SM.SMN_SALESMAN_NAME = @salesperson
        )
        SELECT
            MAX(PropertyID) as PropertyId,
            Property,
            [Mgmt Co] AS ManagementCompany,
            MAX([Date Created]) as Established,
            COUNT(DISTINCT [Order#]) AS Orders,
            SUM(OrderAmount) AS TotalSalesAmount,
            COUNT(DISTINCT [Invoice#]) AS Invoices,
            SUM(InvoiceAmount) AS TotalInvoiceAmount
        FROM NewAccounts
        GROUP BY Property, [Mgmt Co]
        ORDER BY Property;
    ", conn);

            cmd.Parameters.AddWithValue("@startDate", startDate);
            cmd.Parameters.AddWithValue("@endDate", endDate);
            cmd.Parameters.AddWithValue("@salesperson", salesmanName);

            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                var item = new SalesRepNewAccountSummaryViewModel
                {
                    PropertyId = Convert.ToInt32(reader["PropertyId"]),
                    Property = reader["Property"].ToString(),
                    ManagementCompany = reader["ManagementCompany"].ToString(),
                    EstablishedDate = Convert.ToDateTime(reader["Established"]),
                    Orders = Convert.ToInt32(reader["Orders"]),
                    TotalSalesAmount = Convert.ToDecimal(reader["TotalSalesAmount"]),
                    Invoices = Convert.ToInt32(reader["Invoices"]),
                    TotalInvoiceAmount = Convert.ToDecimal(reader["TotalInvoiceAmount"])
                };
                metrics.PropertyDetails.Add(item);
            }

            // Summary Totals
            metrics.NewAccounts = metrics.PropertyDetails.Count;
            metrics.OrdersCount = metrics.PropertyDetails.Sum(x => x.Orders);
            metrics.InvoicesCount = metrics.PropertyDetails.Sum(x => x.Invoices);
            metrics.TotalSalesAmount = metrics.PropertyDetails.Sum(x => x.TotalSalesAmount);
            metrics.TotalInvoiceAmount = metrics.PropertyDetails.Sum(x => x.TotalInvoiceAmount);
            metrics.WithoutOrders = metrics.PropertyDetails.Count(x => x.Orders == 0);

            return metrics;
        }

        private string GetSalespersonFullName(int salesmanId)
        {
            using var conn = new SqlConnection(_config.GetConnectionString("SalesMetrics"));
            conn.Open();

            var cmd = new SqlCommand("SELECT FirstName + ' ' + LastName AS FullName FROM Users WHERE SalesmanID = @SalesmanID", conn);
            cmd.Parameters.AddWithValue("@SalesmanID", salesmanId);

            return cmd.ExecuteScalar()?.ToString() ?? string.Empty;
        }

        // End Methods Controller
    }
}
