// Controllers/SignTemplateController.cs
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.FileProviders;
using SalesMetrics.Data;
using SalesMetrics.Domain.Signing;

namespace SalesMetrics.Controllers;

//[Authorize(Roles = "Admin")]
public class SignTemplatesController : Controller
{
    private readonly SalesMetricsDbContext _db;
    private readonly IWebHostEnvironment _env;

    public SignTemplatesController(SalesMetricsDbContext db, IWebHostEnvironment env)
    { _db = db; _env = env; }

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
        return View(new SignTemplate { IsActive = true, DefaultSubject = "Please review and sign" });
    }

    // POST: /SignTemplates/Create
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(SignTemplate model, IFormFile? pdfFile)
    {
        if (!ModelState.IsValid)
        {
            ViewBag.ViewPaths = GetRazorViewPaths();
            return View(model);
        }

        // Upload base PDF for stamping
        if (pdfFile is not null && pdfFile.Length > 0)
        {
            var dir = Path.Combine(_env.ContentRootPath, "Content", "Templates");
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

        //var userId = int.Parse(User.FindFirst("Users_ID").Value);
        //model.CreatedByUsers_ID = userId;

        model.CreatedByUsers_ID = 1; // TODO: current user id
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
        return View(t);
    }

    // POST: /SignTemplates/Edit/{key}
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(string id, SignTemplate model, IFormFile? pdfFile)
    {
        if (id != model.TemplateKey) return BadRequest();

        if (!ModelState.IsValid)
        {
            ViewBag.ViewPaths = GetRazorViewPaths();
            return View(model);
        }

        var t = await _db.SignTemplates.FirstAsync(x => x.TemplateKey == id);

        // basic fields you want editable:
        t.DisplayName = model.DisplayName;
        t.DefaultSubject = model.DefaultSubject;
        t.DefaultMessage = model.DefaultMessage;
        t.RazorViewPath = model.RazorViewPath;
        t.MergeSpecJson = model.MergeSpecJson;
        t.IsActive = model.IsActive;
        t.ModifiedDateUtc = DateTime.UtcNow;
        t.ModifiedByUsers_ID = 1; // TODO current user

        if (pdfFile is not null && pdfFile.Length > 0)
        {
            var dir = Path.Combine(_env.ContentRootPath, "Content", "Templates");
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
            IsActive = false, // Set to inactive by default
            CreatedByUsers_ID = 1, // TODO: current user
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
            var pdfPath = Path.Combine(_env.ContentRootPath, "Content", "Templates", template.PdfFilePath);
            if (System.IO.File.Exists(pdfPath))
            {
                try
                {
                    System.IO.File.Delete(pdfPath);
                }
                catch (Exception ex)
                {
                    // Log error but continue with deletion
                    Console.WriteLine($"Error deleting PDF file: {ex.Message}");
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
        Console.WriteLine($"[GetTemplatePdf] Called with templateKey: {templateKey}");
        Console.WriteLine($"[GetTemplatePdf] ContentRootPath: {_env.ContentRootPath}");

        var template = await _db.SignTemplates.AsNoTracking().FirstOrDefaultAsync(t => t.TemplateKey == templateKey);

        if (template == null)
        {
            Console.WriteLine($"[GetTemplatePdf] Template not found for key: {templateKey}");
            return NotFound("Template not found");
        }

        Console.WriteLine($"[GetTemplatePdf] Template found: {template.DisplayName}");
        Console.WriteLine($"[GetTemplatePdf] PdfFilePath from DB: {template.PdfFilePath}");

        if (string.IsNullOrWhiteSpace(template.PdfFilePath))
        {
            Console.WriteLine($"[GetTemplatePdf] PdfFilePath is empty");
            return NotFound("PDF file path is empty");
        }

        var filePath = Path.Combine(_env.ContentRootPath, "Content", "Templates", template.PdfFilePath);
        Console.WriteLine($"[GetTemplatePdf] Full file path: {filePath}");
        Console.WriteLine($"[GetTemplatePdf] File exists: {System.IO.File.Exists(filePath)}");

        if (!System.IO.File.Exists(filePath))
        {
            Console.WriteLine($"[GetTemplatePdf] File not found at path: {filePath}");
            // Try to list files in the directory to help diagnose
            var directory = Path.Combine(_env.ContentRootPath, "Content", "Templates");
            if (Directory.Exists(directory))
            {
                var files = Directory.GetFiles(directory);
                Console.WriteLine($"[GetTemplatePdf] Files in Templates directory: {string.Join(", ", files.Select(Path.GetFileName))}");
            }
            else
            {
                Console.WriteLine($"[GetTemplatePdf] Templates directory doesn't exist: {directory}");
            }
            return NotFound("PDF file not found on disk");
        }

        var fileBytes = await System.IO.File.ReadAllBytesAsync(filePath);
        Console.WriteLine($"[GetTemplatePdf] File read successfully. Size: {fileBytes.Length} bytes");

        Response.Headers.Add("Content-Disposition", "inline");
        return File(fileBytes, "application/pdf");
    }

    private List<string> GetRazorViewPaths()
    {
        // enumerate /Views/SignTemplates/*.cshtml
        var root = Path.Combine(_env.ContentRootPath, "Views", "SignTemplates");
        if (!Directory.Exists(root)) return new();

        // Exclude UI pages (Create, Edit, Index, Delete, etc.) - only include document templates
        var excludedPrefixes = new[] { "Create", "Edit", "Index", "Delete", "Details" };

        return Directory.GetFiles(root, "*.cshtml")
            .Select(p => Path.GetFileName(p))
            .Where(fileName => !excludedPrefixes.Any(prefix =>
                fileName.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)))
            .Where(fileName => !fileName.EndsWith(".backup", StringComparison.OrdinalIgnoreCase))
            .Select(fileName => "/Views/SignTemplates/" + fileName)
            .OrderBy(x => x)
            .ToList();
    }
}
