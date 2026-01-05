// Services/Signing/PdfService.cs
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using PdfSharpCore.Drawing;
using PdfSharpCore.Pdf;
using PdfSharpCore.Pdf.IO;
using SalesMetrics.Data;
using SalesMetrics.Utilities.Security;
using System.Text.Json;

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
        Console.WriteLine($"[PDF DEBUG] ===== STARTING PDF GENERATION FOR ENVELOPE {envelopeId} =====");

        // 1) Load envelope + template + recipients + fields
        var env = await _db.SignEnvelopes
            .Include(e => e.Recipients)
            .Include(e => e.Fields)  // Load SignField records
            .AsNoTracking()
            .FirstAsync(e => e.EnvelopeId == envelopeId);

        // Load the template to get PDF path and field mappings
        var template = await _db.SignTemplates
            .AsNoTracking()
            .FirstAsync(t => t.TemplateKey == env.TemplateKey);

        Console.WriteLine($"[PDF DEBUG] Template: {template.TemplateKey} ({template.DisplayName})");
        Console.WriteLine($"[PDF DEBUG] PDF File: {template.PdfFilePath ?? "NONE - will use default"}");
        Console.WriteLine($"[PDF DEBUG] Has MergeSpec: {!string.IsNullOrWhiteSpace(template.MergeSpecJson)}");
        Console.WriteLine($"[PDF DEBUG] SignField records found: {env.Fields?.Count ?? 0}");

        var tenant = env.Recipients.FirstOrDefault(r => r.Role == "Tenant") ?? env.Recipients.First();
        var manager = env.Recipients.FirstOrDefault(r => r.Role == "Manager");

        // 2) Gather data from SignField records and supplement with ERP data
        var fieldData = await GatherFieldDataAsync(env, tenant, manager);

        Console.WriteLine($"[PDF DEBUG] ========== ENVELOPE {envelopeId} ==========");
        Console.WriteLine($"[PDF DEBUG] Field data gathered: {fieldData.Count} fields");
        foreach (var kvp in fieldData)
        {
            Console.WriteLine($"[PDF DEBUG] Field '{kvp.Key}' = '{kvp.Value}'");
        }

        // 3) Determine which PDF to use
        string templatePath;
        if (!string.IsNullOrWhiteSpace(template.PdfFilePath))
        {
            // Use custom uploaded PDF
            templatePath = Path.Combine("Content", "Templates", template.PdfFilePath);
        }
        else
        {
            // Fallback to default for backward compatibility
            templatePath = Path.Combine("Content", "Templates", "SF_OccupiedReleaseForm_1.4.pdf");
        }

        if (!File.Exists(templatePath))
            throw new FileNotFoundException($"PDF template not found: {templatePath}", templatePath);

        using var doc = PdfReader.Open(templatePath, PdfDocumentOpenMode.Modify);
        var page = doc.Pages[0];
        using var gfx = XGraphics.FromPdfPage(page);

        // Log page dimensions for debugging coordinate issues
        Console.WriteLine($"[PDF DEBUG] Page dimensions: {page.Width:F1} x {page.Height:F1} points");
        Console.WriteLine($"[PDF DEBUG] Page size in inches: {page.Width/72:F2}\" x {page.Height/72:F2}\"");

        // 4) Fonts & brushes
        var font = new XFont("Roboto", 11, XFontStyle.Regular);
        var fontBold = new XFont("Roboto", 12, XFontStyle.Bold);
        var brush = XBrushes.Black;

        // 5) Debug mode
        var debug = _db.Database.GetService<IConfiguration>()["Pdf:DebugMarkers"]?.Equals("true", StringComparison.OrdinalIgnoreCase) == true;
        if (debug)
        {
            DrawGrid(gfx, page, 1.0);
        }

        // 6) Parse MergeSpec and stamp fields dynamically
        if (!string.IsNullOrWhiteSpace(template.MergeSpecJson))
        {
            try
            {
                Console.WriteLine($"[PDF DEBUG] Parsing MergeSpec for envelope {envelopeId}");
                Console.WriteLine($"[PDF DEBUG] MergeSpec JSON: {template.MergeSpecJson}");

                var mergeSpec = JsonSerializer.Deserialize<MergeSpec>(template.MergeSpecJson);
                if (mergeSpec?.fieldMapping != null)
                {
                    Console.WriteLine($"[PDF DEBUG] Found {mergeSpec.fieldMapping.Count} fields in MergeSpec");

                    foreach (var (fieldKey, fieldInfo) in mergeSpec.fieldMapping)
                    {
                        // Get the value for this field
                        if (!fieldData.TryGetValue(fieldKey, out var value))
                        {
                            Console.WriteLine($"[PDF DEBUG] Field '{fieldKey}' not found in field data - skipping");
                            continue;
                        }

                        Console.WriteLine($"[PDF DEBUG] Processing field '{fieldKey}' with value: {value}");

                        // Support multiple placements of the same field
                        var placements = fieldInfo.placements ?? new List<Coordinates>();

                        // Also check for legacy single coordinate (backward compatibility)
                        if (placements.Count == 0 && fieldInfo.coordinates != null)
                        {
                            placements = new List<Coordinates> { fieldInfo.coordinates };
                        }

                        Console.WriteLine($"[PDF DEBUG] Field '{fieldKey}' has {placements.Count} placement(s)");

                        // Place field at all specified locations
                        foreach (var coord in placements)
                        {
                            Console.WriteLine($"[PDF DEBUG] Placing '{fieldKey}' at page={coord.page}, x={coord.x}, y={coord.y}, w={coord.width}, h={coord.height}");

                            // Draw debug rectangle if enabled
                            if (debug)
                            {
                                var boxRect = new XRect(coord.x, coord.y, coord.width, coord.height);
                                gfx.DrawRectangle(new XPen(XColors.Blue, 0.5), boxRect);
                                DrawLabel(gfx, fieldKey, coord.x, coord.y - 5);
                            }

                            // Handle signatures differently
                            if (fieldInfo.type == "signature" && value is string sigPath && !string.IsNullOrWhiteSpace(sigPath))
                            {
                                Console.WriteLine($"[PDF DEBUG] Drawing signature for '{fieldKey}' from path: {sigPath}");
                                DrawSignature(gfx, page, sigPath, coord.x, coord.y, coord.width, coord.height);
                            }
                            else if (value is string textValue)
                            {
                                Console.WriteLine($"[PDF DEBUG] Drawing text for '{fieldKey}': {textValue}");
                                DrawTextWithCoordinateConversion(gfx, page, font, brush, textValue, coord.x, coord.y);
                            }
                        }
                    }
                }
                else
                {
                    Console.WriteLine($"[PDF DEBUG] No field mappings found in MergeSpec");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[PDF ERROR] Error parsing MergeSpec: {ex.Message}");
                Console.WriteLine($"[PDF ERROR] Stack trace: {ex.StackTrace}");
                // Fall back to default behavior
                StampFieldsLegacy(gfx, font, brush, fieldData);
            }
        }
        else
        {
            // No MergeSpec - use legacy hardcoded positions
            StampFieldsLegacy(gfx, font, brush, fieldData);
        }

        // Optional: audit footer
        DrawSmall(gfx, $"Envelope #{env.EnvelopeId}", In(1.0), 36);

        // Stamp "Tenant Signature Waived" notation if tenant was skipped
        if (env.TenantSkipped)
        {
            // Find both ResidentName and ResidentSignature field coordinates to create combined stamp area
            Coordinates? residentNameCoords = null;
            Coordinates? residentSigCoords = null;

            if (!string.IsNullOrWhiteSpace(template.MergeSpecJson))
            {
                try
                {
                    var mergeSpec = JsonSerializer.Deserialize<MergeSpec>(template.MergeSpecJson);
                    if (mergeSpec?.fieldMapping != null)
                    {
                        // Get ResidentName coordinates
                        if (mergeSpec.fieldMapping.TryGetValue("ResidentName", out var residentNameField))
                        {
                            residentNameCoords = residentNameField.placements?.FirstOrDefault() ?? residentNameField.coordinates;
                        }

                        // Get ResidentSignature coordinates
                        if (mergeSpec.fieldMapping.TryGetValue("ResidentSignature", out var residentSigField))
                        {
                            residentSigCoords = residentSigField.placements?.FirstOrDefault() ?? residentSigField.coordinates;
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[PDF DEBUG] Could not find resident field coordinates: {ex.Message}");
                }
            }

            // Calculate bounding box that covers both ResidentName and ResidentSignature
            double stampX, stampY, stampWidth, stampHeight;
            if (residentNameCoords != null && residentSigCoords != null)
            {
                // Find the leftmost X coordinate
                stampX = Math.Min(residentNameCoords.x, residentSigCoords.x);

                // Find the topmost Y coordinate
                stampY = Math.Min(residentNameCoords.y, residentSigCoords.y);

                // Calculate width to cover both fields (rightmost edge - leftmost edge)
                var rightEdgeName = residentNameCoords.x + residentNameCoords.width;
                var rightEdgeSig = residentSigCoords.x + residentSigCoords.width;
                stampWidth = Math.Max(rightEdgeName, rightEdgeSig) - stampX;

                // Calculate height to cover both fields (bottommost edge - topmost edge)
                var bottomEdgeName = residentNameCoords.y + residentNameCoords.height;
                var bottomEdgeSig = residentSigCoords.y + residentSigCoords.height;
                stampHeight = Math.Max(bottomEdgeName, bottomEdgeSig) - stampY;

                Console.WriteLine($"[PDF DEBUG] Combined stamp area: Name({residentNameCoords.x},{residentNameCoords.y}) + Sig({residentSigCoords.x},{residentSigCoords.y}) = Stamp({stampX},{stampY},{stampWidth}x{stampHeight})");
            }
            else if (residentSigCoords != null)
            {
                // Fallback to just signature field if name field not found
                stampX = residentSigCoords.x;
                stampY = residentSigCoords.y;
                stampWidth = residentSigCoords.width;
                stampHeight = residentSigCoords.height;
            }
            else
            {
                // Fallback to default position (lower area of page)
                stampX = In(0.5);
                stampY = In(1.5);
                stampWidth = In(7.0);
                stampHeight = In(0.6);
            }

            var waivedFont = new XFont("Roboto", 10, XFontStyle.Bold);
            var waivedBrush = XBrushes.Red;
            var waivedText = $"TENANT SIGNATURE WAIVED BY {env.TenantSkippedByName?.ToUpper() ?? "PROPERTY STAFF"}";
            var waivedDate = env.TenantSkippedAtUtc?.ToLocalTime().ToString("MM/dd/yyyy h:mm tt") ?? "";

            // Get manager phone for display
            var managerPhone = manager?.Phone ?? "";
            var phoneText = !string.IsNullOrWhiteSpace(managerPhone) ? $"Property Staff Phone: {managerPhone}" : "";

            // Draw a red box background over both resident name and signature fields
            var boxRect = new XRect(stampX, stampY, stampWidth, stampHeight);
            gfx.DrawRectangle(new XSolidBrush(XColor.FromArgb(240, 255, 240, 240)), boxRect); // Semi-transparent red background
            gfx.DrawRectangle(new XPen(XColors.Red, 2.0), boxRect); // Thicker red border

            // Draw the text (adjusted for box size)
            var textY = stampY + In(0.12);
            gfx.DrawString(waivedText, waivedFont, waivedBrush, new XPoint(stampX + In(0.1), textY), XStringFormats.Default);

            textY += In(0.18);
            gfx.DrawString($"Date: {waivedDate}", new XFont("Roboto", 9, XFontStyle.Regular), XBrushes.DarkRed,
                new XPoint(stampX + In(0.1), textY), XStringFormats.Default);

            // Add manager phone if available
            if (!string.IsNullOrWhiteSpace(phoneText))
            {
                textY += In(0.18);
                gfx.DrawString(phoneText, new XFont("Roboto", 9, XFontStyle.Regular), XBrushes.DarkRed,
                    new XPoint(stampX + In(0.1), textY), XStringFormats.Default);
            }

            Console.WriteLine($"[PDF DEBUG] Added tenant skip notation covering ResidentName + ResidentSignature at ({stampX}, {stampY})");
        }

        // 7) Save to bytes
        using var ms = new MemoryStream();
        doc.Save(ms, false);
        var bytes = ms.ToArray();

        // 8) Persist to /wwwroot/Files/Sign/YYYY/MM/
        var dir = Path.Combine("wwwroot", "Files", "Sign",
            DateTime.UtcNow.ToString("yyyy"), DateTime.UtcNow.ToString("MM"));
        Directory.CreateDirectory(dir);
        var filePath = Path.Combine(dir, $"envelope-{envelopeId}.pdf");
        await File.WriteAllBytesAsync(filePath, bytes);

        Console.WriteLine($"[PDF DEBUG] PDF saved to: {filePath}");
        Console.WriteLine($"[PDF DEBUG] PDF size: {bytes.Length} bytes");

        // 9) Hash for tamper evidence
        var sha = HashHelper.Sha256(bytes);
        var webPath = filePath.Replace("wwwroot", "").Replace("\\", "/");

        Console.WriteLine($"[PDF DEBUG] ===== PDF GENERATION COMPLETE FOR ENVELOPE {envelopeId} =====");

        return (bytes, webPath, sha);
    }

    /// <summary>
    /// Generate a preview PDF showing only the fields that should be visible to the current recipient
    /// - Manager view: Property fields only (PropertyName, InstallationDate, UnitNumber)
    /// - Tenant view: Property fields + Manager signature and fields
    /// </summary>
    public async Task<byte[]> GeneratePreviewPdfAsync(long envelopeId, long currentRecipientId)
    {
        Console.WriteLine($"[PREVIEW PDF] ===== GENERATING PREVIEW FOR ENVELOPE {envelopeId}, RECIPIENT {currentRecipientId} =====");

        // 1) Load envelope + template + recipients + fields
        var env = await _db.SignEnvelopes
            .Include(e => e.Recipients)
            .Include(e => e.Fields)
            .AsNoTracking()
            .FirstAsync(e => e.EnvelopeId == envelopeId);

        var currentRecipient = env.Recipients.First(r => r.RecipientId == currentRecipientId);
        Console.WriteLine($"[PREVIEW PDF] Current recipient: {currentRecipient.FullName} ({currentRecipient.Role})");

        var template = await _db.SignTemplates
            .AsNoTracking()
            .FirstAsync(t => t.TemplateKey == env.TemplateKey);

        var tenant = env.Recipients.FirstOrDefault(r => r.Role == "Tenant") ?? env.Recipients.First();
        var manager = env.Recipients.FirstOrDefault(r => r.Role == "Manager");

        // 2) Determine which fields to show based on current recipient's role
        var fieldData = await GatherFieldDataAsync(env, tenant, manager);
        var fieldsToShow = new HashSet<string>();

        if (currentRecipient.Role == "Manager")
        {
            // Manager sees only property fields
            fieldsToShow.Add("PropertyName");
            fieldsToShow.Add("InstallationDate");
            fieldsToShow.Add("UnitNumber");
            Console.WriteLine($"[PREVIEW PDF] Manager view - showing property fields only");
        }
        else if (currentRecipient.Role == "Tenant")
        {
            // Tenant sees property fields + manager's signature and info
            fieldsToShow.Add("PropertyName");
            fieldsToShow.Add("InstallationDate");
            fieldsToShow.Add("UnitNumber");
            fieldsToShow.Add("PropertyStaffName");
            fieldsToShow.Add("PropertyStaffDate");
            fieldsToShow.Add("PropertyStaffSignature");
            Console.WriteLine($"[PREVIEW PDF] Tenant view - showing property fields + manager signature");
        }

        Console.WriteLine($"[PREVIEW PDF] Fields to display: {string.Join(", ", fieldsToShow)}");

        // 3) Load the PDF template
        string templatePath;
        if (!string.IsNullOrWhiteSpace(template.PdfFilePath))
        {
            templatePath = Path.Combine("Content", "Templates", template.PdfFilePath);
        }
        else
        {
            templatePath = Path.Combine("Content", "Templates", "SF_OccupiedReleaseForm_1.4.pdf");
        }

        if (!File.Exists(templatePath))
            throw new FileNotFoundException($"PDF template not found: {templatePath}", templatePath);

        using var doc = PdfReader.Open(templatePath, PdfDocumentOpenMode.Modify);
        var page = doc.Pages[0];
        using var gfx = XGraphics.FromPdfPage(page);

        var font = new XFont("Roboto", 11, XFontStyle.Regular);
        var brush = XBrushes.Black;

        // 4) Stamp only the fields that should be visible
        if (!string.IsNullOrWhiteSpace(template.MergeSpecJson))
        {
            try
            {
                var mergeSpec = JsonSerializer.Deserialize<MergeSpec>(template.MergeSpecJson);
                if (mergeSpec?.fieldMapping != null)
                {
                    foreach (var (fieldKey, fieldInfo) in mergeSpec.fieldMapping)
                    {
                        // Only show fields that are in the allowed list
                        if (!fieldsToShow.Contains(fieldKey))
                        {
                            Console.WriteLine($"[PREVIEW PDF] Skipping field '{fieldKey}' - not visible to {currentRecipient.Role}");
                            continue;
                        }

                        if (!fieldData.TryGetValue(fieldKey, out var value))
                        {
                            Console.WriteLine($"[PREVIEW PDF] Field '{fieldKey}' not found in field data");
                            continue;
                        }

                        Console.WriteLine($"[PREVIEW PDF] Stamping field '{fieldKey}' = '{value}'");

                        var placements = fieldInfo.placements ?? new List<Coordinates>();
                        if (placements.Count == 0 && fieldInfo.coordinates != null)
                        {
                            placements = new List<Coordinates> { fieldInfo.coordinates };
                        }

                        foreach (var coord in placements)
                        {
                            if (fieldInfo.type == "signature" && value is string sigPath && !string.IsNullOrWhiteSpace(sigPath))
                            {
                                DrawSignature(gfx, page, sigPath, coord.x, coord.y, coord.width, coord.height);
                            }
                            else if (value is string textValue)
                            {
                                DrawTextWithCoordinateConversion(gfx, page, font, brush, textValue, coord.x, coord.y);
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[PREVIEW PDF ERROR] Error generating preview: {ex.Message}");
            }
        }

        // 5) Return PDF bytes
        using var ms = new MemoryStream();
        doc.Save(ms, false);
        var bytes = ms.ToArray();

        Console.WriteLine($"[PREVIEW PDF] ===== PREVIEW GENERATION COMPLETE, SIZE: {bytes.Length} bytes =====");

        return bytes;
    }

    /// <summary>
    /// Gather all field values from envelope and ERP data
    /// Priority: 1) SignField records, 2) ERP data, 3) Recipient data
    /// </summary>
    private async Task<Dictionary<string, object>> GatherFieldDataAsync(
        Domain.Signing.SignEnvelope env,
        Domain.Signing.SignRecipient tenant,
        Domain.Signing.SignRecipient? manager)
    {
        var data = new Dictionary<string, object>();

        // First, populate from SignField records if they exist
        if (env.Fields != null && env.Fields.Any())
        {
            Console.WriteLine($"[PDF DEBUG] Loading data from {env.Fields.Count} SignField records");
            foreach (var field in env.Fields)
            {
                if (!string.IsNullOrWhiteSpace(field.FieldValue))
                {
                    data[field.FieldKey] = field.FieldValue;
                    Console.WriteLine($"[PDF DEBUG] Field from DB: {field.FieldKey} = {field.FieldValue}");
                }
            }
        }
        else
        {
            Console.WriteLine($"[PDF DEBUG] No SignField records found, using legacy ERP data gathering");
        }

        // Supplement with ERP data for any missing fields
        // Property fields
        if (!data.ContainsKey("PropertyName"))
        {
            var propertyName = await _merge.GetPropertyNameAsync(env.PropertyID) ?? "";
            data["PropertyName"] = propertyName;
        }
        if (!data.ContainsKey("PropertyAddress"))
        {
            var propertyAddress = await _merge.GetPropertyAddressAsync(env.PropertyID) ?? "";
            data["PropertyAddress"] = propertyAddress;
        }
        if (!data.ContainsKey("PropertyPhone"))
            data["PropertyPhone"] = ""; // TODO: Get from ERP if needed

        // Order fields
        if (!data.ContainsKey("UnitNumber"))
        {
            var unitNumber = await _merge.GetUnitNumberByOrderIdAsync(env.OrderId) ?? "";
            data["UnitNumber"] = unitNumber;
        }
        if (!data.ContainsKey("InstallationDate"))
            data["InstallationDate"] = DateTime.UtcNow.ToString("MM/dd/yyyy");
        if (!data.ContainsKey("DeliveryDate"))
            data["DeliveryDate"] = ""; // TODO: Get from ERP if needed

        // Property Staff (Manager) fields - updated with signing data
        if (!data.ContainsKey("PropertyStaffName"))
            data["PropertyStaffName"] = manager?.FullName ?? "Property Staff";
        if (!data.ContainsKey("PropertyStaffDate"))
            data["PropertyStaffDate"] = manager?.SignedAtUtc?.ToLocalTime().ToString("MM/dd/yyyy") ?? "";
        if (!data.ContainsKey("PropertyStaffSignature"))
            data["PropertyStaffSignature"] = GetSignatureFilePath(manager) ?? "";

        // Resident (Tenant) fields - updated with signing data
        if (!data.ContainsKey("ResidentName"))
            data["ResidentName"] = tenant.FullName;
        if (!data.ContainsKey("ResidentPhone"))
            data["ResidentPhone"] = tenant.Phone ?? "";
        if (!data.ContainsKey("ResidentSignature"))
            data["ResidentSignature"] = GetSignatureFilePath(tenant) ?? "";

        return data;
    }

    /// <summary>
    /// Get physical file path for signature image
    /// </summary>
    private string? GetSignatureFilePath(Domain.Signing.SignRecipient? recipient)
    {
        if (recipient?.SignatureImagePath == null) return null;

        var fsPath = Path.Combine("wwwroot", recipient.SignatureImagePath.TrimStart('/')
            .Replace("/", Path.DirectorySeparatorChar.ToString()));

        return File.Exists(fsPath) ? fsPath : null;
    }

    /// <summary>
    /// Draw signature image on PDF
    /// </summary>
    private void DrawSignature(XGraphics gfx, PdfPage page, string imagePath, double x, double y, double width, double height)
    {
        if (!File.Exists(imagePath)) return;

        try
        {
            using var fs = File.OpenRead(imagePath);
            using var img = XImage.FromStream(() => fs);

            // PDFSharp uses same coordinate system as canvas (top-left origin)
            // Use coordinates directly as saved from configurator
            Console.WriteLine($"[COORDINATE DEBUG] Signature - Page: {page.Width:F1}x{page.Height:F1}pt, x={x}, y={y}, size={width}x{height}");

            gfx.DrawImage(img, x, y, width, height);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error drawing signature: {ex.Message}");
        }
    }

    /// <summary>
    /// Draw text on PDF with baseline offset
    /// </summary>
    private static void DrawTextWithCoordinateConversion(XGraphics gfx, PdfPage page, XFont font, XBrush brush, string text, double x, double y)
    {
        // PDFSharp uses same coordinate system as canvas (top-left origin)
        // Add small baseline offset so text appears properly within the field box
        const double baselineOffset = 12; // Slightly below box top for better visual alignment
        var textY = y + baselineOffset;

        Console.WriteLine($"[COORDINATE DEBUG] Text '{text}' - Page: {page.Width:F1}x{page.Height:F1}pt, box y={y}, text y={textY}");

        gfx.DrawString(text, font, brush, new XPoint(x, textY), XStringFormats.Default);
    }

    /// <summary>
    /// Legacy field stamping for backward compatibility (hardcoded positions)
    /// </summary>
    private void StampFieldsLegacy(XGraphics gfx, XFont font, XBrush brush, Dictionary<string, object> fieldData)
    {
        // TOP SECTION - Form fields
        DrawText(gfx, font, brush, fieldData.GetValueOrDefault("PropertyName") as string ?? "", In(1.9), In(10.35));
        DrawText(gfx, font, brush, fieldData.GetValueOrDefault("InstallationDate") as string ?? "", In(1.9), In(10.05));
        DrawText(gfx, font, brush, fieldData.GetValueOrDefault("UnitNumber") as string ?? "", In(1.9), In(9.75));

        // BOTTOM SECTION - Signature table
        double staffRowY = In(2.0);
        DrawText(gfx, font, brush, fieldData.GetValueOrDefault("PropertyStaffName") as string ?? "", In(0.65), staffRowY);
        DrawText(gfx, font, brush, fieldData.GetValueOrDefault("PropertyStaffDate") as string ?? "", In(5.4), staffRowY);

        // Property Staff signature
        var managerSig = fieldData.GetValueOrDefault("PropertyStaffSignature") as string;
        if (managerSig != null)
        {
            using var fs = File.OpenRead(managerSig);
            using var img = XImage.FromStream(() => fs);
            gfx.DrawImage(img, In(2.4), In(1.6), In(1.6), In(0.45));
        }

        // Resident row
        double residentRowY = In(1.25);
        DrawText(gfx, font, brush, fieldData.GetValueOrDefault("ResidentName") as string ?? "", In(0.65), residentRowY);
        DrawText(gfx, font, brush, fieldData.GetValueOrDefault("ResidentPhone") as string ?? "", In(5.4), residentRowY);

        // Resident signature
        var tenantSig = fieldData.GetValueOrDefault("ResidentSignature") as string;
        if (tenantSig != null)
        {
            using var fs = File.OpenRead(tenantSig);
            using var img = XImage.FromStream(() => fs);
            gfx.DrawImage(img, In(2.4), In(0.85), In(1.6), In(0.45));
        }
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

    // MergeSpec JSON structure
    private class MergeSpec
    {
        public List<string>? fields { get; set; }
        public Dictionary<string, FieldMapping>? fieldMapping { get; set; }
    }

    private class FieldMapping
    {
        public string? source { get; set; }
        public string? type { get; set; }
        public string? role { get; set; }
        public Coordinates? coordinates { get; set; }  // Legacy: single placement
        public List<Coordinates>? placements { get; set; }  // New: multiple placements
    }

    private class Coordinates
    {
        public int page { get; set; }
        public double x { get; set; }
        public double y { get; set; }
        public double width { get; set; }
        public double height { get; set; }
    }
}
