// Controllers/SignAdminController.cs
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using SalesMetrics.Services.Signing;
using SalesMetrics.Models.Signing;
using SalesMetrics.Data;
using SalesMetrics.Services.Helpers;
using Microsoft.EntityFrameworkCore;

namespace SalesMetrics.Controllers;

/// <summary>
/// Admin portal for managing envelopes and signatures
/// Access: Office Staff, Office Manager, Sales Team, Sales Admin, General Manager, Admin
/// </summary>
[Authorize]
public class SignAdminController : Controller
{
    private readonly IEnvelopeService _svc;
    private readonly SalesMetricsDbContext _db;

    public SignAdminController(IEnvelopeService svc, SalesMetricsDbContext db)
    {
        _svc = svc;
        _db = db;
    }

    /// <summary>
    /// Gets the current user's ID from claims
    /// </summary>
    private int GetCurrentUserId()
    {
        return int.Parse(User.FindFirst("Users_Id")?.Value ?? "0");
    }

    /// <summary>
    /// Gets the current user's role ID from claims
    /// </summary>
    private int GetCurrentRoleId()
    {
        return int.Parse(User.FindFirst("RoleId")?.Value ?? "0");
    }

    /// <summary>
    /// Gets the current user's location ID from claims
    /// </summary>
    private int GetCurrentLocationId()
    {
        return int.Parse(User.FindFirst("LocationId")?.Value ?? "0");
    }

    /// <summary>
    /// Gets the location codes the current user is allowed to access,
    /// based on UserLocationAssignments. Falls back to all locations if
    /// no assignments exist (admin/legacy users).
    /// </summary>
    private async Task<Dictionary<int, (string Code, string Name)>> GetUserAccessibleLocationsAsync(int userId)
    {
        var assignedLocationIds = await _db.UserLocationAssignments
            .Where(ula => ula.UserID == userId && ula.IsActive == "Y")
            .Select(ula => ula.LocationID)
            .ToListAsync();

        if (assignedLocationIds.Any())
        {
            return LocationHelper.Locations
                .Where(loc => assignedLocationIds.Contains(loc.Key))
                .ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
        }

        // No specific assignments — fallback for admins/legacy users
        return LocationHelper.Locations;
    }

    /// <summary>
    /// Checks whether the given location code is within the user's accessible locations.
    /// </summary>
    private async Task<bool> CanAccessLocationAsync(int userId, string? locationCode)
    {
        if (string.IsNullOrWhiteSpace(locationCode)) return true;
        var accessible = await GetUserAccessibleLocationsAsync(userId);
        return accessible.Values.Any(l => l.Code.Equals(locationCode, StringComparison.OrdinalIgnoreCase));
    }

    // /SignAdmin
    public async Task<IActionResult> Index(string? status, string? office, string? scope, int page = 1, int pageSize = 20)
    {
        var userId = GetCurrentUserId();
        var roleId = GetCurrentRoleId();
        var locationId = GetCurrentLocationId();
        var currentLocationCode = HttpContext.Session.GetString("OfficeLocation") ?? "LAX";

        // Default office filter to current session location so users see only their branch
        if (string.IsNullOrWhiteSpace(office))
        {
            office = currentLocationCode;
        }

        // Determine default scope based on role if not explicitly provided
        if (string.IsNullOrWhiteSpace(scope))
        {
            // RoleId 2 = Sales, RoleId 6 = Office Staff - default to "mine"
            // Others default to "all"
            scope = (roleId == 2 || roleId == 6) ? "mine" : "all";
        }

        // Validate the selected office is within user's accessible locations
        var userLocations = await GetUserAccessibleLocationsAsync(userId);
        if (!userLocations.Values.Any(l => l.Code.Equals(office, StringComparison.OrdinalIgnoreCase)))
        {
            office = currentLocationCode; // Reset to session location if invalid
        }

        var (rows, total) = await _svc.SearchAsync(status, office, page, pageSize, userId, scope, roleId, locationId);

        ViewBag.Total = total;
        ViewBag.Status = status;
        ViewBag.Office = office;
        ViewBag.Scope = scope;
        ViewBag.RoleId = roleId;
        ViewBag.UserId = userId;
        ViewBag.CurrentLocationCode = currentLocationCode;

        // Pass only user's accessible locations for the dropdown
        ViewBag.Locations = userLocations;

        return View(rows);
    }

    // /SignAdmin/Details/123
    public async Task<IActionResult> Details(long id)
    {
        var vm = await _svc.GetDetailsAsync(id);
        if (vm == null) return NotFound();

        // Verify user has access to this envelope's location
        var userId = GetCurrentUserId();
        var envelopeLocation = await _db.SignEnvelopes
            .Where(e => e.EnvelopeId == id)
            .Select(e => e.LocationCode)
            .FirstOrDefaultAsync();

        if (!await CanAccessLocationAsync(userId, envelopeLocation))
        {
            TempData["error"] = "You do not have access to envelopes from this branch.";
            return RedirectToAction(nameof(Index));
        }

        return View(vm);
    }

    // GET /SignAdmin/Create
    public async Task<IActionResult> Create()
    {
        var userId = GetCurrentUserId();
        var currentLocationCode = HttpContext.Session.GetString("OfficeLocation") ?? "LAX";

        var userAccessibleLocations = await GetUserAccessibleLocationsAsync(userId);

        var vm = new CreateEnvelopeVm
        {
            ExpiresAtUtc = DateTime.UtcNow.AddDays(14),
            LocationCode = currentLocationCode,
            Templates = await _db.SignTemplates
                .Where(t => t.IsActive)
                .OrderBy(t => t.DisplayName)
                .Select(t => new ValueTuple<string, string>(t.TemplateKey, t.DisplayName))
                .ToListAsync()
        };

        ViewBag.Locations = userAccessibleLocations;
        ViewBag.CurrentLocationCode = currentLocationCode;
        ViewBag.HasMultipleLocations = userAccessibleLocations.Count > 1;

        return View(vm);
    }
        

    // POST /SignAdmin/Create
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(CreateEnvelopeVm vm)
    {
        if (!ModelState.IsValid)
        {
            var userId = GetCurrentUserId();
            var currentLocationCode = HttpContext.Session.GetString("OfficeLocation") ?? "LAX";
            var userAccessibleLocations = await GetUserAccessibleLocationsAsync(userId);

            vm.Templates = await _db.SignTemplates
                .Where(t => t.IsActive)
                .OrderBy(t => t.DisplayName)
                .Select(t => new ValueTuple<string, string>(t.TemplateKey, t.DisplayName))
                .ToListAsync();

            ViewBag.Locations = userAccessibleLocations;
            ViewBag.CurrentLocationCode = currentLocationCode;
            ViewBag.HasMultipleLocations = userAccessibleLocations.Count > 1;

            return View(vm);
        }
        var userId2 = GetCurrentUserId();
        var id = await _svc.CreateAsync(userId2, vm);
        await _svc.SendAsync(id);
        return RedirectToAction(nameof(Details), new { id });
    }

    // POST /SignAdmin/Resend/123
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Resend(long id)
    {
        // Verify user has access to this envelope's location
        var userId = GetCurrentUserId();
        var envelopeLocation = await _db.SignEnvelopes
            .Where(e => e.EnvelopeId == id)
            .Select(e => e.LocationCode)
            .FirstOrDefaultAsync();

        if (!await CanAccessLocationAsync(userId, envelopeLocation))
        {
            TempData["error"] = "You do not have access to envelopes from this branch.";
            return RedirectToAction(nameof(Index));
        }

        await _svc.SendAsync(id);
        TempData["msg"] = "Envelope resent.";
        return RedirectToAction(nameof(Details), new { id });
    }

    // POST /SignAdmin/Void/123
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Void(long id, string? reason)
    {
        // Verify user has access to this envelope's location
        var userId = GetCurrentUserId();
        var envelopeLocation = await _db.SignEnvelopes
            .Where(e => e.EnvelopeId == id)
            .Select(e => e.LocationCode)
            .FirstOrDefaultAsync();

        if (!await CanAccessLocationAsync(userId, envelopeLocation))
        {
            TempData["error"] = "You do not have access to envelopes from this branch.";
            return RedirectToAction(nameof(Index));
        }

        try
        {
            await _svc.VoidEnvelopeAsync(id, userId, reason);
            TempData["msg"] = "Envelope has been voided.";
        }
        catch (InvalidOperationException ex)
        {
            TempData["error"] = ex.Message;
        }
        return RedirectToAction(nameof(Details), new { id });
    }
}
