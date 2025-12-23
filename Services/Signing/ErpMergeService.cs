// Services/Signing/ErpMergeService.cs
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Routing;
using Microsoft.AspNetCore.Routing;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using SalesMetrics.Data;
using SalesMetrics.Domain.Signing;
using SalesMetrics.Models;
using SalesMetrics.Models.EFCore;
using SalesMetrics.Models.Signing;
using SalesMetrics.Services.Mvc;
using SalesMetrics.Services.Signing;

namespace SalesMetrics.Services.Signing
{
    public sealed class ErpMergeService : IErpMergeService
    {
        private readonly SalesMetricsDbContext _db;
        private readonly IRazorViewToStringRenderer _renderer;
        private readonly IHttpContextAccessor _http;
        private readonly IConfiguration _config;

        public ErpMergeService(SalesMetricsDbContext db, IRazorViewToStringRenderer renderer, IHttpContextAccessor http, IConfiguration config)
        {
            _db = db;
            _renderer = renderer;
            _http = http;
            _config = config;
        }

        public async Task<string> RenderHtmlAsync(string templateKey, long envelopeId, long? recipientId = null)
        {
            // Load envelope + current recipient
            var env = await _db.SignEnvelopes.FindAsync(envelopeId)
                      ?? throw new InvalidOperationException($"Envelope {envelopeId} not found.");

            var rcpt = await _db.SignRecipients
                .Where(r => r.EnvelopeId == envelopeId &&
                            (recipientId == null || r.RecipientId == recipientId))
                .OrderBy(r => r.SignerOrder)
                .FirstOrDefaultAsync()
                ?? throw new InvalidOperationException($"Recipient for envelope {envelopeId} not found.");

            // Load optional ERP context
            PropertyVm? prop = null;
            OrderVm? ord = null;

            if (env.PropertyID.HasValue)
            {
                var p = await GetPropertyByIdAsync(env.PropertyID.Value);
                if (p != null)
                {
                    prop = new PropertyVm
                    {
                        PropertyId = p.Property_ID,
                        Name = p.PropertyName,
                        Address = p.PropertyAddress,
                        City = p.PropertyCity,
                        State = p.PropertyState,
                        Zip = p.PropertyZipCode,
                        ManagerName = p.Manager,
                        Phone = p.PropertyPhone,
                        Unit = null // Will be set from order if available
                    };
                }
            }

            if (env.OrderId.HasValue)
            {
                var o = await GetOrderByIdAsync(env.OrderId.Value.ToString());
                if (o != null)
                {
                    ord = new OrderVm
                    {
                        OrderId = o.OrderID,
                        UnitNumber = o.UnitNumber,
                        DeliveryDate = o.DeliveryDate,
                        TenantName = o.PropertyName
                    };

                    // Set Unit on Property if we have order data
                    if (prop != null && !string.IsNullOrWhiteSpace(o.UnitNumber))
                    {
                        prop = prop with { Unit = o.UnitNumber };
                    }
                }
            }

            var viewModel = new TemplateRenderVm
            {
                Envelope = env,
                Recipient = rcpt,
                Property = prop,
                Order = ord
            };

            // Create a PROPER ControllerContext with ControllerActionDescriptor
            var httpContext = _http.HttpContext ?? throw new InvalidOperationException("HttpContext not available");

            var actionDescriptor = new ControllerActionDescriptor
            {
                ActionName = "RenderTemplate",
                ControllerName = "SignPublic",
                DisplayName = "Render Sign Template"
            };

            var actionContext = new ActionContext(httpContext, httpContext.GetRouteData(), actionDescriptor);
            var controllerContext = new ControllerContext(actionContext);

            // Resolve view path and render
            var tmpl = await _db.SignTemplates.SingleAsync(t => t.TemplateKey == templateKey);
            return await _renderer.RenderAsync(controllerContext, tmpl.RazorViewPath, viewModel);
        }

        public async Task<string?> GetPropertyNameAsync(int? propertyId)
        {
            if (!propertyId.HasValue) return null;

            var prop = await _db.YardiProperties
                .Where(p => p.Property_ID == propertyId.Value)
                .Select(p => p.PropertyName)
                .FirstOrDefaultAsync();

            return prop;
        }

        public async Task<string?> GetUnitNumberByOrderIdAsync(int? orderId)
        {
            if (!orderId.HasValue) return null;

            var order = await GetOrderByIdAsync(orderId.Value.ToString());
            return order?.UnitNumber;
        }

        public async Task<IReadOnlyList<CustomerPropertyViewModel>> SearchPropertiesAsync(string term, int take = 20)
        {
            var officeLocation = _http.HttpContext?.Session.GetString("OfficeLocation") ?? "LAX";
            var connStr = _config.GetConnectionString(officeLocation);
            var results = new List<CustomerPropertyViewModel>();

            using var conn = new SqlConnection(connStr);
            await conn.OpenAsync();

            var sql = @"
                SELECT TOP (@take)
                    C.CUM_CUMMAS_ID as CustomerId,
                    C.CUM_CUSTOMER_NUMBER as CustomerNumber,
                    C.CUM_CUSTOMER_NAME as CustomerName,
                    ISNULL(C.CUM_CITY, '') as City,
                    ISNULL(C.CUM_STATE, '') as State
                FROM CUSTOMER_MASTER C
                WHERE C.CUM_CUSTOMER_NAME LIKE @term
                ORDER BY C.CUM_CUSTOMER_NAME";

            using var cmd = new SqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@take", take);
            cmd.Parameters.AddWithValue("@term", "%" + term + "%");

            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                results.Add(new CustomerPropertyViewModel
                {
                    CustomerId = reader.GetInt32(0),
                    CustomerNumber = reader.GetString(1),
                    CustomerName = reader.GetString(2),
                    City = reader.GetString(3),
                    State = reader.GetString(4)
                });
            }

            return results;
        }

        public async Task<IReadOnlyList<WorkOrderViewModel>> GetOrdersForPropertyAsync(int propertyId, string status = "pending")
        {
            var officeLocation = _http.HttpContext?.Session.GetString("OfficeLocation") ?? "LAX";
            var connStr = _config.GetConnectionString(officeLocation);
            var results = new List<WorkOrderViewModel>();

            using var conn = new SqlConnection(connStr);
            await conn.OpenAsync();

            var sql = @"
                SELECT TOP 50
                    SOH_NUMBER AS OrderID,
                    CUS.CUM_CUMMAS_ID as PropertyID,
                    CUS.CUM_CUSTOMER_NUMBER as PropertyNumber,
                    CUS.CUM_CUSTOMER_NAME AS PropertyName,
                    CUS.CUM_CITY AS City,
                    CONVERT(VARCHAR, S.SOH_DELIVERY_DATE, 101) AS DeliveryDate,
                    B.BuildingNumber + ' - ' + A.ApartmentNumber AS UnitNumber,
                    APT.Description AS UnitType,
                    ISNULL(AR.ARO_DATE_PAID_IN_FULL, '') AS PaidInFullDate
                FROM SALES_HEADER S
                    LEFT JOIN CUSTOMER_MASTER CUS ON CUS.CUM_CUMMAS_ID = S.SOH_CUMMAS_ID
                    LEFT JOIN Apartments A ON S.SOH_APARTMENT_ID = A.Id
                    LEFT JOIN Buildings B ON A.Building_id = B.Id
                    LEFT JOIN ApartmentType APT ON A.ApartmentType_Id = APT.ID
                    LEFT JOIN AR_OPEN_ITEM AR on S.SOH_NUMBER = AR.ARO_SALES_ORDER_NUMBER
                WHERE CUS.CUM_CUMMAS_ID = @propertyId
                  AND S.SOH_CANCELED_DATE IS NULL";

            using var cmd = new SqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@propertyId", propertyId);

            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                results.Add(new WorkOrderViewModel
                {
                    OrderID = reader["OrderID"].ToString(),
                    PropertyId = Convert.ToInt32(reader["PropertyID"]),
                    PropertyNumber = Convert.ToInt32(reader["PropertyNumber"]),
                    PropertyName = reader["PropertyName"].ToString(),
                    City = reader["City"].ToString(),
                    DeliveryDate = reader["DeliveryDate"].ToString(),
                    UnitNumber = reader["UnitNumber"].ToString(),
                    UnitType = reader["UnitType"].ToString(),
                    PaidInFullDate = reader["PaidInFullDate"].ToString()
                });
            }

            return results;
        }

        // Helper method to get property by ID from YardiProperties table
        private async Task<YardiPropertyEntity?> GetPropertyByIdAsync(int propertyId)
        {
            return await _db.YardiProperties
                .FirstOrDefaultAsync(p => p.Property_ID == propertyId);
        }

        // Helper method to get order by ID from ERP
        private async Task<WorkOrderViewModel?> GetOrderByIdAsync(string orderId)
        {
            var officeLocation = _http.HttpContext?.Session.GetString("OfficeLocation") ?? "LAX";
            var connStr = _config.GetConnectionString(officeLocation);

            using var conn = new SqlConnection(connStr);
            await conn.OpenAsync();

            var sql = @"
                SELECT TOP 1
                    SOH_NUMBER AS OrderID,
                    CUS.CUM_CUMMAS_ID as PropertyID,
                    CUS.CUM_CUSTOMER_NUMBER as PropertyNumber,
                    CUS.CUM_CUSTOMER_NAME AS PropertyName,
                    CUS.CUM_CITY AS City,
                    CONVERT(VARCHAR, S.SOH_DELIVERY_DATE, 101) AS DeliveryDate,
                    B.BuildingNumber + ' - ' + A.ApartmentNumber AS UnitNumber,
                    APT.Description AS UnitType,
                    ISNULL(AR.ARO_DATE_PAID_IN_FULL, '') AS PaidInFullDate
                FROM SALES_HEADER S
                    LEFT JOIN CUSTOMER_MASTER CUS ON CUS.CUM_CUMMAS_ID = S.SOH_CUMMAS_ID
                    LEFT JOIN Apartments A ON S.SOH_APARTMENT_ID = A.Id
                    LEFT JOIN Buildings B ON A.Building_id = B.Id
                    LEFT JOIN ApartmentType APT ON A.ApartmentType_Id = APT.ID
                    LEFT JOIN AR_OPEN_ITEM AR on S.SOH_NUMBER = AR.ARO_SALES_ORDER_NUMBER
                WHERE S.SOH_NUMBER = @orderId";

            using var cmd = new SqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@orderId", orderId);

            using var reader = await cmd.ExecuteReaderAsync();
            if (await reader.ReadAsync())
            {
                return new WorkOrderViewModel
                {
                    OrderID = reader["OrderID"].ToString(),
                    PropertyId = Convert.ToInt32(reader["PropertyID"]),
                    PropertyNumber = Convert.ToInt32(reader["PropertyNumber"]),
                    PropertyName = reader["PropertyName"].ToString(),
                    City = reader["City"].ToString(),
                    DeliveryDate = reader["DeliveryDate"].ToString(),
                    UnitNumber = reader["UnitNumber"].ToString(),
                    UnitType = reader["UnitType"].ToString(),
                    PaidInFullDate = reader["PaidInFullDate"].ToString()
                };
            }

            return null;
        }
    }
}