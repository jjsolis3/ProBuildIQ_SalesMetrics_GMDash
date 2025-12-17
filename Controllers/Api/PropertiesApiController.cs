// Controllers/Api/PropertiesApiController.cs
using Microsoft.AspNetCore.Mvc;
using SalesMetrics.Services.Signing;

namespace SalesMetrics.Controllers.Api
{
    [ApiController]
    [Route("api/properties")]
    public class PropertiesApiController : ControllerBase
    {
        private readonly IErpMergeService _erp; // whatever you use today
        public PropertiesApiController(IErpMergeService erp) => _erp = erp;

        // GET /api/properties/search?term=park
        // PropertiesApiController.cs
        [HttpGet("search")]
        public async Task<IActionResult> Search([FromQuery] string term, [FromQuery] int take = 20)
        {
            var props = await _erp.SearchPropertiesAsync(term, take);
            return Ok(props.Select(p => new { id = p.CustomerId, name = p.CustomerName }));
        }

    }

}
