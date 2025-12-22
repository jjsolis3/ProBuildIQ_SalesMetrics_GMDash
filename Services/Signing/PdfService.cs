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
        //    Coordinates are based on the SF_OccupiedReleaseForm_1.4.pdf template
        //    Top section: Property Name, Installation Date, Unit Number
        //    Bottom section: Property Staff and Resident signature rows

        // Enable debug mode to visualize field positions
        var debug = _db.Database.GetService<IConfiguration>()["Pdf:DebugMarkers"]?.Equals("true", StringComparison.OrdinalIgnoreCase) == true;
        if (debug)
        {
            DrawGrid(gfx, page, 1.0); // 1-inch grid
        }

        // TOP SECTION - Form fields (aligned with template form lines)
        // Property Name field - approximately 9.2" from bottom, 1.65" from left
        DrawText(gfx, font, brush, propertyName, In(1.65), In(9.18));

        // Installation Date field - approximately 8.87" from bottom, 1.65" from left
        DrawText(gfx, font, brush, installDate, In(1.65), In(8.87));

        // Unit Number field - approximately 8.57" from bottom, 1.65" from left
        DrawText(gfx, font, brush, unitNumber, In(1.65), In(8.57));

        // BOTTOM SECTION - Signature rows
        // Property Staff row (approximately 1.73" from bottom)
        double staffRowY = In(1.73);
        DrawText(gfx, font, brush, staffName, In(0.6), staffRowY);                    // Property Staff Name column
        DrawText(gfx, font, brush, staffSignedAt, In(5.5), staffRowY);               // Date column

        // Resident row (approximately 1.03" from bottom)
        double residentRowY = In(1.03);
        DrawText(gfx, font, brush, tenantName, In(0.6), residentRowY);               // Resident Name column
        DrawText(gfx, font, brush, tenantPhone, In(5.5), residentRowY);              // Resident Phone column

        // Resident signature image (middle column, aligned with Resident row)
        // Signature should be positioned in the "Resident Signature" column
        if (tenantSigFs != null && File.Exists(tenantSigFs))
        {
            using var fs = File.OpenRead(tenantSigFs);
            using var img = XImage.FromStream(() => fs);
            double sigW = In(1.8);   // Signature width to fit in signature column
            double sigH = In(0.5);   // Signature height to fit in row
            // Position in middle column, slightly below the baseline to align nicely
            gfx.DrawImage(img, In(2.6), In(0.88), sigW, sigH);
        }


        // Optional: staff signature too (if you capture it similarly)
        // var staffSigFs = ...
        // gfx.DrawImage(...);

        // Optional: audit footer/hash (once you compute final bytes, you can re-open and stamp hash)
        // Here we add a placeholder text (hash added after save is tricky). You can also write the
        // envelope/recipient IDs as audit anchors.
        DrawSmall(gfx, $"Envelope #{env.EnvelopeId}", In(1.0), 36);

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
