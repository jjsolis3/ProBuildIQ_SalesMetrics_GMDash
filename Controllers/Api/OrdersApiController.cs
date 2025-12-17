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
        public OrdersApiController(IErpMergeService erp) => _erp = erp;

        // GET /api/orders/by-property?propertyId=123&status=pending
        // OrdersApiController.cs
        [HttpGet("by-property")]
        public async Task<IActionResult> ByProperty([FromQuery] int propertyId)
        {
            var orders = await _erp.GetOrdersForPropertyAsync(propertyId);
            return Ok(orders.Select(o => new { id = o.OrderID, display = $"{o.OrderID} · {o.UnitNumber}" }));
        }

    }

}
