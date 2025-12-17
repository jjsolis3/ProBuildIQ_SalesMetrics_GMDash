// Controllers/SignAdminController.cs
using Microsoft.AspNetCore.Mvc;
using SalesMetrics.Services.Signing;
using SalesMetrics.Models.Signing;
using SalesMetrics.Data;
using System.Security.AccessControl;
using Microsoft.EntityFrameworkCore;

namespace SalesMetrics.Controllers;

public class SignAdminController : Controller
{
    private readonly IEnvelopeService _svc;
    private readonly SalesMetricsDbContext _db;

    public SignAdminController(IEnvelopeService svc, SalesMetricsDbContext db)
    {
        _svc = svc;
        _db = db;
    }

    // /SignAdmin
    public async Task<IActionResult> Index(string? status, string? office, int page = 1, int pageSize = 20)
    {
        var (rows, total) = await _svc.SearchAsync(status, office, page, pageSize);
        ViewBag.Total = total;
        ViewBag.Status = status;
        ViewBag.Office = office;
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
            return View(vm);
        }
        var userId = 1; // TODO: from session/claims
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
}
