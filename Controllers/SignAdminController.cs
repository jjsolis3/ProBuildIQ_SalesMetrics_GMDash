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
    /// Gets the current user's active location ID from session (updated when user switches branch).
    /// Falls back to the claim value set at login.
    /// </summary>
    private int GetCurrentLocationId()
    {
        // Session is updated whenever the user switches branch in the header dropdown.
        // Claims are only set at login and never change, so session must be the source of truth here.
        if (int.TryParse(HttpContext.Session.GetString("LocationId"), out var sessionId) && sessionId > 0)
            return sessionId;
        return int.Parse(User.FindFirst("LocationId")?.Value ?? "0");
    }

    // /SignAdmin
    public async Task<IActionResult> Index(string? status, string? office, string? scope, int page = 1, int pageSize = 2000)
    {
        var userId = GetCurrentUserId();
        var roleId = GetCurrentRoleId();
        var locationId = GetCurrentLocationId(); // session-based, respects branch switches

        // Default: every user sees their currently selected location's envelopes.
        // Users can explicitly choose "mine" or (if authorized) "all" via the filter buttons.
        if (string.IsNullOrWhiteSpace(scope))
            scope = "branch";

        // DataTables handles client-side pagination/search/sort so we load all rows for the
        // current filter (status/scope/office).  The 2000 ceiling is a generous safety cap.
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
        var userId = GetCurrentUserId();
        var currentLocationCode = HttpContext.Session.GetString("OfficeLocation") ?? "LAX";
        var currentLocationId = GetCurrentLocationId();

        // Get user's assigned locations from database
        var userLocations = await _db.UserLocationAssignments
            .Where(ula => ula.UserID == userId && ula.IsActive == "Y")
            .Select(ula => ula.LocationID)
            .ToListAsync();

        // Filter LocationHelper.Locations to only show user's assigned locations
        Dictionary<int, (string Code, string Name)> userAccessibleLocations;

        if (userLocations.Any())
        {
            // User has specific location assignments - filter to those
            userAccessibleLocations = LocationHelper.Locations
                .Where(loc => userLocations.Contains(loc.Key))
                .ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
        }
        else
        {
            // No specific assignments found - show all locations (fallback for admins/legacy users)
            userAccessibleLocations = LocationHelper.Locations;
        }

        var vm = new CreateEnvelopeVm
        {
            ExpiresAtUtc = DateTime.UtcNow.AddDays(14),
            LocationCode = currentLocationCode, // Pre-populate with current location
            Templates = await _db.SignTemplates
                .Where(t => t.IsActive)
                .OrderBy(t => t.DisplayName)
                .Select(t => new ValueTuple<string, string, string>(t.TemplateKey, t.DisplayName, t.TemplateType))
                .ToListAsync()
        };

        // Pass filtered branch locations for dropdown
        ViewBag.Locations = userAccessibleLocations;
        ViewBag.CurrentLocationCode = currentLocationCode;
        ViewBag.HasMultipleLocations = userAccessibleLocations.Count > 1;

        return View(vm);
    }
        

    // POST /SignAdmin/Create
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(CreateEnvelopeVm vm)
    {
        // Communication envelopes don't need a template — remove the spurious ModelState error
        if (vm.EnvelopeType == "Communication" && string.IsNullOrWhiteSpace(vm.TemplateKey))
            ModelState.Remove(nameof(vm.TemplateKey));

        // Communication envelopes require a message body since that IS the communication
        if (vm.EnvelopeType == "Communication" && string.IsNullOrWhiteSpace(vm.MessageBody))
            ModelState.AddModelError(nameof(vm.MessageBody), "A message body is required for Communication envelopes.");

        if (!ModelState.IsValid)
        {
            var userId = GetCurrentUserId();
            var currentLocationCode = HttpContext.Session.GetString("OfficeLocation") ?? "LAX";

            // Get user's assigned locations
            var userLocations = await _db.UserLocationAssignments
                .Where(ula => ula.UserID == userId && ula.IsActive == "Y")
                .Select(ula => ula.LocationID)
                .ToListAsync();

            Dictionary<int, (string Code, string Name)> userAccessibleLocations;
            if (userLocations.Any())
            {
                userAccessibleLocations = LocationHelper.Locations
                    .Where(loc => userLocations.Contains(loc.Key))
                    .ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
            }
            else
            {
                userAccessibleLocations = LocationHelper.Locations;
            }

            vm.Templates = await _db.SignTemplates
                .Where(t => t.IsActive)
                .OrderBy(t => t.DisplayName)
                .Select(t => new ValueTuple<string, string, string>(t.TemplateKey, t.DisplayName, t.TemplateType))
                .ToListAsync();

            // Repopulate filtered branch locations for dropdown
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

    // GET /SignAdmin/Edit/123
    [HttpGet]
    public async Task<IActionResult> Edit(long id)
    {
        var details = await _svc.GetDetailsAsync(id);
        if (details == null) return NotFound();

        if (details.Status == "Completed" || details.Status == "Voided")
        {
            TempData["error"] = "Completed or voided envelopes cannot be edited.";
            return RedirectToAction(nameof(Details), new { id });
        }

        // Strip the old "Envelope for: ..." block that was auto-appended before Feb 2026.
        // Regex matches from the first blank-line-then-"Envelope for:" to end of string.
        var messageBody = details.MessageBody;
        if (!string.IsNullOrWhiteSpace(messageBody))
        {
            messageBody = System.Text.RegularExpressions.Regex.Replace(
                messageBody,
                @"\n*Envelope for:\s*\n[\s\S]*$",
                "").TrimEnd();
            if (string.IsNullOrWhiteSpace(messageBody)) messageBody = null;
        }

        var vm = new EditEnvelopeVm
        {
            EnvelopeId   = details.EnvelopeId,
            Subject      = details.Subject,
            MessageBody  = messageBody,
            Status       = details.Status,
            ExpiresAtUtc = details.ExpiresAtUtc,
            PropertyName = details.PropertyName,
            OrderNumber  = details.OrderNumber,
            UnitNumber   = details.UnitNumber,
            LocationCode = details.LocationCode,
            Recipients   = details.Recipients.Select(r => new EditRecipientVm
            {
                RecipientId = r.RecipientId,
                Role        = r.Role,
                SignerOrder  = r.SignerOrder,
                FullName    = r.FullName,
                Email       = r.Email,
                Phone       = r.Phone,
                HasSigned   = r.SignedAtUtc.HasValue
            }).ToList()
        };

        return View(vm);
    }

    // POST /SignAdmin/Edit/123
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(long id, EditEnvelopeVm vm)
    {
        vm.EnvelopeId = id;
        try
        {
            var userId = GetCurrentUserId();
            await _svc.EditEnvelopeAsync(vm, userId);
            TempData["msg"] = "Envelope updated successfully.";
        }
        catch (InvalidOperationException ex)
        {
            TempData["error"] = ex.Message;
        }
        return RedirectToAction(nameof(Details), new { id });
    }

    // POST /SignAdmin/Resend/123
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Resend(long id)
    {
        // If the envelope is Expired, auto-extend expiry by 14 days before resending
        var details = await _svc.GetDetailsAsync(id);
        if (details?.Status == "Expired")
        {
            var userId = GetCurrentUserId();
            var newExpiry = DateTime.UtcNow.AddDays(14);
            await _svc.EditEnvelopeAsync(new EditEnvelopeVm
            {
                EnvelopeId   = id,
                Subject      = details.Subject,
                MessageBody  = details.MessageBody,
                PropertyName = details.PropertyName,
                OrderNumber  = details.OrderNumber,
                UnitNumber   = details.UnitNumber,
                ExpiresAtUtc = newExpiry,
                Status       = details.Status,
                Recipients   = details.Recipients.Select(r => new EditRecipientVm
                {
                    RecipientId = r.RecipientId,
                    Role        = r.Role,
                    SignerOrder  = r.SignerOrder,
                    FullName    = r.FullName,
                    Email       = r.Email,
                    Phone       = r.Phone,
                    HasSigned   = r.SignedAtUtc.HasValue
                }).ToList()
            }, userId);
            TempData["msg"] = $"Envelope was expired. Expiry extended to {newExpiry.ToLocalTime():MMM d, yyyy} and emails have been resent.";
        }
        else
        {
            TempData["msg"] = "Envelope resent.";
        }

        await _svc.SendAsync(id);
        return RedirectToAction(nameof(Details), new { id });
    }

    // POST /SignAdmin/AdminSkipTenant/123
    // Allows a staff admin to skip the tenant signature from the Details page,
    // but only after at least one Property Staff/Manager has already signed.
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> AdminSkipTenant(long id)
    {
        try
        {
            var staffName = User.FindFirst("FullName")?.Value
                         ?? User.FindFirst(System.Security.Claims.ClaimTypes.Name)?.Value
                         ?? "Staff";

            await _svc.MarkTenantSkippedAsync(id, staffName);
            var (isComplete, _) = await _svc.TryFinalizeEnvelopeAsync(id);
            TempData["msg"] = isComplete
                ? "Tenant skipped. Envelope is now complete and notifications have been sent."
                : "Tenant skipped. Envelope will complete once all other recipients have signed.";
        }
        catch (InvalidOperationException ex)
        {
            TempData["error"] = ex.Message;
        }
        catch (Microsoft.EntityFrameworkCore.DbUpdateException ex)
        {
            var inner = ex.InnerException?.Message ?? ex.Message;
            TempData["error"] = $"Database error while skipping tenant: {inner}";
        }
        catch (Exception ex)
        {
            TempData["error"] = $"An unexpected error occurred: {ex.Message}";
        }
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

    // POST /SignAdmin/UpdateFields/123
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateFields(long id, [FromForm] Dictionary<string, string> fields)
    {
        if (fields != null && fields.Count > 0)
        {
            // Strip the hidden envelopeId entry that the form submits alongside the field values
            fields.Remove("envelopeId");
            await _svc.UpdateFieldsAsync(id, fields);
            TempData["msg"] = "Document fields updated.";
        }
        return RedirectToAction(nameof(Details), new { id });
    }

    // POST /SignAdmin/MarkOffline/123
    // Staff uploads the physically-signed/scanned PDF and marks the envelope as complete.
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> MarkOffline(long id, IFormFile? signedPdf, string? staffNote)
    {
        if (signedPdf == null || signedPdf.Length == 0)
        {
            TempData["error"] = "Please select a PDF file to upload.";
            return RedirectToAction(nameof(Details), new { id });
        }

        // Validate file type (basic content-type check; rely on extension too)
        var ct = signedPdf.ContentType ?? "";
        var ext = Path.GetExtension(signedPdf.FileName).ToLowerInvariant();
        if (!ct.Contains("pdf", StringComparison.OrdinalIgnoreCase) && ext != ".pdf")
        {
            TempData["error"] = "Only PDF files are accepted.";
            return RedirectToAction(nameof(Details), new { id });
        }

        // 25 MB safety cap
        if (signedPdf.Length > 25 * 1024 * 1024)
        {
            TempData["error"] = "The uploaded file exceeds the 25 MB limit.";
            return RedirectToAction(nameof(Details), new { id });
        }

        try
        {
            var userId    = GetCurrentUserId();
            var staffName = User.FindFirst("FullName")?.Value
                         ?? User.FindFirst(System.Security.Claims.ClaimTypes.Name)?.Value
                         ?? $"User #{userId}";

            await _svc.MarkOfflineCompleteAsync(id, signedPdf, staffNote, userId, staffName);
            TempData["msg"] = "Envelope has been marked as completed with the uploaded signed PDF.";
        }
        catch (InvalidOperationException ex)
        {
            TempData["error"] = ex.Message;
        }

        return RedirectToAction(nameof(Details), new { id });
    }
}
