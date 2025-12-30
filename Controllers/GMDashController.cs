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
            var locationFullName = string.Empty;

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
            ViewBag.SalesRange = "ytd";

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

            // ADD DATE RANGE LABELS
            var salesRange = "ytd"; // or "ytd", whatever default you want
            var installerRange = "thisweek";
            var rtjRange = "mtd";

            var (salesStart, salesEnd) = DateRangeHelper.GetRange(salesRange);
            var (installerStart, installerEnd) = DateRangeHelper.GetRange(installerRange);
            var (rtjStart, rtjEnd) = DateRangeHelper.GetRange(rtjRange);

            ViewBag.salesRangeLabel = $"{salesStart.ToString("M/d/yyyy")} to {salesEnd.ToString("M/d/yyyy")}";
            ViewBag.installerRangeLabel = $"{installerStart.ToString("M/d/yyyy")} to {installerEnd.ToString("M/d/yyyy")}";
            ViewBag.rtjRangeLabel = $"{rtjStart.ToString("M/d/yyyy")} to {rtjEnd.ToString("M/d/yyyy")}";

            // INSTALLER METRICS
            var installerTasks = locations.Select(loc => GetInstallerMetricsForWeek(loc, installerStart, installerEnd));
            var installerResults = await Task.WhenAll(installerTasks);
            model.InstallerCompletionMetrics = installerResults.SelectMany(r => r).ToList();
            ViewBag.InstallerRange = "thisweek";

            // RECENT RTJS
            var rtjTasks = locations.Select(loc => GetRecentRTJEntries(rtjStart, rtjEnd, loc));
            var rtjResult = await Task.WhenAll(rtjTasks);
            model.RecentRTJs = rtjResult.SelectMany(r => r).ToList();
            ViewBag.RTJRange = "mtd";

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

            // ==========================================================================
            // CORRECTED QUERY - Uses AR_OPEN_ITEM as the authoritative data source
            // ==========================================================================
            // KEY CHANGES:
            // 1. AR_OPEN_ITEM is now the primary table (not SALES_HEADER)
            // 2. Uses IHF_INVOICE_DATE for date filtering (consistent with KPI bubbles)
            // 3. Uses ARO_INVOICE_AMOUNT for all monetary values (actual invoiced amounts)
            // 4. Properly handles ARO_DATE_PAID_IN_FULL as a string column
            // 5. LEFT JOINs to SALES_HEADER only for SOH_OPERATOR (online detection)
            // ==========================================================================
            var query = @"
                SELECT
                    -- Sales Totals (using ARO_INVOICE_DATE for consistency with KPI)
                    SUM(CASE WHEN IHF.IHF_INVOICE_DATE BETWEEN @MTDStart AND @Today 
                             THEN ARO.ARO_INVOICE_AMOUNT ELSE 0 END) AS MTDSales,
                    SUM(CASE WHEN IHF.IHF_INVOICE_DATE BETWEEN @YTDStart AND @Today 
                             THEN ARO.ARO_INVOICE_AMOUNT ELSE 0 END) AS YTDSales,

                    -- Order Counts (using IHF_INVOICE_DATE for consistency)
                    COUNT(CASE WHEN IHF.IHF_INVOICE_DATE BETWEEN @MTDStart AND @Today 
                               THEN 1 ELSE NULL END) AS MTDOrders,
                    COUNT(CASE WHEN IHF.IHF_INVOICE_DATE BETWEEN @YTDStart AND @Today 
                               THEN 1 ELSE NULL END) AS YTDOrders,

                    -- Total Orders in date range
                    COUNT(*) AS TotalOrders,

                    -- Online Orders (using SOH_OPERATOR from SALES_HEADER)
                    SUM(CASE WHEN S.SOH_OPERATOR = 'Online' THEN 1 ELSE 0 END) AS OnlineOrders,

                    -- Total Order Amount (from AR - the authoritative source)
                    SUM(ARO.ARO_INVOICE_AMOUNT) AS TotalOrderAmount,

                    -- Online Order Amount
                    SUM(CASE WHEN S.SOH_OPERATOR = 'Online' 
                             THEN ARO.ARO_INVOICE_AMOUNT ELSE 0 END) AS OnlineOrderAmount,

                    -- Open Orders Count (properly handles ARO_DATE_PAID_IN_FULL as string)
                    SUM(CASE WHEN (ARO.ARO_DATE_PAID_IN_FULL IS NULL OR ARO.ARO_DATE_PAID_IN_FULL = '') 
                              AND ARO.ARO_INVOICE_BALANCE_DUE > 0 
                             THEN 1 ELSE 0 END) AS OpenOrders,

                    -- Open Order Amount (balance due on unpaid invoices)
                    SUM(CASE WHEN (ARO.ARO_DATE_PAID_IN_FULL IS NULL OR ARO.ARO_DATE_PAID_IN_FULL = '') 
                              AND ARO.ARO_INVOICE_BALANCE_DUE > 0 
                             THEN ARO.ARO_INVOICE_BALANCE_DUE ELSE 0 END) AS OpenOrderAmount

                FROM AR_OPEN_ITEM ARO WITH (NOLOCK)
                INNER JOIN INVOICE_HEADER IHF WITH (NOLOCK) 
                    ON ARO.ARO_INVOICE_NUMBER = IHF.IHF_INVOICE_NUMBER
                LEFT JOIN SALES_HEADER S WITH (NOLOCK) 
                    ON IHF.IHF_ORDER_NUMBER = S.SOH_NUMBER
                WHERE IHF.IHF_INVOICE_DATE BETWEEN @YTDStart AND @Today
                  AND IHF.IHF_WHSMAS_ID IN (1)
                  AND (@Location <> 'LAX' OR ISNULL(S.SOH_SMNMAS_ID, 0) <> 24);
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
                result.MTDOrders = reader.IsDBNull(2) ? 0 : reader.GetInt32(2);
                result.YTDOrders = reader.IsDBNull(3) ? 0 : reader.GetInt32(3);
                result.TotalOrders = reader.IsDBNull(4) ? 0 : reader.GetInt32(4);
                result.OnlineOrders = reader.IsDBNull(5) ? 0 : reader.GetInt32(5);
                result.TotalOrderAmount = reader.IsDBNull(6) ? 0 : Convert.ToDecimal(reader.GetDouble(6));
                result.OnlineOrderAmount = reader.IsDBNull(7) ? 0 : Convert.ToDecimal(reader.GetDouble(7));
                result.OpenOrders = reader.IsDBNull(8) ? 0 : reader.GetInt32(8);
                result.OpenOrderAmount = reader.IsDBNull(9) ? 0 : Convert.ToDecimal(reader.GetDouble(9));
            }

            return result;
        }

        [HttpGet]
        public async Task<IActionResult> GetSalesSummaryTable(string range = "ytd", int locationId = 0)
        {
            var (start, end) = DateRangeHelper.GetRange(range);
            var locations = LocationHelper.GetLocationQueryList(locationId);

            var salesTasks = locations.Select(loc => GetBranchSalesMetrics(loc, start, start, end));
            var results = await Task.WhenAll(salesTasks);

            ViewBag.salesRangeLabel = $"{start.ToString("M/d/yyyy")} to {end.ToString("M/d/yyyy")}";

            return PartialView("_SalesSummaryPartial", results.ToList());
        }


        private async Task<GMARDataViewModel> GetARDataAsync(string location)
        {
            var result = new GMARDataViewModel { Location = location };
            using var conn = new SqlConnection(_configuration.GetConnectionString(location));
            await conn.OpenAsync();

            var cmd = new SqlCommand(@"
                SELECT 
                    SUM(CASE WHEN DATEDIFF(DAY, ARO_DUE_DATE, GETDATE()) < 30 
                             THEN ARO_INVOICE_BALANCE_DUE ELSE 0 END) AS DueUnder30,
                    SUM(CASE WHEN DATEDIFF(DAY, ARO_DUE_DATE, GETDATE()) BETWEEN 30 AND 59 
                             THEN ARO_INVOICE_BALANCE_DUE ELSE 0 END) AS Due30to60,
                    SUM(CASE WHEN DATEDIFF(DAY, ARO_DUE_DATE, GETDATE()) BETWEEN 60 AND 89 
                             THEN ARO_INVOICE_BALANCE_DUE ELSE 0 END) AS Due60to90,
                    SUM(CASE WHEN DATEDIFF(DAY, ARO_DUE_DATE, GETDATE()) BETWEEN 90 AND 120 
                             THEN ARO_INVOICE_BALANCE_DUE ELSE 0 END) AS Due90to120,
                    SUM(CASE WHEN DATEDIFF(DAY, ARO_DUE_DATE, GETDATE()) > 120 
                             THEN ARO_INVOICE_BALANCE_DUE ELSE 0 END) AS DueOver120
                FROM AR_OPEN_ITEM ARO WITH (NOLOCK)
                WHERE ARO_INVOICE_BALANCE_DUE > 0
                  AND (ARO_DATE_PAID_IN_FULL IS NULL OR ARO_DATE_PAID_IN_FULL = '');
            ", conn);

            using var reader = await cmd.ExecuteReaderAsync();
            if (await reader.ReadAsync())
            {
                result.DueUnder30 = reader.IsDBNull(0) ? 0 : Convert.ToDecimal(reader.GetDouble(0));
                result.Due30to60 = reader.IsDBNull(1) ? 0 : Convert.ToDecimal(reader.GetDouble(1));
                result.Due60to90 = reader.IsDBNull(2) ? 0 : Convert.ToDecimal(reader.GetDouble(2));
                result.Due90to120 = reader.IsDBNull(3) ? 0 : Convert.ToDecimal(reader.GetDouble(3));
                result.DueOver120 = reader.IsDBNull(4) ? 0 : Convert.ToDecimal(reader.GetDouble(4));
            }

            return result;
        }

        [HttpGet]
        public IActionResult GetInventoryModal(string office)
        {
            var items = GetInventorySummaryByBranch(office);
            return PartialView("_InventoryModalPartial", items);
        }

        public List<BranchInventorySummary> GetInventorySummaryByBranch(string officeLocation)
        {
            //var officeLocation = User.FindFirstValue("OfficeLocation");
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
                    OfficeBranch = officeLocation,
                    WarehouseId = reader["WhsID"]?.ToString(),
                    ProductClass = reader["ProductClass"]?.ToString(),
                    Style = reader["Style"]?.ToString(),
                    Color = reader["Color"]?.ToString(),
                    Description = reader["Description"]?.ToString(),
                    Vendor = reader["Vendor"]?.ToString(),
                    RollLot = reader["Roll-Lot"]?.ToString(),
                    UOM = reader["UOM"]?.ToString(),
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

        // INSTALLER SECTION
        private async Task<List<InstallerCompletionMetric>> GetInstallerMetricsForWeek(string location, DateTime startDate, DateTime endDate)
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
                WHERE TID.INSTALL_DATE BETWEEN @startDate AND @endDate
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

        [HttpGet]
        public async Task<IActionResult> GetInstallerTable(string range = "thisweek", int locationId = 0)
        {
            var (startDate, endDate) = DateRangeHelper.GetRange(range);

            var locations = LocationHelper.GetLocationQueryList(locationId);
            var installerTasks = locations.Select(loc => GetInstallerMetricsForWeek(loc, startDate, endDate));
            var installerResults = await Task.WhenAll(installerTasks);
            var metrics = installerResults.SelectMany(x => x).ToList();

            ViewBag.installerRangeLabel = $"{startDate.ToString("M/d/yyyy")} to {endDate.ToString("M/d/yyyy")}";

            return PartialView("_InstallerTablePartial", metrics);
        }

        [HttpGet]
        public async Task<IActionResult> GetInstallerDetails(string location, string range = "thisweek")
        {
            var (startDate, endDate) = DateRangeHelper.GetRange(range);

            var data = await GetInstallerDetailEntries(startDate, endDate, location);
            return PartialView("_InstallerModalPartial", data);
        }

        private async Task<List<InstallerDetails>> GetInstallerDetailEntries(DateTime startDate, DateTime endDate, string location)
        {
            var results = new List<InstallerDetails>();

            var connectionString = _configuration.GetConnectionString(location);
            if (string.IsNullOrEmpty(connectionString))
            {
                throw new InvalidOperationException($"Connection string for location '{location}' is missing.");
            }

            using var conn = new SqlConnection(connectionString);
            await conn.OpenAsync();

            var locationPoint = location switch
            {
                "LAX" => "geography::Point(34.28298, -118.86938, 4326)",
                "LSV" => "geography::Point(36.08918, -115.21183, 4326)",
                "CHN" => "geography::Point(34.00234, -117.67519, 4326)",
                "SND" => "geography::Point(32.89706, -117.13132, 4326)",
                "PHX" => "geography::Point(33.40868, -111.88153, 4326)",
                _ => null
            };

            if (locationPoint == null) return results;

            var sql = $@"
                DECLARE @officeLocation geography = {locationPoint};
                SELECT 
                    SOH.SOH_SHIP_TO_NAME as [PROPERTY],
                    TID.ORDER_ID as [ORDERNUMBER],
                    CONVERT(varchar(10),TID.INSTALL_DATE, 101) as [ORDERDATE],
                    CONVERT(varchar(8), LRS.ARRIVE_TIME, 108) as [WHS_ARRIVE],
                    CONVERT(varchar(8), LRS.RECEIVE_TIME, 108) as [MATERIAL_CONFIRM],
	                DATEDIFF(HOUR, LRS.ARRIVE_TIME, LRS.RECEIVE_TIME) [WHSLOADING],
                    CASE 
		                WHEN (DATEDIFF(MINUTE, LRS.ARRIVE_TIME, LRS.RECEIVE_TIME) / 60) > 0 THEN 
			                CAST((DATEDIFF(MINUTE, LRS.ARRIVE_TIME, LRS.RECEIVE_TIME) / 60) AS NVARCHAR) + ' hours ' + CAST((DATEDIFF(MINUTE, LRS.ARRIVE_TIME, LRS.RECEIVE_TIME) % 60) AS NVARCHAR) + ' minutes' 
		                ELSE 
			                CAST((DATEDIFF(MINUTE, LRS.ARRIVE_TIME, LRS.RECEIVE_TIME) % 60) AS NVARCHAR) + ' minutes'
		                END as [WHS_LOADING],
                    WOS.ARRIVE_LOC as [JOB_ARRIVE_LOCATION],
                    CONVERT(varchar(8), WOS.ARRIVAL_TIME, 108) as [JOB_ARRIVE],
                    CASE 
                        WHEN WOS.Latitude IS NOT NULL AND WOS.Longitude IS NOT NULL THEN
                            CONCAT(CAST((@officeLocation.STDistance(geography::Point(WOS.Latitude, WOS.Longitude, 4326)) * 3.28084) as INT) / 5280 , ' miles, ', CAST((@officeLocation.STDistance(geography::Point(WOS.Latitude, WOS.Longitude, 4326)) * 3.28084) as INT) % 5280 , ' feet')
                        ELSE NULL 
		                END AS [JOB_DISTANCE_FROM_WHS],
                    CASE 
                        WHEN LRS.RECEIVE_TIME < WOS.ARRIVAL_TIME THEN
			                CASE 
				                WHEN (DATEDIFF(MINUTE, LRS.RECEIVE_TIME, WOS.ARRIVAL_TIME) / 60) > 0 THEN 
					                CAST((DATEDIFF(MINUTE, LRS.RECEIVE_TIME, WOS.ARRIVAL_TIME) / 60) AS NVARCHAR) + ' hours ' + CAST((DATEDIFF(MINUTE, LRS.RECEIVE_TIME, WOS.ARRIVAL_TIME) % 60) AS NVARCHAR) + ' minutes'
				                ELSE
					                CAST((DATEDIFF(MINUTE, LRS.RECEIVE_TIME, WOS.ARRIVAL_TIME) % 60) AS NVARCHAR) + ' minutes'
				                END
                        ELSE 
			                CASE 
				                WHEN (DATEDIFF(MINUTE, LRS.ARRIVE_TIME, WOS.ARRIVAL_TIME) / 60) > 0 THEN
					                CAST((DATEDIFF(MINUTE, LRS.ARRIVE_TIME, WOS.ARRIVAL_TIME) / 60) AS NVARCHAR) + ' hours ' + CAST((DATEDIFF(MINUTE, LRS.ARRIVE_TIME, WOS.ARRIVAL_TIME) % 60) AS NVARCHAR) + ' minutes'
				                ELSE 
					                CAST((DATEDIFF(MINUTE, LRS.ARRIVE_TIME, WOS.ARRIVAL_TIME) % 60) AS NVARCHAR) + ' minutes'
				                END
		                END AS [TRAVEL_TIME],
                    CONVERT(varchar(8), WOS.COMPLETE_TIME, 108) as [JOB_COMPLETED],
                    CONVERT(varchar(8), WOS.DEPART_TIME, 108) as [JOB_DEPARTED],
                    CASE
		                WHEN (DATEDIFF(MINUTE, WOS.ARRIVAL_TIME, WOS.DEPART_TIME) / 60) >  0 THEN 
			                CAST((DATEDIFF(MINUTE, WOS.ARRIVAL_TIME, WOS.DEPART_TIME) / 60) AS VARCHAR) + ' hours ' + CAST((DATEDIFF(MINUTE, WOS.ARRIVAL_TIME, WOS.DEPART_TIME) % 60) AS VARCHAR) + ' minutes'
		                ELSE	
			                CAST((DATEDIFF(MINUTE, WOS.ARRIVAL_TIME, WOS.DEPART_TIME) % 60) AS VARCHAR) + ' minutes'
		                END as [JOB_DURATION],
                    CASE 
                        WHEN WOS.COMPLETE_LATITUDE IS NOT NULL AND WOS.COMPLETE_LONGITUDE IS NOT NULL THEN
                            CONCAT(CAST((@officeLocation.STDistance(geography::Point(WOS.COMPLETE_LATITUDE, WOS.COMPLETE_LONGITUDE, 4326)) * 3.28084) as INT) / 5280 , ' miles, ', CAST((@officeLocation.STDistance(geography::Point(WOS.COMPLETE_LATITUDE, WOS.COMPLETE_LONGITUDE, 4326)) * 3.28084) as INT) % 5280 , ' feet')
                        ELSE NULL 
		                END AS [JOB_COMPLETED_FROM_WHS],
                    WOS.COMPLETE_LOCATION as [JOB_COMPLETED_LOCATION],
                    CASE WHEN DATEDIFF(MINUTE, WOS.ARRIVAL_TIME, WOS.DEPART_TIME) < 33 THEN 'ERROR' ELSE '' END AS [UsageCheck],
                    CONVERT(varchar(8), WOS.CANCEL_TIME, 108) as [JOB_CANCELED],
                    TID.INSTALLER_NAME as [INSTALLER],
                    TID.INSTALLER_CODE as [INSTALLERID],
                    WOS.INSTALLER_ID as [ORDER_INSTALLERID]

                FROM TEMP_INSTALLER_DETAILS TID
                    INNER JOIN SALES_HEADER SOH on TID.ORDER_ID = SOH.SOH_NUMBER
                    LEFT JOIN WORK_ORDER_STATUS WOS on SOH.SOH_NUMBER = WOS.SOH_NUMBER
                    LEFT JOIN LOAD_RETURN_STATUS LRS on SOH.SOH_NUMBER = LRS.SOH_NUMBER

                WHERE TID.INSTALL_DATE BETWEEN @FromDate AND @ToDate

                ORDER BY [Installer] ASC, [JOB_ARRIVE] ASC
            ";

            using var cmd = new SqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@FromDate", startDate);
            cmd.Parameters.AddWithValue("@ToDate", endDate);

            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                results.Add(new InstallerDetails
                {
                    Property = reader["PROPERTY"]?.ToString(),
                    OrderId = Convert.ToInt32(reader["ORDERNUMBER"]),
                    OrderDate = DateTime.TryParse(reader["ORDERDATE"]?.ToString(), out var orderDate) ? orderDate : (DateTime?)null,
                    WhsArrive = DateTime.TryParse(reader["WHS_ARRIVE"]?.ToString(), out var wa) ? wa : (DateTime?)null,
                    MaterialConfirm = DateTime.TryParse(reader["MATERIAL_CONFIRM"]?.ToString(), out var mc) ? mc : (DateTime?)null,
                    WhsLoading = reader["WHS_LOADING"]?.ToString(),
                    JobLocation = reader["JOB_ARRIVE_LOCATION"]?.ToString(),
                    JobArrival = DateTime.TryParse(reader["JOB_ARRIVE"]?.ToString(), out var ja) ? ja : (DateTime?)null,
                    DistanceFromWhs = reader["JOB_DISTANCE_FROM_WHS"]?.ToString(),
                    TravelTime = reader["TRAVEL_TIME"]?.ToString(),
                    JobCompleted = DateTime.TryParse(reader["JOB_COMPLETED"]?.ToString(), out var jc) ? jc : (DateTime?)null,
                    JobDepart = DateTime.TryParse(reader["JOB_DEPARTED"]?.ToString(), out var jd) ? jd : (DateTime?)null,
                    JobDuration = reader["JOB_DURATION"]?.ToString(),
                    JobCompleteLocation = reader["JOB_COMPLETED_LOCATION"]?.ToString(),
                    JobCancelled = DateTime.TryParse(reader["JOB_CANCELED"]?.ToString(), out var cancel) ? cancel : (DateTime?)null,
                    Installer = reader["INSTALLER"]?.ToString(),
                    InstallerId = int.TryParse(reader["INSTALLERID"]?.ToString(), out var insId) ? insId : 0
                });

            }

            return results;
        }
        // END INSTALLER SECTION
        // RTJ SECTION
        private async Task<List<RTJEntry>> GetRecentRTJEntries(DateTime startDate, DateTime endDate, string location)
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
                    AND G.GLJ_TRANSACTION_DATE BETWEEN @StartDate AND @EndDate
                    AND G.GLJ_WAREHOUSE_NUMBER IN (1, 2, 3, 6, 11, 86, 90)
                    AND G.GLJ_REFERENCE_NUMBER LIKE '%RTJ%'
                    AND G.GLJ_REFERENCE_NUMBER NOT LIKE '%1226%'
	                AND G.GLJ_REFERENCE_NUMBER NOT LIKE '%12to 6%'
	                AND G.GLJ_REFERENCE_NUMBER NOT LIKE '%12 to 6%'
                ORDER BY G.GLJ_DEBIT_AMOUNT DESC, G.GLJ_CREDIT_AMOUNT ASC
            ", conn);

            cmd.Parameters.AddWithValue("@StartDate", startDate);
            cmd.Parameters.AddWithValue("@EndDate", endDate);

            using var reader = await cmd.ExecuteReaderAsync();
            //Console.WriteLine($"{location}: Reader opened");


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
            //Console.WriteLine($"{location}: Reader read complete. Total: {result.Count}");
            return result;
        }

        [HttpGet]
        public async Task<IActionResult> GetRTJTable(string range = "mtd", int locationId = 0)
        {
            var (startDate, endDate) = DateRangeHelper.GetRange(range);

            var locations = LocationHelper.GetLocationQueryList(locationId);
            var rtjTasks = locations.Select(loc => GetRecentRTJEntries(startDate, endDate, loc));
            var rtjResult = await Task.WhenAll(rtjTasks);
            var allEntries = rtjResult.SelectMany(r => r).ToList();

            ViewBag.rtjRangeLabel = $"{startDate.ToString("M/d/yyyy")} to {endDate.ToString("M/d/yyyy")}";

            return PartialView("_RTJTablePartial", allEntries);
        }
        // END RTJ SECTION
    }

    // Helpers/DateRangeHelper.cs
    public static class DateRangeHelper
    {
        public static (DateTime StartDate, DateTime EndDate) GetRange(string rangeKey)
        {
            var today = DateTime.Today;
            DateTime startDate;
            DateTime endDate = today;

            switch (rangeKey?.ToLowerInvariant())
            {
                case "today":
                    startDate = today;
                    endDate = today;
                    break;

                case "yesterday":
                    {
                        var prevBiz = GetPreviousBusinessDay(today);
                        startDate = prevBiz;
                        endDate = prevBiz;
                        break;
                    }

                case "thisweek":
                default:
                    {
                        // Monday-based week
                        int diff = ((int)today.DayOfWeek - (int)DayOfWeek.Monday + 7) % 7;
                        startDate = today.AddDays(-diff);
                        endDate = startDate.AddDays(6);
                        break;
                    }

                case "lastweek":
                    {
                        int diff = ((int)today.DayOfWeek - (int)DayOfWeek.Monday + 7) % 7;
                        var thisWeekStart = today.AddDays(-diff);
                        startDate = thisWeekStart.AddDays(-7);
                        endDate = startDate.AddDays(6);
                        break;
                    }

                case "mtd":
                    startDate = new DateTime(today.Year, today.Month, 1);
                    endDate = today;
                    break;

                case "lastmonth":
                    startDate = new DateTime(today.Year, today.Month, 1).AddMonths(-1);
                    endDate = startDate.AddMonths(1).AddDays(-1);
                    break;

                case "thisquarter":
                    {
                        int quarterStartMonth = ((today.Month - 1) / 3) * 3 + 1; // 1,4,7,10
                        startDate = new DateTime(today.Year, quarterStartMonth, 1);
                        endDate = startDate.AddMonths(3).AddDays(-1);
                        break;
                    }

                case "lastquarter":
                    {
                        int quarterStartMonth = ((today.Month - 1) / 3) * 3 + 1;
                        var thisQuarterStart = new DateTime(today.Year, quarterStartMonth, 1);
                        startDate = thisQuarterStart.AddMonths(-3);
                        endDate = startDate.AddMonths(3).AddDays(-1);
                        break;
                    }

                case "ytd":
                    startDate = new DateTime(today.Year, 1, 1);
                    endDate = today;
                    break;

                case "lastyear":
                    {
                        int lastYear = today.Year - 1;
                        startDate = new DateTime(lastYear, 1, 1);
                        endDate = new DateTime(lastYear, 12, 31);
                        break;
                    }
            }

            return (startDate, endDate);
        }


        // ✅ Helper: Previous business day (weekends only)
        private static DateTime GetPreviousBusinessDay(DateTime date)
        {
            return date.DayOfWeek switch
            {
                DayOfWeek.Monday => date.AddDays(-3),
                DayOfWeek.Sunday => date.AddDays(-2),
                DayOfWeek.Saturday => date.AddDays(-1),
                _ => date.AddDays(-1) // Tue–Fri
            };
        }
    }
}
