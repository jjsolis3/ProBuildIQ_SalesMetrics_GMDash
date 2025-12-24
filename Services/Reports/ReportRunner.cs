using System;
using System.Data;
using Microsoft.Extensions.Logging;
using SalesMetrics.Models.Reports;
using SalesMetrics.Services.Erp;
using SalesMetrics.Services.Helpers;

namespace SalesMetrics.Services.Reports
{
    public class ReportRunner : IReportRunner
    {
        private readonly IErpDataClient _erpDataClient;
        private readonly ILogger<ReportRunner> _logger;

        public ReportRunner(IErpDataClient erpDataClient, ILogger<ReportRunner> logger)
        {
            _erpDataClient = erpDataClient;
            _logger = logger;
        }

        public Task<DataTable> RunAsync(string definitionId, ReportParameters parameters, ReportUserContext userContext)
        {
            _logger.LogInformation("Running report {ReportId} for user {UserId} at location {Location}", definitionId, userContext.UserId, userContext.LocationId);
            return definitionId.ToLowerInvariant() switch
            {
                "margin-commission-discrepancy-open-invoiced" => RunMarginCommissionDiscrepancy(parameters, userContext),
                _ => Task.FromResult(new DataTable())
            };
        }

        private async Task<DataTable> RunMarginCommissionDiscrepancy(ReportParameters parameters, ReportUserContext userContext)
        {
            if (!parameters.FromDate.HasValue || !parameters.ToDate.HasValue)
            {
                throw new ArgumentException("A date range is required to run this report.");
            }

            var warehouseId = parameters.WarehouseId ?? Math.Max(userContext.LocationId, 1);
            var connectionName = LocationHelper.GetConnectionName(userContext.LocationId) ?? userContext.OfficeLocation ?? "LAX";
            var excludeSalesman24 = string.Equals(connectionName, "LAX", StringComparison.OrdinalIgnoreCase) ||
                                    string.Equals(userContext.OfficeLocation, "LAX", StringComparison.OrdinalIgnoreCase);

            var sql = @"DECLARE @MgmtCo nvarchar(5);
                DECLARE @ExcludeSalesman24 bit = @ExcludeSalesman24Param;

                SELECT @MgmtCo = P.IPC_PRICE_CODE
                FROM PRICE_CODES P WITH (NOLOCK)
                WHERE (@MgmtName IS NOT NULL AND LEN(@MgmtName) > 0) AND P.IPC_DESCRIPTION LIKE '%' + @MgmtName + '%';

                ;WITH base AS (
                    SELECT
                        sh.SOH_WHSMAS_ID                                  AS Warehouse,
                        sh.SOH_SHIP_TO_NAME                               AS PropertyName,
                        sh.SOH_NUMBER                                     AS OrderID,
                        MAX(
                            COALESCE(
                                NULLIF(NULLIF(CAST(sd.SDT_INVOICE_NUMBER AS varchar(50)), '0'), ''),
                                CAST(ih.IHF_INVOICE_NUMBER AS varchar(50))
                            )
                        )                                                 AS InvoiceID,
                        MAX(sm.SMN_SALESMAN_NAME)                         AS SalesmanName,
                        MAX(sh.SOH_SMNMAS_ORDER)                          AS SalesmanID,
                        MAX(sm.SMN_COMMISSION_PCT) / 100.0                AS CommissionPct,
                        MAX(p.IPC_DESCRIPTION)                            AS PriceCodeDesc,
                        CAST(MAX(sh.SOH_ORDER_DATE) AS date)              AS OrderDate,
                        CAST(MAX(ih.IHF_INVOICE_DATE) as date)            AS InvoiceDate,
                        CAST(MAX(ar.ARO_DATE_PAID_IN_FULL) as date)       AS PaidDate,
                        SUM(CAST(sd.SDT_EXTENDED_PRICE AS decimal(18,2))) AS Revenue,
                        SUM(CAST(sd.SDT_EXTENDED_COST  AS decimal(18,2))) AS Cost,
                        SUM(CAST(sd.SDT_USE_TAX_AMOUNT AS decimal(18,2))) AS UseTax,
                        SUM(CAST(sd.SDT_EXTENDED_COST * COALESCE(im.ITM_COST_OVERHEAD_PERCENT,0)/100.0 AS decimal(18,2))) AS Overhead,
                        MAX(CAST(sd.SDT_INVOICE_FLAG AS tinyint))         AS AnyLineInvoiced
                    FROM SALES_DETAIL sd WITH (NOLOCK)
                    JOIN SALES_HEADER sh WITH (NOLOCK)
                        ON sh.SOH_NUMBER    = sd.SDT_SALOHD_ID
                       AND sh.SOH_WHSMAS_ID = sd.SDT_WHSMAS_ID
                    LEFT JOIN Salesman_Master sm WITH (NOLOCK)
                        ON sm.SMN_SMNMAS_ID = sh.SOH_SMNMAS_ORDER
                    LEFT JOIN ITEM_MASTER im WITH (NOLOCK)
                        ON im.ITM_ID_FIRST  = sd.SDT_ITEM_FIRST
                       AND im.ITM_ID_LAST   = sd.SDT_ITEM_LAST
                    LEFT JOIN PRICE_CODES p WITH (NOLOCK)
                        ON p.IPC_PRICE_CODE = sh.SOH_PRICE_CODE
                    LEFT JOIN INVOICE_HEADER ih WITH (NOLOCK)
                        ON ih.IHF_ORDER_NUMBER = sh.SOH_NUMBER
                       AND ih.IHF_WHSMAS_ID    = sh.SOH_WHSMAS_ID
                    LEFT JOIN AR_OPEN_ITEM ar WITH (NOLOCK)
                        ON ih.IHF_ORDER_NUMBER = ar.ARO_INVOICE_NUMBER
                        AND sh.SOH_NUMBER = ar.ARO_SALES_ORDER_NUMBER
                    WHERE
                        CAST(sh.SOH_ORDER_DATE AS date) BETWEEN @FromDate AND @ToDate
                        AND sh.SOH_WHSMAS_ID = @WarehouseId
                        AND NOT ( @ExcludeSalesman24 = 1 AND sm.SMN_SMNMAS_ID = 24 )
                        AND ( @FilterByMgmt = 0 OR sh.SOH_PRICE_CODE = @MgmtCo )
                    GROUP BY
                        sh.SOH_WHSMAS_ID, sh.SOH_SHIP_TO_NAME, sh.SOH_NUMBER
                ),
                agg AS (
                    SELECT
                        b.Warehouse,
                        b.PropertyName,
                        b.OrderID,
                        b.InvoiceID,
                        b.SalesmanName,
                        b.SalesmanID,
                        b.CommissionPct,
                        b.PriceCodeDesc,
                        b.OrderDate,
                        b.PaidDate,
                        DATEDIFF(day, b.OrderDate, GETDATE())  AS DaysOpen,
                        b.Revenue,
                        b.Cost,
                        b.UseTax,
                        b.Overhead,
                        CASE WHEN b.InvoiceID IS NOT NULL OR b.AnyLineInvoiced = 1 THEN 'Invoiced' ELSE 'Open' END AS [Status]
                    FROM base b
                )
                SELECT
                    DB_NAME()                              AS DB,
                    a.[Status],
                    a.SalesmanName,
                    a.OrderID,
                    ISNULL(a.InvoiceID, '0')               AS InvoiceID,
                    a.PropertyName,
                    a.PriceCodeDesc                        AS PriceCode,
                    a.OrderDate,
                    CASE 
                        WHEN a.[Status] = 'Open' THEN a.DaysOpen 
                        WHEN a.[Status] <> 'Open' AND a.PaidDate is NULL THEN a.DaysOpen
                    END AS DaysOpen,
                    a.Revenue       AS OrderAmount,
                    a.Cost          AS CostAmount,
                    a.Overhead      AS CostAmountOverHead,
                    a.UseTax        AS UseTax,
                    p.Profit        AS ProjectedProfit,
                    p.Margin        AS ProjectedMargin,
                    targ.RequiredRevenueToHitTarget,
                    targ.DeltaRevenueToHitTarget,
                    targ.NeededPctIncreaseToTarget,
                    CASE WHEN p.Margin >= @MinMargin THEN 'YES' ELSE 'NO' END AS MeetsCommissionGate,
                    CASE WHEN p.Margin >= @TargetMargin THEN 'YES' ELSE 'NO' END AS MeetsTargetMargin
                FROM agg a
                CROSS APPLY (
                    SELECT
                        Profit = a.Revenue - (a.Cost + a.UseTax + a.Overhead),
                        Margin = CASE WHEN a.Revenue = 0 THEN 0
                                      ELSE (a.Revenue - (a.Cost + a.UseTax + a.Overhead)) / NULLIF(a.Revenue,0) END
                ) p
                CROSS APPLY (
                    SELECT
                        RequiredRevenueToHitTarget = CASE WHEN (1 - @TargetMargin) = 0 THEN NULL
                                                          ELSE (a.Cost + a.UseTax + a.Overhead) / (1 - @TargetMargin) END,
                        DeltaRevenueToHitTarget    = CASE WHEN (1 - @TargetMargin) = 0 THEN NULL
                                                          ELSE ((a.Cost + a.UseTax + a.Overhead) / (1 - @TargetMargin)) - a.Revenue END,
                        NeededPctIncreaseToTarget  = CASE WHEN a.Revenue = 0 THEN NULL
                                                          ELSE ( ((a.Cost + a.UseTax + a.Overhead) / (1 - @TargetMargin)) - a.Revenue )
                                                               / a.Revenue END
                ) targ
                CROSS APPLY (
                    SELECT
                        ProjectedPotentialCommission   = p.Profit * a.CommissionPct,
                        ProjectedCommissionIfInvoiced =
                            CASE
                              WHEN p.Profit <= 0          THEN p.Profit * a.CommissionPct
                              WHEN p.Margin <= @MinMargin THEN 0
                              ELSE p.Profit * a.CommissionPct
                            END
                ) calc
                ORDER BY
                    CASE WHEN a.[Status] = 'Open' THEN 0 ELSE 1 END,
                    a.OrderID asc,
                    a.SalesmanName;
            ";

            var queryParameters = new
            {
                FromDate = parameters.FromDate.Value,
                ToDate = parameters.ToDate.Value,
                MinMargin = parameters.MinMargin ?? 0.15m,
                TargetMargin = parameters.TargetMargin ?? 0.22m,
                MgmtName = parameters.MgmtName ?? string.Empty,
                FilterByMgmt = parameters.FilterByMgmt ? 1 : 0,
                WarehouseId = warehouseId,
                ExcludeSalesman24Param = excludeSalesman24 ? 1 : 0
            };

            var context = new ErpContext
            {
                ConnectionName = connectionName,
                LocationCode = userContext.OfficeLocation,
                LocationId = userContext.LocationId,
                UserId = userContext.UserId
            };

            return await _erpDataClient.QueryAsync("MarginCommissionDiscrepancyOpenInvoiced", sql, queryParameters, context);
        }
    }
}
