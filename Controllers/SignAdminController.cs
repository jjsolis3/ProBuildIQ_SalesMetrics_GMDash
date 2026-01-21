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

    // /SignAdmin
    public async Task<IActionResult> Index(string? status, string? office, string? scope, int page = 1, int pageSize = 20)
    {
        var userId = GetCurrentUserId();
        var roleId = GetCurrentRoleId();
        var locationId = GetCurrentLocationId();

        // Determine default scope based on role if not explicitly provided
        if (string.IsNullOrWhiteSpace(scope))
        {
            // RoleId 2 = Sales, RoleId 6 = Office Staff - default to "mine"
            // Others default to "all"
            scope = (roleId == 2 || roleId == 6) ? "mine" : "all";
        }

        var (rows, total) = await _svc.SearchAsync(status, office, page, pageSize, userId, scope, roleId, locationId);

        ViewBag.Total = total;
        ViewBag.Status = status;
        ViewBag.Office = office;
        ViewBag.Scope = scope;
        ViewBag.RoleId = roleId;
        ViewBag.UserId = userId;

        // Pass location options for dropdown
        ViewBag.Locations = LocationHelper.Locations;

        return View(rows);
    }

    // /SignAdmin/Details/123
    public async Task<IActionResult> Details(long id)
    {
        var vm = await _svc.GetDetailsAsync(id);
        if (vm == null) return NotFound();
        return View(vm);
    }

    // GET /SignAdmin/Create
    public async Task<IActionResult> Create()
    {
        var vm = new CreateEnvelopeVm
        {
            ExpiresAtUtc = DateTime.UtcNow.AddDays(14),
            Templates = await _db.SignTemplates
                .Where(t => t.IsActive)
                .OrderBy(t => t.DisplayName)
                .Select(t => new ValueTuple<string, string>(t.TemplateKey, t.DisplayName))
                .ToListAsync()
        };

        // Pass branch locations for dropdown
        ViewBag.Locations = LocationHelper.Locations;

        return View(vm);
    }
        

    // POST /SignAdmin/Create
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(CreateEnvelopeVm vm)
    {
        if (!ModelState.IsValid)
        {
            vm.Templates = await _db.SignTemplates
                .Where(t => t.IsActive)
                .OrderBy(t => t.DisplayName)
                .Select(t => new ValueTuple<string, string>(t.TemplateKey, t.DisplayName))
                .ToListAsync();

            // Repopulate branch locations for dropdown
            ViewBag.Locations = LocationHelper.Locations;

            return View(vm);
        }
        var userId = GetCurrentUserId();
        var id = await _svc.CreateAsync(userId, vm);
        await _svc.SendAsync(id);
        return RedirectToAction(nameof(Details), new { id });
    }

    // POST /SignAdmin/Resend/123
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Resend(long id)
    {
        await _svc.SendAsync(id);
        TempData["msg"] = "Envelope resent.";
        return RedirectToAction(nameof(Details), new { id });
    }

    // POST /SignAdmin/Void/123
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Void(long id, string? reason)
    {
        try
        {
            var userId = GetCurrentUserId();
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
