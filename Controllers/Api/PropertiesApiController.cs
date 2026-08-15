// Controllers/Api/PropertiesApiController.cs
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SalesMetrics.Services.Signing;
using SalesMetrics.Services.Helpers;

namespace SalesMetrics.Controllers.Api
{
    [Authorize]
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
            // If a locationCode override is requested, verify the caller is authorised for it.
            if (!string.IsNullOrWhiteSpace(locationCode))
            {
                if (!IsLocationAuthorised(locationCode))
                    return Forbid();

                var originalLocation = _http.HttpContext?.Session.GetString("OfficeLocation");
                try
                {
                    _http.HttpContext?.Session.SetString("OfficeLocation", locationCode);
                    var props = await _erp.SearchPropertiesAsync(term, take);
                    return Ok(props.Select(p => new { id = p.CustomerId, name = p.CustomerName }));
                }
                finally
                {
                    if (originalLocation != null)
                        _http.HttpContext?.Session.SetString("OfficeLocation", originalLocation);
                }
            }
            else
            {
                var props = await _erp.SearchPropertiesAsync(term, take);
                return Ok(props.Select(p => new { id = p.CustomerId, name = p.CustomerName }));
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
