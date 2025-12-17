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

        // optional: upload base PDF
        if (pdfFile is not null && pdfFile.Length > 0)
        {
            var dir = Path.Combine(_env.ContentRootPath, "Content", "Templates");
            Directory.CreateDirectory(dir);
            var fileName = Path.GetFileName(pdfFile.FileName);
            var dest = Path.Combine(dir, fileName);
            await using var fs = System.IO.File.Create(dest);
            await pdfFile.CopyToAsync(fs);
            // store the file name in RazorViewPath or add a FilePath column on SignTemplate if you have it
            // if you already have FilePath column in SignTemplate, use that instead of RazorViewPath
            // model.FilePath = fileName;
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
            var fileName = Path.GetFileName(pdfFile.FileName);
            var dest = Path.Combine(dir, fileName);
            await using var fs = System.IO.File.Create(dest);
            await pdfFile.CopyToAsync(fs);
            // t.FilePath = fileName; // if you have FilePath column
        }

        await _db.SaveChangesAsync();
        return RedirectToAction(nameof(Index));
    }

    private List<string> GetRazorViewPaths()
    {
        // enumerate /Views/SignTemplates/*.cshtml
        var root = Path.Combine(_env.ContentRootPath, "Views", "SignTemplates");
        if (!Directory.Exists(root)) return new();
        return Directory.GetFiles(root, "*.cshtml").Select(p =>
            "/Views/SignTemplates/" + Path.GetFileName(p)).OrderBy(x => x).ToList();
    }
}
