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
using SalesMetrics.Services.Erp;

namespace SalesMetrics.Services.Signing
{
    public sealed class ErpMergeService : IErpMergeService
    {
        private readonly SalesMetricsDbContext _db;
        private readonly IRazorViewToStringRenderer _renderer;
        private readonly IHttpContextAccessor _http;
        private readonly IConfiguration _config;
        private readonly ErpClientFactory _erpFactory;

        public ErpMergeService(
            SalesMetricsDbContext db,
            IRazorViewToStringRenderer renderer,
            IHttpContextAccessor http,
            IConfiguration config,
            ErpClientFactory erpFactory)
        {
            _db = db;
            _renderer = renderer;
            _http = http;
            _config = config;
            _erpFactory = erpFactory;
        }

        /// <summary>
        /// Helper to create ErpContext from current session location
        /// </summary>
        private ErpContext GetErpContext()
        {
            var locationCode = _http.HttpContext?.Session.GetString("OfficeLocation") ?? "LAX";
            return new ErpContext { LocationCode = locationCode };
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
                // Get property data from CUSTOMER_MASTER (ERP), not YardiProperties
                // PropertyID in SignEnvelope is CUM_CUMMAS_ID, not YardiProperties.Property_ID
                var propertyName = await GetPropertyNameAsync(env.PropertyID.Value);
                var propertyAddress = await GetPropertyAddressAsync(env.PropertyID.Value);

                if (!string.IsNullOrEmpty(propertyName))
                {
                    prop = new PropertyVm
                    {
                        PropertyId = env.PropertyID.Value,
                        Name = propertyName,
                        Address = propertyAddress ?? "",
                        City = "", // Not needed for email rendering
                        State = "",
                        Zip = "",
                        ManagerName = "", // Not needed for email rendering
                        Phone = "", // Not needed for email rendering
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

            // Use ERP abstraction layer instead of direct SQL
            var context = GetErpContext();
            var client = _erpFactory.GetClient(context);

            var property = await client.GetPropertyByIdAsync(propertyId.Value, context);
            return property?.CustomerName;
        }

        public async Task<string?> GetPropertyAddressAsync(int? propertyId)
        {
            if (!propertyId.HasValue) return null;

            // Use ERP abstraction layer instead of direct SQL
            var context = GetErpContext();
            var client = _erpFactory.GetClient(context);

            var property = await client.GetPropertyByIdAsync(propertyId.Value, context);
            if (property == null) return null;

            // Build address string
            var address = $"{property.Address1} {property.City}, {property.State} {property.Zip}".Trim();

            // Return null if address is empty or just commas/spaces
            return string.IsNullOrWhiteSpace(address) || address == "," || address == ", " ? null : address;
        }

        public async Task<string?> GetUnitNumberByOrderIdAsync(int? orderId)
        {
            if (!orderId.HasValue) return null;

            var order = await GetOrderByIdAsync(orderId.Value.ToString());
            return order?.UnitNumber;
        }

        public async Task<IReadOnlyList<CustomerPropertyViewModel>> SearchPropertiesAsync(string term, int take = 20)
        {
            // Use ERP abstraction layer instead of direct SQL
            var context = GetErpContext();
            var client = _erpFactory.GetClient(context);

            var searchResults = await client.SearchPropertiesAsync(term, context, skip: 0, take: take);

            // Map ERP DTOs to existing ViewModels
            var results = new List<CustomerPropertyViewModel>();
            foreach (var prop in searchResults.Items)
            {
                // Get full property details to get city/state
                var fullProperty = await client.GetPropertyByIdAsync(prop.Id, context);
                if (fullProperty != null)
                {
                    results.Add(new CustomerPropertyViewModel
                    {
                        CustomerId = fullProperty.CustomerId,
                        CustomerNumber = fullProperty.CustomerNumber,
                        CustomerName = fullProperty.CustomerName,
                        City = fullProperty.City ?? "",
                        State = fullProperty.State ?? ""
                    });
                }
            }

            return results;
        }

        public async Task<IReadOnlyList<WorkOrderViewModel>> GetOrdersForPropertyAsync(int propertyId, string status = "pending")
        {
            // TODO: Refactor to use ERP abstraction layer
            // Currently uses direct SQL due to complex joins with Apartments, Buildings, ApartmentType tables
            // These tables might be CompUFloor-specific and need investigation for Kudu equivalents
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
                  AND S.SOH_CANCELED_DATE IS NULL
                  AND S.SOH_CANCELED_DATE IS NULL
                  AND S.SOH_DELIVERY_DATE >= GETDATE()
                  AND S.SOH_INVOICE_TYPE = 0
                  AND S.SOH_WHSMAS_ID = 1
                  AND S.SOH_TOTAL_AMOUNT > 0
            ";

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

        // Helper method to get order by ID from ERP
        // TODO: Refactor to use ERP abstraction layer
        // Currently uses direct SQL due to complex joins with Apartments, Buildings, ApartmentType tables
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