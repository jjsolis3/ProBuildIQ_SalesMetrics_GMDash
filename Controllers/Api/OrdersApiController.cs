// Controllers/Api/OrdersApiController.cs
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SalesMetrics.Services.Signing;

namespace SalesMetrics.Controllers.Api
{
    [Authorize]
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
            // If a locationCode override is requested, verify the caller is authorised for it.
            if (!string.IsNullOrWhiteSpace(locationCode))
            {
                if (!IsLocationAuthorised(locationCode))
                    return Forbid();

                var originalLocation = _http.HttpContext?.Session.GetString("OfficeLocation");
                try
                {
                    _http.HttpContext?.Session.SetString("OfficeLocation", locationCode);
                    var orders = await _erp.GetOrdersForPropertyAsync(propertyId);
                    return Ok(orders.Select(o => new { id = o.OrderID, display = $"{o.OrderID} · {o.UnitNumber}" }));
                }
                finally
                {
                    if (originalLocation != null)
                        _http.HttpContext?.Session.SetString("OfficeLocation", originalLocation);
                }
            }
            else
            {
                var orders = await _erp.GetOrdersForPropertyAsync(propertyId);
                return Ok(orders.Select(o => new { id = o.OrderID, display = $"{o.OrderID} · {o.UnitNumber}" }));
            }
        }

        /// <summary>
        /// Returns true if the current user is authorised to access the given location code.
        /// Admins (RoleId == 1) have access to all locations. All other users may only access
        /// their own assigned locations.
        /// </summary>
        private bool IsLocationAuthorised(string locationCode)
        {
            var roleId = int.TryParse(User.FindFirst("RoleId")?.Value, out var r) ? r : 0;
            if (roleId == 1) return true; // Admin

            var userLocation = _http.HttpContext?.Session.GetString("OfficeLocation");
            return string.Equals(userLocation, locationCode, StringComparison.OrdinalIgnoreCase);
        }
    }
}
