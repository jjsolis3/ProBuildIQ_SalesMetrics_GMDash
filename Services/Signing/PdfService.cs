// Services/Signing/PdfService.cs
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using PdfSharpCore.Drawing;
using PdfSharpCore.Pdf;
using PdfSharpCore.Pdf.IO;
using SalesMetrics.Data;
using SalesMetrics.Utilities.Security;

namespace SalesMetrics.Services.Signing;

public sealed class PdfService : IPdfService
{
    private readonly SalesMetricsDbContext _db;
    private readonly IErpMergeService _merge;

    public PdfService(SalesMetricsDbContext db, IErpMergeService merge)
    {
        _db = db; _merge = merge;
    }

    public async Task<(byte[] bytes, string storagePath, byte[] sha256)> RenderAndSealAsync(long envelopeId)
    {
        // 1) Load envelope + recipients
        var env = await _db.SignEnvelopes
            .Include(e => e.Recipients)
            .AsNoTracking()
            .FirstAsync(e => e.EnvelopeId == envelopeId);

        var tenant = env.Recipients.FirstOrDefault(r => r.Role == "Tenant") ?? env.Recipients.First();
        var manager = env.Recipients.FirstOrDefault(r => r.Role == "Manager");

        // 2) Gather data to stamp (replace placeholders with ERP values)
        var propertyName = await _merge.GetPropertyNameAsync(env.PropertyID) ?? "";
        var installDate = DateTime.UtcNow.ToString("MM/dd/yyyy");   // TODO: from ERP if available
        var unitNumber = "Unit 123";                               // TODO
        var staffName = manager?.FullName ?? "Property Staff";    // from recipient or ERP
        var staffSignedAt = manager?.SignedAtUtc?.ToLocalTime().ToString("MM/dd/yyyy") ?? "";
        var tenantName = tenant.FullName;
        var tenantPhone = tenant.Phone ?? "";

        // Signature images (saved earlier by EnvelopeService)
        string? tenantSigWeb = tenant.SignatureImagePath;     // like "/Files/Sign/2025/09/sig-123.png"
        string? tenantSigFs = tenantSigWeb is null ? null :
            Path.Combine("wwwroot", tenantSigWeb.TrimStart('/')
                .Replace("/", Path.DirectorySeparatorChar.ToString()));

        // 3) Open the base PDF (your uploaded form)
        var templatePath = Path.Combine("Content", "Templates", "SF_OccupiedReleaseForm_1.4.pdf");
        if (!File.Exists(templatePath))
            throw new FileNotFoundException("Base PDF template not found", templatePath);

        using var doc = PdfReader.Open(templatePath, PdfDocumentOpenMode.Modify);
        var page = doc.Pages[0];
        using var gfx = XGraphics.FromPdfPage(page);
        
        // 4) Fonts & brushes
        var font = new XFont("Roboto", 11, XFontStyle.Regular);
        var fontBold = new XFont("Roboto", 12, XFontStyle.Bold);
        var brush = XBrushes.Black;

        // 5) Coordinates (points; 1pt = 1/72in; origin = bottom-left)
        //    Start with approximations; adjust once by eye.
        //    Tip: temporarily call DrawMarker(...) to find exact positions.

        float left = 72f;            // 1 inch from left
        float yTop = 720f;           // ~8.6 inches from bottom
                
        // tiny POCO to register variant reading from config
        var debug = _db.Database.GetService<IConfiguration>()["Pdf:DebugMarkers"]?.Equals("true", StringComparison.OrdinalIgnoreCase) == true;
        if (debug)
        {
            DrawGrid(gfx, page, 1.0); // 1-inch grid

            // Drop some test markers where you THINK fields should land
            DrawMarker(gfx, In(1.0), In(8.6)); DrawLabel(gfx, "PropertyName?", In(1.0), In(8.6));
            DrawMarker(gfx, In(1.0), In(8.35)); DrawLabel(gfx, "InstallDate?", In(1.0), In(8.35));
            DrawMarker(gfx, In(1.0), In(8.1)); DrawLabel(gfx, "Unit #?", In(1.0), In(8.1));

            // Tenant block guesses
            DrawMarker(gfx, In(1.0), In(5.8)); DrawLabel(gfx, "TenantName?", In(1.0), In(5.8));
            DrawMarker(gfx, In(1.0), In(5.55)); DrawLabel(gfx, "TenantPhone?", In(1.0), In(5.55));

            // Signature box guess (x, y of lower-left corner of image)
            DrawMarker(gfx, In(1.0), In(4.9)); DrawLabel(gfx, "Tenant Sig", In(1.0), In(4.9));
        }
        // Note: PDF origin is bottom-left, so Y coordinates are "inches from bottom"

        // after
        DrawText(gfx, fontBold, XBrushes.Black, propertyName, In(1.0), In(8.6));

        // Property Staff section (example)
        DrawText(gfx, fontBold, brush, $"Property: {propertyName}", left, yTop);
        DrawText(gfx, font, brush, $"Installation Date: {installDate}", left, yTop - 18);
        DrawText(gfx, font, brush, $"Unit #: {unitNumber}", left, yTop - 36);
        DrawText(gfx, font, brush, $"Property Staff: {staffName}", left, yTop - 54);
        DrawText(gfx, font, brush, $"Date Signed: {staffSignedAt}", left, yTop - 72);

        // Tenant section (example)
        float yTenant = 420f;
        DrawText(gfx, fontBold, brush, $"Tenant: {tenantName}", left, yTenant);
        DrawText(gfx, font, brush, $"Tenant Phone: {tenantPhone}", left, yTenant - 18);

        // Tenant signature image (drawn). Scale to a nice size.
        //if (tenantSigFs != null && File.Exists(tenantSigFs))
        //{
        //    using var fs = File.OpenRead(tenantSigFs);
        //    using var img = XImage.FromStream(() => fs); // PdfSharpCore expects a Func<Stream>
        //    double sigW = 180, sigH = 60;                // width/height in points
        //    gfx.DrawImage(img, left, yTenant - 80, sigW, sigH);
        //}
        if (tenantSigFs != null && File.Exists(tenantSigFs))
        {
            using var fs = File.OpenRead(tenantSigFs);
            using var img = XImage.FromStream(() => fs);
            double sigW = In(2.5);  // 2.5 inches wide
            double sigH = In(0.9);  // 0.9 inches tall
            gfx.DrawImage(img, In(1.0), In(4.9), sigW, sigH);
        }


        // Optional: staff signature too (if you capture it similarly)
        // var staffSigFs = ...
        // gfx.DrawImage(...);

        // Optional: audit footer/hash (once you compute final bytes, you can re-open and stamp hash)
        // Here we add a placeholder text (hash added after save is tricky). You can also write the
        // envelope/recipient IDs as audit anchors.
        DrawSmall(gfx, $"Envelope #{env.EnvelopeId}", left, 36);

        // 6) Save to bytes
        using var ms = new MemoryStream();
        doc.Save(ms, false);
        var bytes = ms.ToArray();

        // 7) Persist to /wwwroot/Files/Sign/YYYY/MM/
        var dir = Path.Combine("wwwroot", "Files", "Sign",
            DateTime.UtcNow.ToString("yyyy"), DateTime.UtcNow.ToString("MM"));
        Directory.CreateDirectory(dir);
        var filePath = Path.Combine(dir, $"envelope-{envelopeId}.pdf");
        await File.WriteAllBytesAsync(filePath, bytes);

        // 8) Hash for tamper evidence
        var sha = HashHelper.Sha256(bytes);
        var webPath = filePath.Replace("wwwroot", "").Replace("\\", "/");
        return (bytes, webPath, sha);
    }

    // ---------- helpers ----------
    private static void DrawText(XGraphics gfx, XFont font, XBrush brush, string text, double x, double y)
    => gfx.DrawString(text, font, brush, new XPoint(x, y), XStringFormats.Default);

    private static void DrawSmall(XGraphics gfx, string text, double x, double y)
        => gfx.DrawString(text, new XFont("Roboto", 9, XFontStyle.Regular), XBrushes.Gray, new XPoint(x, y), XStringFormats.Default);

    // Convert inches to PDF points (1 in = 72 pt)
    private static double In(double inches) => inches * 72.0;

    // Debug helper to find coordinates quickly
    private static void DrawMarker(XGraphics gfx, double x, double y)
    {
        var p = new XPen(XColors.Red, 0.75);
        gfx.DrawLine(p, x - 6, y, x + 6, y);
        gfx.DrawLine(p, x, y - 6, x, y + 6);
    }

    // Optional label next to the marker
    private static void DrawLabel(XGraphics gfx, string text, double x, double y)
    {
        var font = new XFont("Roboto", 9, XFontStyle.Bold);
        gfx.DrawString(text, font, XBrushes.DarkRed, new XPoint(x + 8, y + 8), XStringFormats.Default);
    }

    // Draw a light grid every 1 inch with labels (0,1,2…) for quick orientation
    private static void DrawGrid(XGraphics gfx, PdfPage page, double stepInches = 1.0)
    {
        var step = In(stepInches);
        var pen = new XPen(XColors.OrangeRed, 0.25) { DashStyle = XDashStyle.Dot };
        var font = new XFont("Roboto", 7, XFontStyle.Regular);
        // Vertical lines + X labels
        for (double x = 0; x <= page.Width; x += step)
        {
            gfx.DrawLine(pen, x, 0, x, page.Height);
            gfx.DrawString((x / 72).ToString("0"), font, XBrushes.OrangeRed, new XPoint(x + 2, page.Height - 10), XStringFormats.Default);
        }
        // Horizontal lines + Y labels (from bottom in inches)
        for (double y = 0; y <= page.Height; y += step)
        {
            gfx.DrawLine(pen, 0, y, page.Width, y);
            gfx.DrawString((y / 72).ToString("0"), font, XBrushes.OrangeRed, new XPoint(2, y + 10), XStringFormats.Default);
        }
    }
}
