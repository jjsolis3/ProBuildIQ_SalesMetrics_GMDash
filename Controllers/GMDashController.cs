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
using System.Security.Claims;
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

            // New addition:
            model.ARBranchBreakdown = arResults.ToList(); // ✅ Add this line

            // INVENTORY Metrics - across locations
            var inventoryResults = new List<InventoryProductClassSummary>();

            foreach (var location in locations)
            {
                var result = GetInventoryCostByBranch(location, 1); // Warehouse ID 1 for all
                inventoryResults.AddRange(result);
            }

            // Group by ProductClass and merge branch values
            var merged = inventoryResults
                .GroupBy(x => x.ProductClass)
                .Select(group =>
                {
                    var mergedRow = new InventoryProductClassSummary { ProductClass = group.Key };
                    foreach (var item in group)
                    {
                        mergedRow.LAX_InventoryCost += item.LAX_InventoryCost;
                        mergedRow.LSV_InventoryCost += item.LSV_InventoryCost;
                        mergedRow.CHN_InventoryCost += item.CHN_InventoryCost;
                        mergedRow.PHX_InventoryCost += item.PHX_InventoryCost;
                        mergedRow.SND_InventoryCost += item.SND_InventoryCost;
                    }
                    return mergedRow;
                }).ToList();

            model.InventoryByClass = merged;

            // INSTALLER METRICS
            var installerTasks = locations.Select(loc => GetInstallerMetricsForWeek(loc, ytdStart, today));
            var installerResults = await Task.WhenAll(installerTasks);
            model.InstallerCompletionMetrics = installerResults.SelectMany(r => r).ToList();

            // RECENT RTJS
            var rtjTasks = locations.Select(loc => GetRecentRTJEntries(ytdStart, loc));
            var rtjResult = await Task.WhenAll(rtjTasks);
            model.RecentRTJs = rtjResult.SelectMany(r => r).ToList();


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
                result.Due30to60 =  reader.IsDBNull(1) ? 0 : Convert.ToDecimal(reader.GetDouble(1));
                result.Due60to90 =  reader.IsDBNull(2) ? 0 : Convert.ToDecimal(reader.GetDouble(2));
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

            var cmd = new SqlCommand(@"
                SELECT
                
            ", conn); // Add inventory query
            using var reader = await cmd.ExecuteReaderAsync();
            if (await reader.ReadAsync())
            {
                // Fill inventory data
            }

            return result;
        }

        public List<BranchInventorySummary> GetInventorySummaryByBranch()
        {
            var officeLocation = User.FindFirstValue("OfficeLocation");
            var connectionString = _configuration.GetConnectionString(officeLocation);

            var results = new List<BranchInventorySummary>();

            using var conn = new SqlConnection(connectionString);
            conn.Open();

            var cmd = new SqlCommand(@"
                WITH TransactionInfo as (
                    SELECT ITR_ITMMAS_ID_FIRST, ITR_ITMMAS_ID_LAST,
                        ITR_ROLL_LOT, 
                        MAX(ITR_DATETIME) as [Last Activity]

                    FROM ITEM_TRANSACTION as IT 
                    --WHERE IT.ITR_ITMMAS_ID_FIRST = 'LH164'
                    GROUP by ITR_ITMMAS_ID_FIRST, ITR_ITMMAS_ID_LAST, ITR_ROLL_LOT
                )
                SELECT I.ITM_PCLMAS_ID as [ProductClass],
                       W.WHI_ITMMAS_ID_FIRST as [Style],
                       W.WHI_ITMMAS_ID_LAST as [Color],
                       I.ITM_DESCRIPTION + ' ' + I.ITM_DESCRIPTION_2 as [Description],
                       V.VND_VENDOR_NAME as [Vendor],
                       W.WHI_ROLL_LOT as [Roll-Lot],
                       I.ITM_STOCKING_UM as [UOM],
	                   I.ITM_UNIT_PER_CARTON as [Unit/Ctn],
	                   I.ITM_PIECES_PER_CARTON as [PCs/Ctn],

                       I.ITM_ROLL_WIDTH as [RollWidth],

                       -- QTY Available
                       ROUND(CAST(CASE 
                        WHEN I.ITM_PCLMAS_ID = 'VINYLPLANK' THEN 
                            CASE WHEN I.ITM_PIECES_PER_CARTON > 1 then ((W.WHI_QUANTITY_ON_HAND - W.WHI_QUANTITY_ALLOCATED ) * I.ITM_UNIT_PER_CARTON ) / I.ITM_PIECES_PER_CARTON
                                ELSE (W.WHI_QUANTITY_ON_HAND - W.WHI_QUANTITY_ALLOCATED) * I.ITM_UNIT_PER_CARTON
                                END
		                WHEN I.ITM_PCLMAS_ID = 'OTHER' THEN 
			                CASE 
				                WHEN I.ITM_ID_FIRST = 'Prime' THEN ((W.WHI_QUANTITY_ON_HAND - W.WHI_QUANTITY_ALLOCATED) / 6.25) 
				                ELSE W.WHI_QUANTITY_ON_HAND - W.WHI_QUANTITY_ALLOCATED
			                END
                        ELSE
                            CASE
                                -- CARPET TILE
                                WHEN I.ITM_UNIT_PER_CARTON <> 0 THEN (W.WHI_QUANTITY_ON_HAND - W.WHI_QUANTITY_ALLOCATED) * I.ITM_UNIT_PER_CARTON
                                -- ROLL GOODS
                                ELSE (W.WHI_QUANTITY_ON_HAND - W.WHI_QUANTITY_ALLOCATED)
                                END
                        END as FLOAT), 2) as [QtyAvailable],  
		
                       -- Qty On-Hand
                       ROUND(CAST(CASE 
                        WHEN I.ITM_PCLMAS_ID = 'VINYLPLANK' THEN 
                            CASE WHEN I.ITM_PIECES_PER_CARTON > 1 then (W.WHI_QUANTITY_ON_HAND * I.ITM_UNIT_PER_CARTON ) / I.ITM_PIECES_PER_CARTON
                                ELSE W.WHI_QUANTITY_ON_HAND * I.ITM_UNIT_PER_CARTON
                            END
		                WHEN I.ITM_PCLMAS_ID = 'OTHER' THEN 
			                CASE 
				                WHEN I.ITM_ID_FIRST = 'Prime' THEN (W.WHI_QUANTITY_ON_HAND / 6.25) 
				                ELSE W.WHI_QUANTITY_ON_HAND
			                END
                        ELSE 
                            CASE
                                -- CARPET TILE
                                WHEN I.ITM_UNIT_PER_CARTON <> 0 THEN (W.WHI_QUANTITY_ON_HAND) * I.ITM_UNIT_PER_CARTON
                                -- ROLL GOODS
                                ELSE (W.WHI_QUANTITY_ON_HAND)
                                END
                        END as FLOAT), 2) as [QtyOnHand],
	   
                       -- Qty Allocated
                       ROUND(CAST(CASE 
                        WHEN I.ITM_PCLMAS_ID = 'VINYLPLANK' THEN 
                            CASE WHEN I.ITM_PIECES_PER_CARTON > 1 then (W.WHI_QUANTITY_ALLOCATED * I.ITM_UNIT_PER_CARTON ) / I.ITM_PIECES_PER_CARTON
                                ELSE W.WHI_QUANTITY_ALLOCATED * I.ITM_UNIT_PER_CARTON
                                END
		                WHEN I.ITM_PCLMAS_ID = 'OTHER' THEN 
			                CASE 
				                WHEN I.ITM_ID_FIRST = 'Prime' THEN (W.WHI_QUANTITY_ALLOCATED / 6.25) 
				                ELSE W.WHI_QUANTITY_ALLOCATED
			                END
                        ELSE
                            CASE
                                -- CARPET TILE
                                WHEN I.ITM_UNIT_PER_CARTON <> 0 THEN W.WHI_QUANTITY_ALLOCATED * I.ITM_UNIT_PER_CARTON
                                -- ROLL GOODS
                                ELSE W.WHI_QUANTITY_ALLOCATED
                                END
                        END as FlOAT), 2) as [QtyAllocated],

                       ISNULL( DATEDIFF(d, W.WHI_LAST_RECEIPT_DATE, GETDATE()), DATEDIFF(d, T.[Last Activity], GETDATE()) ) as [AgingDays],
                       ISNULL(W.WHI_LAST_RECEIPT_DATE, T.[Last Activity]) as [DateReceived],
       
                       W.WHI_WHSMAS_ID as [WhsID]

                FROM WAREHOUSE_ITEM AS W WITH (NOLOCK)
                    LEFT JOIN ITEM_MASTER AS I WITH (NOLOCK) 
                        ON W.WHI_ITMMAS_ID_FIRST = I.ITM_ID_FIRST AND W.WHI_ITMMAS_ID_LAST = I.ITM_ID_LAST
                    LEFT JOIN VENDOR_MASTER as V WITH (NOLOCK)
                        ON I.ITM_VENDOR_ID = V.VND_VENDOR_NUMBER 
                    LEFT JOIN TransactionInfo as T 
                        ON W.WHI_ITMMAS_ID_FIRST = T.ITR_ITMMAS_ID_FIRST AND W.WHI_ITMMAS_ID_LAST = T.ITR_ITMMAS_ID_LAST AND WHI_ROLL_LOT = T.ITR_ROLL_LOT

                WHERE (W.WHI_QUANTITY_ON_HAND - W.WHI_QUANTITY_ALLOCATED) > 0

                ORDER BY WhsID, ProductClass, Style, Color, [QtyAvailable] desc;
            ", conn);

            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                results.Add(new BranchInventorySummary
                {
                    WarehouseId = reader["WhsID"]?.ToString(),
                    ProductClass = reader["ProductClass"]?.ToString(),
                    Style = reader["Style"]?.ToString(),
                    Color = reader["Color"]?.ToString(),
                    Description = reader["Description"]?.ToString(),
                    Vendor = reader["Vendor"]?.ToString(),
                    QtyAvailable = Convert.ToDecimal(reader["QtyAvailable"]),
                    QtyOnHand = Convert.ToDecimal(reader["QtyOnHand"]),
                    QtyAllocated = Convert.ToDecimal(reader["QtyAllocated"]),
                    AgingDays = Convert.ToInt32(reader["AgingDays"]),
                    DateReceived = Convert.ToDateTime(reader["DateReceived"]),
                });
            }

            return results;
        }

        public List<InventoryProductClassSummary> GetInventoryCostByBranch(string location, int warehouseId)
        {
            var summaryDict = new Dictionary<string, InventoryProductClassSummary>();

            using var conn = new SqlConnection(_configuration.GetConnectionString(location));
            conn.Open();

            var cmd = new SqlCommand(@"
                SELECT 
                    I.ITM_PCLMAS_ID AS ProductClass,
                    ROUND(SUM(
                        CASE 
                            WHEN I.ITM_PCLMAS_ID = 'VINYLPLANK' THEN 
                                CASE WHEN I.ITM_PIECES_PER_CARTON > 1 THEN 
                                    ((W.WHI_QUANTITY_ON_HAND - W.WHI_QUANTITY_ALLOCATED) * I.ITM_UNIT_PER_CARTON) / I.ITM_PIECES_PER_CARTON
                                ELSE 
                                    (W.WHI_QUANTITY_ON_HAND - W.WHI_QUANTITY_ALLOCATED) * I.ITM_UNIT_PER_CARTON
                                END
                            WHEN I.ITM_PCLMAS_ID = 'OTHER' THEN 
                                CASE WHEN I.ITM_ID_FIRST = 'Prime' THEN 
                                    (W.WHI_QUANTITY_ON_HAND - W.WHI_QUANTITY_ALLOCATED) / 6.25
                                ELSE 
                                    W.WHI_QUANTITY_ON_HAND - W.WHI_QUANTITY_ALLOCATED
                                END
                            ELSE
                                CASE WHEN I.ITM_UNIT_PER_CARTON <> 0 THEN 
                                    (W.WHI_QUANTITY_ON_HAND - W.WHI_QUANTITY_ALLOCATED) * I.ITM_UNIT_PER_CARTON
                                ELSE 
                                    W.WHI_QUANTITY_ON_HAND - W.WHI_QUANTITY_ALLOCATED
                                END
                        END * I.ITM_VENDOR_COST
                    ), 4) AS InventoryCost
                FROM WAREHOUSE_ITEM W
                    LEFT JOIN ITEM_MASTER I ON W.WHI_ITMMAS_ID_FIRST = I.ITM_ID_FIRST AND W.WHI_ITMMAS_ID_LAST = I.ITM_ID_LAST
                    LEFT JOIN VENDOR_MASTER V ON I.ITM_VENDOR_ID = V.VND_VENDOR_NUMBER
                WHERE (W.WHI_QUANTITY_ON_HAND - W.WHI_QUANTITY_ALLOCATED) > 0
                    AND W.WHI_WHSMAS_ID = @WhsId
                GROUP BY I.ITM_PCLMAS_ID
                ORDER BY I.ITM_PCLMAS_ID;
            ", conn);

            cmd.Parameters.AddWithValue("@WhsId", warehouseId);

            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                var productClass = reader["ProductClass"].ToString();
                var cost = Convert.ToDecimal(reader["InventoryCost"]);

                if (!summaryDict.ContainsKey(productClass))
                {
                    summaryDict[productClass] = new InventoryProductClassSummary
                    {
                        ProductClass = productClass
                    };
                }

                // Assign cost to correct branch property
                switch (location)
                {
                    case "LAX": summaryDict[productClass].LAX_InventoryCost = cost; break;
                    case "LSV": summaryDict[productClass].LSV_InventoryCost = cost; break;
                    case "CHN": summaryDict[productClass].CHN_InventoryCost = cost; break;
                    case "PHX": summaryDict[productClass].PHX_InventoryCost = cost; break;
                    case "SND": summaryDict[productClass].SND_InventoryCost = cost; break;
                }
            }

            return summaryDict.Values.ToList();

        }

        private async Task<List<InstallerCompletionMetric>> GetInstallerMetricsForWeek(string location, DateTime startDate, DateTime endDate )
        {
            var metrics = new List<InstallerCompletionMetric>();

            using var conn = new SqlConnection(_configuration.GetConnectionString(location));
            await conn.OpenAsync();

            var cmd = new SqlCommand(@"
                SELECT 
                    [WOS].[ARRIVAL_TIME], 
                    [WOS].[DEPART_TIME], 
                    [LRS].[ARRIVE_TIME] AS [OrderArrival], 
                    [WOS].[CANCEL_TIME], 
                    [WOS].[COMPLETE_TIME]
                FROM [TEMP_INSTALLER_DETAILS] TID
                INNER JOIN [SALES_HEADER] SOH ON TID.ORDER_ID = SOH.SOH_NUMBER
                LEFT JOIN [WORK_ORDER_STATUS] WOS ON SOH.SOH_NUMBER = WOS.SOH_NUMBER
                LEFT JOIN [LOAD_RETURN_STATUS] LRS ON SOH.SOH_NUMBER = LRS.SOH_NUMBER
                WHERE TID.INSTALL_DATE >= @startDate AND TID.INSTALL_DATE < @endDate
            ", conn);
            cmd.Parameters.AddWithValue("@startDate", startDate);
            cmd.Parameters.AddWithValue("@endDate", endDate);

            var totalOrders = 0;
            var completedOrders = 0;
            var whsArrival = 0;
            var whsDepart = 0;
            var orderArrival = 0;
            var orderCompleted = 0;
            var cancelled = 0;

            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                totalOrders++;

                var hasCancel = !reader.IsDBNull(3);
                var hasAll4 = !reader.IsDBNull(0) && !reader.IsDBNull(1) && !reader.IsDBNull(2) && !reader.IsDBNull(4);

                if (hasCancel || hasAll4)
                    completedOrders++;

                if (!reader.IsDBNull(0)) whsArrival++;
                if (!reader.IsDBNull(1)) whsDepart++;
                if (!reader.IsDBNull(2)) orderArrival++;
                if (!reader.IsDBNull(4)) orderCompleted++;
                if (hasCancel) cancelled++;
            }

            metrics.Add(new InstallerCompletionMetric
            {
                Location = location,
                TotalOrders = totalOrders,
                CompletedOrders = completedOrders,
                WhsArrivalCount = whsArrival,
                WhsDepartCount = whsDepart,
                OrderArrivalCount = orderArrival,
                OrderCompletedCount = orderCompleted,
                OrderCancelledCount = cancelled
            });

            return metrics;
        }

        private async Task<List<RTJEntry>> GetRecentRTJEntries(DateTime startDate, string location)
        {
            var result = new List<RTJEntry>();
            using var conn = new SqlConnection(_configuration.GetConnectionString(location));
            await conn.OpenAsync();

            var cmd = new SqlCommand(@"
                SELECT 
                    G.GLJ_REFERENCE_NUMBER as [AdjustmentComments], 
                    G.GLJ_JOURNAL_NUMBER as [JournalNumber], 
                    G.GLJ_CREDIT_AMOUNT as [Credit], 
                    G.GLJ_DEBIT_AMOUNT as [Debit], 
                    CAST(G.GLJ_TRANSACTION_DATE as DATE) as [Date],
                    G.GLJ_WAREHOUSE_NUMBER as [WarehouseNumber]
                FROM GL_JOURNAL G
                WHERE G.GLJ_ACCOUNT_NUMBER = 12970 
                    AND G.GLJ_TRANSACTION_DATE >= @StartDate
                    AND G.GLJ_WAREHOUSE_NUMBER IN (1, 2, 3, 6, 11, 86, 90)
                    AND G.GLJ_REFERENCE_NUMBER LIKE '%RTJ%'
                ORDER BY G.GLJ_DEBIT_AMOUNT DESC, G.GLJ_CREDIT_AMOUNT ASC
            ", conn);

            cmd.Parameters.AddWithValue("@StartDate", startDate);

            using var reader = await cmd.ExecuteReaderAsync();
            Console.WriteLine($"{location}: Reader opened");
            

            while (await reader.ReadAsync())
            {
                result.Add(new RTJEntry
                {
                    AdjustmentComments = reader["AdjustmentComments"].ToString(),
                    JournalNumber = reader["JournalNumber"].ToString(),
                    Credit = Convert.ToDecimal(reader["Credit"]),
                    Debit = Convert.ToDecimal(reader["Debit"]),
                    Date = Convert.ToDateTime(reader["Date"]),
                    WarehouseNumber = Convert.ToInt32(reader["WarehouseNumber"]),
                    Location = location // ✅ NEW
                });
            }
            Console.WriteLine($"{location}: Reader read complete. Total: {result.Count}");
            return result;
        }

    }
}
