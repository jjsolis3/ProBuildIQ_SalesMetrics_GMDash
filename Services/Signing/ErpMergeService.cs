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
        /// Helper to create ErpContext. When locationCode is provided it is used directly
        /// (important for background/anonymous calls such as TryFinalizeEnvelopeAsync where
        /// there is no authenticated session). Falls back to the session value when null.
        /// </summary>
        private ErpContext GetErpContext(string? locationCode = null)
        {
            var code = locationCode ?? _http.HttpContext?.Session.GetString("OfficeLocation") ?? "LAX";
            return new ErpContext { LocationCode = code };
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

            // Resolve template — prefer DB-stored HTML body (token path); fall back to Razor view
            var tmpl = await _db.SignTemplates.SingleAsync(t => t.TemplateKey == templateKey);

            if (!string.IsNullOrWhiteSpace(tmpl.HtmlBodyContent))
            {
                // Load tenant separately (envelope loaded via FindAsync without nav collection)
                var tenantForToken = await _db.SignRecipients
                    .Where(r => r.EnvelopeId == envelopeId && r.Role == "Tenant")
                    .FirstOrDefaultAsync();
                return ApplyTokens(tmpl.HtmlBodyContent, viewModel, tenantForToken);
            }

            // Legacy Razor path
            return await _renderer.RenderAsync(controllerContext, tmpl.RazorViewPath ?? "", viewModel);
        }

        public async Task<string?> GetPropertyNameAsync(int? propertyId, string? locationCode = null)
        {
            if (!propertyId.HasValue) return null;

            // Use ERP abstraction layer instead of direct SQL
            var context = GetErpContext(locationCode);
            var client = _erpFactory.GetClient(context);

            var property = await client.GetPropertyByIdAsync(propertyId.Value, context);
            return property?.CustomerName;
        }

        public async Task<string?> GetPropertyAddressAsync(int? propertyId, string? locationCode = null)
        {
            if (!propertyId.HasValue) return null;

            // Use ERP abstraction layer instead of direct SQL
            var context = GetErpContext(locationCode);
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
            // Use ERP abstraction layer
            var context = GetErpContext();
            var client = _erpFactory.GetClient(context);

            // Get future orders (pending status = delivery date in future)
            var startDate = DateTime.Today;
            var orders = await client.GetOrdersForPropertyAsync(
                propertyId,
                context,
                startDate: startDate,
                endDate: null,
                skip: 0,
                take: 50);

            // Map ErpOrder to WorkOrderViewModel
            var results = orders.Items.Select(o => new WorkOrderViewModel
            {
                OrderID = o.OrderNumber,
                PropertyId = o.CustomerId,
                PropertyNumber = int.TryParse(o.CustomerNumber, out var pn) ? pn : 0,
                PropertyName = o.CustomerName ?? string.Empty,
                City = o.ShipCity ?? string.Empty,
                DeliveryDate = o.DeliveryDate?.ToString("MM/dd/yyyy") ?? string.Empty,
                UnitNumber = o.Building ?? string.Empty,
                UnitType = o.ManagementCompany ?? string.Empty,
                PaidInFullDate = string.Empty // Set in ErpOrderDetails if needed
            }).ToList();

            return results;
        }

        // Helper method to get order by ID from ERP
        private async Task<WorkOrderViewModel?> GetOrderByIdAsync(string orderId)
        {
            // Use ERP abstraction layer
            var context = GetErpContext();
            var client = _erpFactory.GetClient(context);

            if (!int.TryParse(orderId, out var orderIdInt))
                return null;

            var order = await client.GetOrderDetailAsync(orderIdInt, context);
            if (order == null) return null;

            // Map ErpOrderDetails to WorkOrderViewModel
            return new WorkOrderViewModel
            {
                OrderID = order.OrderNumber,
                PropertyId = order.CustomerId,
                PropertyNumber = int.TryParse(order.CustomerNumber, out var pn) ? pn : 0,
                PropertyName = order.CustomerName ?? string.Empty,
                City = order.ShipCity ?? string.Empty,
                DeliveryDate = order.DeliveryDate?.ToString("MM/dd/yyyy") ?? string.Empty,
                UnitNumber = order.Building ?? string.Empty,
                UnitType = order.ManagementCompany ?? string.Empty,
                PaidInFullDate = order.PaidInFullDate?.ToString("MM/dd/yyyy") ?? string.Empty
            };
        }

        // New methods for customer contact info
        public async Task<string?> GetCustomerPhoneAsync(int? propertyId)
        {
            if (!propertyId.HasValue) return null;

            try
            {
                var context = GetErpContext();
                var client = _erpFactory.GetClient(context);
                var property = await client.GetPropertyByIdAsync(propertyId.Value, context);
                return property?.PhoneNumber;
            }
            catch
            {
                return null;
            }
        }

        public async Task<string?> GetCustomerEmailAsync(int? propertyId)
        {
            if (!propertyId.HasValue) return null;

            try
            {
                var context = GetErpContext();
                var client = _erpFactory.GetClient(context);
                var property = await client.GetPropertyByIdAsync(propertyId.Value, context);
                return property?.Email;
            }
            catch
            {
                return null;
            }
        }

        public async Task<string?> GetCustomerNameAsync(int? propertyId)
        {
            if (!propertyId.HasValue) return null;

            try
            {
                var context = GetErpContext();
                var client = _erpFactory.GetClient(context);
                var property = await client.GetPropertyByIdAsync(propertyId.Value, context);
                return property?.CustomerName;
            }
            catch
            {
                return null;
            }
        }

        public async Task<string?> GetCustomerCompanyAsync(int? propertyId)
        {
            // In this ERP, the company name is the same as the customer name
            return await GetCustomerNameAsync(propertyId);
        }

        public async Task<string?> GetPropertyCityAsync(int? propertyId)
        {
            if (!propertyId.HasValue) return null;
            try
            {
                var context = GetErpContext();
                var client = _erpFactory.GetClient(context);
                var property = await client.GetPropertyByIdAsync(propertyId.Value, context);
                return property?.City;
            }
            catch { return null; }
        }

        public async Task<string?> GetPropertyStateAsync(int? propertyId)
        {
            if (!propertyId.HasValue) return null;
            try
            {
                var context = GetErpContext();
                var client = _erpFactory.GetClient(context);
                var property = await client.GetPropertyByIdAsync(propertyId.Value, context);
                return property?.State;
            }
            catch { return null; }
        }

        public async Task<string?> GetPropertyZipAsync(int? propertyId)
        {
            if (!propertyId.HasValue) return null;
            try
            {
                var context = GetErpContext();
                var client = _erpFactory.GetClient(context);
                var property = await client.GetPropertyByIdAsync(propertyId.Value, context);
                return property?.Zip;
            }
            catch { return null; }
        }

        // New methods for order/lease dates
        public async Task<DateTime?> GetOrderStartDateAsync(int? orderId)
        {
            if (!orderId.HasValue) return null;

            try
            {
                var order = await GetOrderByIdAsync(orderId.Value.ToString());
                // Assuming start date could be delivery date or a custom start date field
                if (order != null && DateTime.TryParse(order.DeliveryDate, out var date))
                {
                    return date;
                }
                return null;
            }
            catch
            {
                return null;
            }
        }

        public async Task<DateTime?> GetOrderEndDateAsync(int? orderId)
        {
            if (!orderId.HasValue) return null;

            try
            {
                var order = await GetOrderByIdAsync(orderId.Value.ToString());
                // End date could be calculated from start date + lease term
                // For now, return null - can be enhanced with actual lease end date logic
                return null;
            }
            catch
            {
                return null;
            }
        }

        public async Task<DateTime?> GetOrderSignedDateAsync(int? orderId)
        {
            if (!orderId.HasValue) return null;

            try
            {
                var order = await GetOrderByIdAsync(orderId.Value.ToString());
                // Signed date could be PaidInFullDate or OrderDate
                if (order != null && DateTime.TryParse(order.PaidInFullDate, out var date))
                {
                    return date;
                }
                return null;
            }
            catch
            {
                return null;
            }
        }

        // ======================================================================
        // HTML Body Token Replacement
        // ======================================================================

        /// <summary>
        /// Available tokens for DB-stored template HTML.
        /// Staff use {{TokenName}} syntax in the TinyMCE editor.
        /// </summary>
        public static readonly IReadOnlyList<(string Token, string Description)> AvailableTokens = new[]
        {
            ("{{RecipientName}}",    "Signer's full name"),
            ("{{RecipientEmail}}",   "Signer's email address"),
            ("{{RecipientRole}}",    "Signer's role (Manager / Tenant)"),
            ("{{TenantName}}",       "Tenant's full name (if added to envelope)"),
            ("{{TenantEmail}}",      "Tenant's email address"),
            ("{{PropertyName}}",     "Property / customer name"),
            ("{{PropertyAddress}}", "Property street address"),
            ("{{OrderNumber}}",      "Work order number"),
            ("{{UnitNumber}}",       "Unit number"),
            ("{{LocationCode}}",     "Branch code (e.g. PHX, LAX)"),
            ("{{EnvelopeId}}",       "Envelope ID number"),
            ("{{Subject}}",          "Envelope subject line"),
            ("{{Date}}",             "Today's date (Month D, YYYY)"),
        };

        private static string ApplyTokens(string html, TemplateRenderVm vm,
            SalesMetrics.Domain.Signing.SignRecipient? tenant = null)
        {
            var tokens = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["{{RecipientName}}"]    = vm.Recipient?.FullName ?? "",
                ["{{RecipientEmail}}"]   = vm.Recipient?.Email ?? "",
                ["{{RecipientRole}}"]    = vm.Recipient?.Role ?? "",
                ["{{TenantName}}"]       = tenant?.FullName ?? "",
                ["{{TenantEmail}}"]      = tenant?.Email ?? "",
                ["{{PropertyName}}"]     = vm.Property?.Name ?? vm.Envelope.PropertyName ?? "",
                ["{{PropertyAddress}}"] = vm.Property?.Address ?? "",
                ["{{OrderNumber}}"]      = vm.Envelope.OrderNumber ?? "",
                ["{{UnitNumber}}"]       = vm.Property?.Unit ?? vm.Order?.UnitNumber ?? "",
                ["{{LocationCode}}"]     = vm.Envelope.LocationCode ?? "",
                ["{{EnvelopeId}}"]       = vm.Envelope.EnvelopeId.ToString(),
                ["{{Subject}}"]          = vm.Envelope.Subject ?? "",
                ["{{Date}}"]             = DateTime.Now.ToString("MMMM d, yyyy"),
            };

            foreach (var (token, value) in tokens)
                html = html.Replace(token, System.Net.WebUtility.HtmlEncode(value), StringComparison.OrdinalIgnoreCase);

            return html;
        }
    }
}