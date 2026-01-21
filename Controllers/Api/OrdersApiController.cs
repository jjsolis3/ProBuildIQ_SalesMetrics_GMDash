// Controllers/Api/OrdersApiController.cs
using Microsoft.AspNetCore.Mvc;
using SalesMetrics.Services.Signing;

namespace SalesMetrics.Controllers.Api
{

    [ApiController]
    [Route("api/orders")]
    public class OrdersApiController : ControllerBase
    {
        private readonly IErpMergeService _erp;
        private readonly IHttpContextAccessor _http;

        public OrdersApiController(IErpMergeService erp, IHttpContextAccessor http)
        {
            _erp = erp;
            _http = http;
        }

        // GET /api/orders/by-property?propertyId=123&locationCode=LSV
        [HttpGet("by-property")]
        public async Task<IActionResult> ByProperty([FromQuery] int propertyId, [FromQuery] string? locationCode = null)
        {
            // If locationCode is provided, temporarily override the session location for this search
            if (!string.IsNullOrWhiteSpace(locationCode))
            {
                var originalLocation = _http.HttpContext?.Session.GetString("OfficeLocation");
                try
                {
                    // Temporarily set the location for this request
                    _http.HttpContext?.Session.SetString("OfficeLocation", locationCode);
                    var orders = await _erp.GetOrdersForPropertyAsync(propertyId);
                    return Ok(orders.Select(o => new { id = o.OrderID, display = $"{o.OrderID} · {o.UnitNumber}" }));
                }
                finally
                {
                    // Restore original session location
                    if (originalLocation != null)
                    {
                        _http.HttpContext?.Session.SetString("OfficeLocation", originalLocation);
                    }
                }
            }
            else
            {
                // Use session location (default behavior)
                var orders = await _erp.GetOrdersForPropertyAsync(propertyId);
                return Ok(orders.Select(o => new { id = o.OrderID, display = $"{o.OrderID} · {o.UnitNumber}" }));
            }
        }

    }

}
