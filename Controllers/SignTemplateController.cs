// Controllers/SignTemplateController.cs
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SalesMetrics.Data;
using SalesMetrics.Domain.Signing;
using SalesMetrics.Models.Signing;
using SalesMetrics.Services.Helpers;

namespace SalesMetrics.Controllers;

public class SignTemplatesController : Controller
{
    private readonly SalesMetricsDbContext _db;
    private readonly IWebHostEnvironment _env;
    private readonly ILogger<SignTemplatesController> _logger;

    public SignTemplatesController(SalesMetricsDbContext db, IWebHostEnvironment env, ILogger<SignTemplatesController> logger)
    { _db = db; _env = env; _logger = logger; }

    private int GetCurrentUserId()
        => int.TryParse(User.FindFirst("Users_Id")?.Value, out var id) ? id : 0;

    // GET: /SignTemplates
    public async Task<IActionResult> Index()
    {
        var rows = await _db.SignTemplates.AsNoTracking()
            .OrderBy(t => t.DisplayName).ToListAsync();
        return View(rows);
    }

    // GET: /SignTemplates/Create
    public IActionResult Create()
    {
        ViewBag.ViewPaths = GetRazorViewPaths();
        ViewBag.TemplateMetadata = TemplateMetadata.GetTemplateMetadata(); // NEW: Pass template metadata
        ViewBag.Locations = LocationHelper.Locations; // for branch checkbox group
        return View(new SignTemplate { IsActive = true, DefaultSubject = "Please review and sign" });
    }

    // POST: /SignTemplates/Create
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(SignTemplate model, IFormFile? pdfFile, string[]? selectedLocationCodes)
    {
        if (!ModelState.IsValid)
        {
            ViewBag.ViewPaths = GetRazorViewPaths();
            ViewBag.Locations = LocationHelper.Locations;
            return View(model);
        }

        // Empty selection (or all unchecked) = global / visible to all branches
        model.LocationCodes = (selectedLocationCodes != null && selectedLocationCodes.Length > 0)
            ? string.Join(",", selectedLocationCodes)
            : null;

        // Upload base PDF for stamping
        if (pdfFile is not null && pdfFile.Length > 0)
        {
            // Use wwwroot for persistent storage (works with single-file deployment)
            var dir = Path.Combine(_env.WebRootPath, "Files", "Templates");
            Directory.CreateDirectory(dir);

            // Generate unique filename: templateKey_timestamp.pdf
            var timestamp = DateTime.UtcNow.ToString("yyyyMMddHHmmss");
            var fileName = $"{model.TemplateKey}_{timestamp}.pdf";
            var dest = Path.Combine(dir, fileName);

            await using var fs = System.IO.File.Create(dest);
            await pdfFile.CopyToAsync(fs);

            // Store relative path for PdfService to use
            model.PdfFilePath = fileName;
        }

        model.CreatedDateUtc = DateTime.UtcNow;

        model.CreatedByUsers_ID = GetCurrentUserId();
        _db.SignTemplates.Add(model);
        await _db.SaveChangesAsync();
        return RedirectToAction(nameof(Index));
    }

    // GET: /SignTemplates/Edit/{key}
    public async Task<IActionResult> Edit(string id)
    {
        var t = await _db.SignTemplates.FindAsync(id);
        if (t is null) return NotFound();
        ViewBag.ViewPaths = GetRazorViewPaths();
        ViewBag.Locations = LocationHelper.Locations;
        return View(t);
    }

    // POST: /SignTemplates/Edit/{key}
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(string id, SignTemplate model, IFormFile? pdfFile, string[]? selectedLocationCodes)
    {
        if (id != model.TemplateKey) return BadRequest();

        if (!ModelState.IsValid)
        {
            ViewBag.ViewPaths = GetRazorViewPaths();
            ViewBag.Locations = LocationHelper.Locations;
            return View(model);
        }

        var t = await _db.SignTemplates.FirstAsync(x => x.TemplateKey == id);

        // basic fields you want editable:
        t.DisplayName            = model.DisplayName;
        t.DefaultSubject         = model.DefaultSubject;
        t.DefaultMessage         = model.DefaultMessage;
        t.RazorViewPath          = model.RazorViewPath;
        t.HtmlBodyContent        = model.HtmlBodyContent;
        t.MergeSpecJson          = model.MergeSpecJson;
        t.IsActive               = model.IsActive;
        t.RequiresTenantSection  = model.RequiresTenantSection;
        t.TemplateType           = model.TemplateType;
        t.LocationCodes          = (selectedLocationCodes != null && selectedLocationCodes.Length > 0)
                                       ? string.Join(",", selectedLocationCodes)
                                       : null;
        t.ModifiedDateUtc        = DateTime.UtcNow;
        t.ModifiedByUsers_ID     = GetCurrentUserId();

        if (pdfFile is not null && pdfFile.Length > 0)
        {
            // Use wwwroot for persistent storage (works with single-file deployment)
            var dir = Path.Combine(_env.WebRootPath, "Files", "Templates");
            Directory.CreateDirectory(dir);

            // Generate unique filename: templateKey_timestamp.pdf
            var timestamp = DateTime.UtcNow.ToString("yyyyMMddHHmmss");
            var fileName = $"{t.TemplateKey}_{timestamp}.pdf";
            var dest = Path.Combine(dir, fileName);

            await using var fs = System.IO.File.Create(dest);
            await pdfFile.CopyToAsync(fs);

            // Update PDF file path
            t.PdfFilePath = fileName;
        }

        await _db.SaveChangesAsync();
        return RedirectToAction(nameof(Index));
    }

    // POST: /SignTemplates/Duplicate
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Duplicate(string templateKey)
    {
        var original = await _db.SignTemplates.FirstOrDefaultAsync(t => t.TemplateKey == templateKey);
        if (original == null) return NotFound();

        // Create a copy with new key
        var copy = new SignTemplate
        {
            TemplateKey = $"{original.TemplateKey}-copy",
            DisplayName = $"{original.DisplayName} (Copy)",
            DefaultSubject = original.DefaultSubject,
            DefaultMessage = original.DefaultMessage,
            RazorViewPath = original.RazorViewPath,
            MergeSpecJson = original.MergeSpecJson,
            LocationCodes = original.LocationCodes,
            IsActive = false, // Set to inactive by default
            CreatedByUsers_ID = GetCurrentUserId(),
            CreatedDateUtc = DateTime.UtcNow
        };

        _db.SignTemplates.Add(copy);
        await _db.SaveChangesAsync();

        TempData["SuccessMessage"] = $"Template '{original.DisplayName}' duplicated successfully.";
        return RedirectToAction(nameof(Edit), new { id = copy.TemplateKey });
    }

    // POST: /SignTemplates/Delete
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(string templateKey)
    {
        var template = await _db.SignTemplates.FirstOrDefaultAsync(t => t.TemplateKey == templateKey);
        if (template == null) return NotFound();

        // Check if template is being used by any envelopes
        var envelopeCount = await _db.SignEnvelopes.CountAsync(e => e.TemplateKey == templateKey);
        if (envelopeCount > 0)
        {
            TempData["ErrorMessage"] = $"Cannot delete template '{template.DisplayName}' because it is being used by {envelopeCount} envelope(s). Please deactivate it instead.";
            return RedirectToAction(nameof(Index));
        }

        // Delete associated PDF file if exists
        if (!string.IsNullOrWhiteSpace(template.PdfFilePath))
        {
            var pdfPath = Path.Combine(_env.WebRootPath, "Files", "Templates", template.PdfFilePath);
            if (System.IO.File.Exists(pdfPath))
            {
                try
                {
                    System.IO.File.Delete(pdfPath);
                }
                catch (Exception ex)
                {
                    // Log error but continue with deletion
                    _logger.LogWarning(ex, "Error deleting PDF file for template");
                }
            }
        }

        _db.SignTemplates.Remove(template);
        await _db.SaveChangesAsync();

        TempData["SuccessMessage"] = $"Template '{template.DisplayName}' deleted successfully.";
        return RedirectToAction(nameof(Index));
    }

    // GET: /SignTemplates/GetTemplatePdf/{id}
    public async Task<IActionResult> GetTemplatePdf(string id)
    {
        var templateKey = id; // Route parameter is 'id', but we use it as templateKey
        _logger.LogDebug("[GetTemplatePdf] Called with templateKey: {TemplateKey}", templateKey);
        _logger.LogDebug("[GetTemplatePdf] ContentRootPath: {ContentRootPath}", _env.ContentRootPath);

        var template = await _db.SignTemplates.AsNoTracking().FirstOrDefaultAsync(t => t.TemplateKey == templateKey);

        if (template == null)
        {
            _logger.LogWarning("[GetTemplatePdf] Template not found for key: {TemplateKey}", templateKey);
            return NotFound("Template not found");
        }

        _logger.LogDebug("[GetTemplatePdf] Template found: {DisplayName}", template.DisplayName);
        _logger.LogDebug("[GetTemplatePdf] PdfFilePath from DB: {PdfFilePath}", template.PdfFilePath);

        if (string.IsNullOrWhiteSpace(template.PdfFilePath))
        {
            _logger.LogWarning("[GetTemplatePdf] PdfFilePath is empty for template: {TemplateKey}", templateKey);
            return NotFound("PDF file path is empty");
        }

        var filePath = Path.Combine(_env.WebRootPath, "Files", "Templates", template.PdfFilePath);
        _logger.LogDebug("[GetTemplatePdf] Full file path: {FilePath}", filePath);
        _logger.LogDebug("[GetTemplatePdf] File exists: {FileExists}", System.IO.File.Exists(filePath));

        if (!System.IO.File.Exists(filePath))
        {
            _logger.LogWarning("[GetTemplatePdf] File not found at path: {FilePath}", filePath);
            // Try to list files in the directory to help diagnose
            var directory = Path.Combine(_env.WebRootPath, "Files", "Templates");
            if (Directory.Exists(directory))
            {
                var files = Directory.GetFiles(directory);
                _logger.LogWarning("[GetTemplatePdf] Files in Templates directory: {Files}", string.Join(", ", files.Select(Path.GetFileName)));
            }
            else
            {
                _logger.LogWarning("[GetTemplatePdf] Templates directory doesn't exist: {Directory}", directory);
            }
            return NotFound("PDF file not found on disk");
        }

        var fileBytes = await System.IO.File.ReadAllBytesAsync(filePath);
        _logger.LogDebug("[GetTemplatePdf] File read successfully. Size: {FileSize} bytes", fileBytes.Length);

        Response.Headers.Add("Content-Disposition", "inline");
        return File(fileBytes, "application/pdf");
    }

    private List<string> GetRazorViewPaths()
    {
        // IMPORTANT: In single-file deployment, Views are compiled into the assembly
        // and not accessible as physical files. We return a hardcoded list of known templates.
        // When adding new templates, add them to this list.

        var templates = new List<string>
        {
            "/Views/SignTemplates/OccupiedRelease.cshtml",
            "/Views/SignTemplates/TenantConsent.cshtml"
        };

        // In development, optionally scan filesystem for any additional templates
        if (_env.IsDevelopment())
        {
            try
            {
                var root = Path.Combine(_env.ContentRootPath, "Views", "SignTemplates");
                if (Directory.Exists(root))
                {
                    var excludedPrefixes = new[] { "Create", "Edit", "Index", "Delete", "Details" };
                    var discoveredTemplates = Directory.GetFiles(root, "*.cshtml")
                        .Select(p => Path.GetFileName(p))
                        .Where(fileName => !excludedPrefixes.Any(prefix =>
                            fileName.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)))
                        .Where(fileName => !fileName.EndsWith(".backup", StringComparison.OrdinalIgnoreCase))
                        .Select(fileName => "/Views/SignTemplates/" + fileName)
                        .ToList();

                    // Add any newly discovered templates not in hardcoded list
                    templates = templates.Union(discoveredTemplates).OrderBy(x => x).ToList();
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "[GetRazorViewPaths] Error scanning filesystem");
            }
        }

        return templates.OrderBy(x => x).ToList();
    }
}
