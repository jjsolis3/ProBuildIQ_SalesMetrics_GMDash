// Controllers/Api/PropertiesApiController.cs
using Microsoft.AspNetCore.Mvc;
using SalesMetrics.Services.Signing;

namespace SalesMetrics.Controllers.Api
{
    [ApiController]
    [Route("api/properties")]
    public class PropertiesApiController : ControllerBase
    {
        private readonly IErpMergeService _erp;
        private readonly IHttpContextAccessor _http;

        public PropertiesApiController(IErpMergeService erp, IHttpContextAccessor http)
        {
            _erp = erp;
            _http = http;
        }

        // GET /api/properties/search?term=park&locationCode=LSV
        [HttpGet("search")]
        public async Task<IActionResult> Search([FromQuery] string term, [FromQuery] int take = 20, [FromQuery] string? locationCode = null)
        {
            // If locationCode is provided, temporarily override the session location for this search
            if (!string.IsNullOrWhiteSpace(locationCode))
            {
                var originalLocation = _http.HttpContext?.Session.GetString("OfficeLocation");
                try
                {
                    // Temporarily set the location for this request
                    _http.HttpContext?.Session.SetString("OfficeLocation", locationCode);
                    var props = await _erp.SearchPropertiesAsync(term, take);
                    return Ok(props.Select(p => new { id = p.CustomerId, name = p.CustomerName }));
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
                var props = await _erp.SearchPropertiesAsync(term, take);
                return Ok(props.Select(p => new { id = p.CustomerId, name = p.CustomerName }));
            }
        }

    }

}
