// Services/Signing/PdfService.cs
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.Logging;
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
    private readonly IWebHostEnvironment _env;
    private readonly ILogger<PdfService> _logger;

    public PdfService(SalesMetricsDbContext db, IErpMergeService merge, IWebHostEnvironment env, ILogger<PdfService> logger)
    {
        _db = db; _merge = merge; _env = env; _logger = logger;
    }

    public async Task<(byte[] bytes, string storagePath, byte[] sha256)> RenderAndSealAsync(long envelopeId)
    {
        _logger.LogDebug("===== STARTING PDF GENERATION FOR ENVELOPE {EnvelopeId} =====", envelopeId);

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

        _logger.LogDebug("Template: {TemplateKey} ({DisplayName})", template.TemplateKey, template.DisplayName);
        _logger.LogDebug("PDF File: {PdfFilePath}", template.PdfFilePath ?? "NONE - will use default");
        _logger.LogDebug("Has MergeSpec: {HasMergeSpec}", !string.IsNullOrWhiteSpace(template.MergeSpecJson));
        _logger.LogDebug("SignField records found: {FieldCount}", env.Fields?.Count ?? 0);

        var tenant = env.Recipients.FirstOrDefault(r => r.Role == "Tenant");
        var manager = env.Recipients.FirstOrDefault(r => r.Role == "Manager");

        // 2) Gather data from SignField records and supplement with ERP data
        var fieldData = await GatherFieldDataAsync(env, tenant, manager);

        _logger.LogDebug("========== ENVELOPE {EnvelopeId} ==========", envelopeId);
        _logger.LogDebug("Field data gathered: {FieldCount} fields", fieldData.Count);
        foreach (var kvp in fieldData)
        {
            _logger.LogDebug("Field '{FieldKey}' = '{FieldValue}'", kvp.Key, kvp.Value);
        }

        // 3) Determine which PDF to use
        string templatePath;
        if (!string.IsNullOrWhiteSpace(template.PdfFilePath))
        {
            // Use custom uploaded PDF
            templatePath = Path.Combine(_env.WebRootPath, "Files", "Templates", template.PdfFilePath);
        }
        else
        {
            // Fallback to default for backward compatibility
            templatePath = Path.Combine(_env.WebRootPath, "Files", "Templates", "SF_OccupiedReleaseForm_1.4.pdf");
        }

        if (!File.Exists(templatePath))
            throw new FileNotFoundException($"PDF template not found: {templatePath}", templatePath);

        using var doc = PdfReader.Open(templatePath, PdfDocumentOpenMode.Modify);
        var page = doc.Pages[0]; // default page for legacy stamping, tenant-skip, audit footer
        using var gfx = XGraphics.FromPdfPage(page);

        // Cache XGraphics per page for multi-page MergeSpec stamping
        var pageGfxCache = new Dictionary<int, (PdfPage Page, XGraphics Gfx)> { [0] = (page, gfx) };
        XGraphics GetPageGfx(int pageIndex)
        {
            pageIndex = Math.Clamp(pageIndex, 0, doc.Pages.Count - 1);
            if (!pageGfxCache.TryGetValue(pageIndex, out var entry))
            {
                var p = doc.Pages[pageIndex];
                entry = (p, XGraphics.FromPdfPage(p));
                pageGfxCache[pageIndex] = entry;
            }
            return entry.Gfx;
        }
        PdfPage GetPage(int pageIndex) => pageGfxCache[Math.Clamp(pageIndex, 0, doc.Pages.Count - 1)].Page;

        // Log page dimensions for debugging coordinate issues
        _logger.LogDebug("Page count: {PageCount}, Page 0 dimensions: {PageWidth}x{PageHeight} points",
            doc.Pages.Count, page.Width.Point.ToString("F1"), page.Height.Point.ToString("F1"));

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
                _logger.LogDebug("Parsing MergeSpec for envelope {EnvelopeId}", envelopeId);
                _logger.LogDebug("MergeSpec JSON: {MergeSpecJson}", template.MergeSpecJson);

                var mergeSpec = JsonSerializer.Deserialize<MergeSpec>(template.MergeSpecJson);
                if (mergeSpec?.fieldMapping != null)
                {
                    _logger.LogDebug("Found {FieldMappingCount} fields in MergeSpec", mergeSpec.fieldMapping.Count);

                    foreach (var (fieldKey, fieldInfo) in mergeSpec.fieldMapping)
                    {
                        // Get the value for this field
                        if (!fieldData.TryGetValue(fieldKey, out var value))
                        {
                            _logger.LogDebug("Field '{FieldKey}' not found in field data - skipping", fieldKey);
                            continue;
                        }

                        _logger.LogDebug("Processing field '{FieldKey}' with value: {FieldValue}", fieldKey, value);

                        // Support multiple placements of the same field
                        var placements = fieldInfo.placements ?? new List<Coordinates>();

                        // Also check for legacy single coordinate (backward compatibility)
                        if (placements.Count == 0 && fieldInfo.coordinates != null)
                        {
                            placements = new List<Coordinates> { fieldInfo.coordinates };
                        }

                        _logger.LogDebug("Field '{FieldKey}' has {PlacementCount} placement(s)", fieldKey, placements.Count);

                        // Place field at all specified locations (multi-page aware)
                        foreach (var coord in placements)
                        {
                            var coordGfx = GetPageGfx(coord.page);
                            var coordPage = GetPage(coord.page);
                            _logger.LogDebug("Placing '{FieldKey}' at page={Page}, x={X}, y={Y}, w={Width}, h={Height}", fieldKey, coord.page, coord.x, coord.y, coord.width, coord.height);

                            // Draw debug rectangle if enabled
                            if (debug)
                            {
                                var boxRect = new XRect(coord.x, coord.y, coord.width, coord.height);
                                coordGfx.DrawRectangle(new XPen(XColors.Blue, 0.5), boxRect);
                                DrawLabel(coordGfx, fieldKey, coord.x, coord.y - 5);
                            }

                            // Handle signatures differently
                            if (fieldInfo.type == "signature" && value is string sigPath && !string.IsNullOrWhiteSpace(sigPath))
                            {
                                _logger.LogDebug("Drawing signature for '{FieldKey}' from path: {SignaturePath}", fieldKey, sigPath);
                                DrawSignature(coordGfx, coordPage, sigPath, coord.x, coord.y, coord.width, coord.height);
                            }
                            else if (value is string textValue)
                            {
                                _logger.LogDebug("Drawing text for '{FieldKey}': {TextValue}", fieldKey, textValue);
                                DrawTextWithCoordinateConversion(coordGfx, coordPage, font, brush, textValue, coord.x, coord.y);
                            }
                        }
                    }
                }
                else
                {
                    _logger.LogDebug("No field mappings found in MergeSpec");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error parsing MergeSpec for envelope {EnvelopeId}", envelopeId);
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
                    _logger.LogDebug(ex, "Could not find resident field coordinates");
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

                _logger.LogDebug("Combined stamp area: Name({NameX},{NameY}) + Sig({SigX},{SigY}) = Stamp({StampX},{StampY},{StampWidth}x{StampHeight})",
                    residentNameCoords.x, residentNameCoords.y, residentSigCoords.x, residentSigCoords.y, stampX, stampY, stampWidth, stampHeight);
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
            var waivedText = $"TENANT SIGNATURE WAIVED — AUTHORIZED BY: {env.TenantSkippedByName?.ToUpper() ?? "PROPERTY STAFF"}";
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

            _logger.LogDebug("Added tenant skip notation covering ResidentName + ResidentSignature at ({StampX}, {StampY})", stampX, stampY);
        }

        // Dispose any extra-page graphics (page 0 is disposed by the using statement)
        foreach (var kvp in pageGfxCache.Where(k => k.Key != 0))
            kvp.Value.Gfx.Dispose();

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

        _logger.LogDebug("PDF saved to: {FilePath}", filePath);
        _logger.LogDebug("PDF size: {PdfSizeBytes} bytes", bytes.Length);

        // 9) Hash for tamper evidence
        var sha = HashHelper.Sha256(bytes);
        var webPath = filePath.Replace("wwwroot", "").Replace("\\", "/");

        _logger.LogDebug("===== PDF GENERATION COMPLETE FOR ENVELOPE {EnvelopeId} =====", envelopeId);

        return (bytes, webPath, sha);
    }

    /// <summary>
    /// Generate a preview PDF showing only the fields that should be visible to the current recipient
    /// - Manager view: Property fields only (PropertyName, InstallationDate, UnitNumber)
    /// - Tenant view: Property fields + Manager signature and fields
    /// </summary>
    public async Task<byte[]> GeneratePreviewPdfAsync(long envelopeId, long currentRecipientId)
    {
        _logger.LogDebug("===== GENERATING PREVIEW FOR ENVELOPE {EnvelopeId}, RECIPIENT {RecipientId} =====", envelopeId, currentRecipientId);

        // 1) Load envelope + template + recipients + fields
        var env = await _db.SignEnvelopes
            .Include(e => e.Recipients)
            .Include(e => e.Fields)
            .AsNoTracking()
            .FirstAsync(e => e.EnvelopeId == envelopeId);

        var currentRecipient = env.Recipients.First(r => r.RecipientId == currentRecipientId);
        _logger.LogDebug("Current recipient: {RecipientName} ({RecipientRole})", currentRecipient.FullName, currentRecipient.Role);

        var template = await _db.SignTemplates
            .AsNoTracking()
            .FirstAsync(t => t.TemplateKey == env.TemplateKey);

        var tenant = env.Recipients.FirstOrDefault(r => r.Role == "Tenant");
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
            _logger.LogDebug("Manager view - showing property fields only");
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
            _logger.LogDebug("Tenant view - showing property fields + manager signature");
        }

        _logger.LogDebug("Fields to display: {FieldsToShow}", string.Join(", ", fieldsToShow));

        // 3) Load the PDF template
        string templatePath;
        if (!string.IsNullOrWhiteSpace(template.PdfFilePath))
        {
            templatePath = Path.Combine(_env.WebRootPath, "Files", "Templates", template.PdfFilePath);
        }
        else
        {
            templatePath = Path.Combine(_env.WebRootPath, "Files", "Templates", "SF_OccupiedReleaseForm_1.4.pdf");
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
                            _logger.LogDebug("Skipping field '{FieldKey}' - not visible to {RecipientRole}", fieldKey, currentRecipient.Role);
                            continue;
                        }

                        if (!fieldData.TryGetValue(fieldKey, out var value))
                        {
                            _logger.LogDebug("Field '{FieldKey}' not found in field data", fieldKey);
                            continue;
                        }

                        _logger.LogDebug("Stamping field '{FieldKey}' = '{FieldValue}'", fieldKey, value);

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
                _logger.LogError(ex, "Error generating preview for envelope {EnvelopeId}", envelopeId);
            }
        }

        // 5) Return PDF bytes
        using var ms = new MemoryStream();
        doc.Save(ms, false);
        var bytes = ms.ToArray();

        _logger.LogDebug("===== PREVIEW GENERATION COMPLETE, SIZE: {PdfSizeBytes} bytes =====", bytes.Length);

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
            _logger.LogDebug("Loading data from {FieldCount} SignField records", env.Fields.Count);
            foreach (var field in env.Fields)
            {
                if (!string.IsNullOrWhiteSpace(field.FieldValue))
                {
                    data[field.FieldKey] = field.FieldValue;
                    _logger.LogDebug("Field from DB: {FieldKey} = {FieldValue}", field.FieldKey, field.FieldValue);
                }
            }
        }
        else
        {
            _logger.LogDebug("No SignField records found, using legacy ERP data gathering");
        }

        // Supplement with ERP data for any missing fields.
        // When no ERP PropertyID is linked, fall back to the free-text name stored on the envelope.
        // Property fields
        if (!data.ContainsKey("PropertyName"))
            data["PropertyName"] = (env.PropertyID.HasValue
                ? await _merge.GetPropertyNameAsync(env.PropertyID)
                : null) ?? env.PropertyName ?? "";
        if (!data.ContainsKey("PropertyAddress"))
            data["PropertyAddress"] = (env.PropertyID.HasValue
                ? await _merge.GetPropertyAddressAsync(env.PropertyID)
                : null) ?? "";
        if (!data.ContainsKey("PropertyPhone"))
            data["PropertyPhone"] = (env.PropertyID.HasValue
                ? await _merge.GetCustomerPhoneAsync(env.PropertyID)
                : null) ?? "";
        if (!data.ContainsKey("PropertyCity"))
            data["PropertyCity"] = (env.PropertyID.HasValue ? await _merge.GetPropertyCityAsync(env.PropertyID) : null) ?? "";
        if (!data.ContainsKey("PropertyState"))
            data["PropertyState"] = (env.PropertyID.HasValue ? await _merge.GetPropertyStateAsync(env.PropertyID) : null) ?? "";
        if (!data.ContainsKey("PropertyZip"))
            data["PropertyZip"] = (env.PropertyID.HasValue ? await _merge.GetPropertyZipAsync(env.PropertyID) : null) ?? "";

        // Customer fields (sourced from property record in this ERP)
        if (!data.ContainsKey("CustomerName"))
            data["CustomerName"] = (env.PropertyID.HasValue ? await _merge.GetCustomerNameAsync(env.PropertyID) : null) ?? env.PropertyName ?? "";
        if (!data.ContainsKey("CustomerPhone"))
            data["CustomerPhone"] = (env.PropertyID.HasValue ? await _merge.GetCustomerPhoneAsync(env.PropertyID) : null) ?? "";
        if (!data.ContainsKey("CustomerEmail"))
            data["CustomerEmail"] = (env.PropertyID.HasValue ? await _merge.GetCustomerEmailAsync(env.PropertyID) : null) ?? "";
        if (!data.ContainsKey("CustomerCompany"))
            data["CustomerCompany"] = (env.PropertyID.HasValue ? await _merge.GetCustomerCompanyAsync(env.PropertyID) : null) ?? env.PropertyName ?? "";

        // Order fields
        if (!data.ContainsKey("OrderNumber"))
            data["OrderNumber"] = env.OrderNumber ?? "";
        if (!data.ContainsKey("UnitNumber"))
            data["UnitNumber"] = await _merge.GetUnitNumberByOrderIdAsync(env.OrderId) ?? "";
        if (!data.ContainsKey("InstallationDate"))
        {
            var start = await _merge.GetOrderStartDateAsync(env.OrderId);
            data["InstallationDate"] = start?.ToString("MM/dd/yyyy") ?? DateTime.UtcNow.ToString("MM/dd/yyyy");
        }
        if (!data.ContainsKey("DeliveryDate"))
        {
            var start = await _merge.GetOrderStartDateAsync(env.OrderId);
            data["DeliveryDate"] = start?.ToString("MM/dd/yyyy") ?? "";
        }
        if (!data.ContainsKey("OrderStartDate"))
        {
            var start = await _merge.GetOrderStartDateAsync(env.OrderId);
            data["OrderStartDate"] = start?.ToString("MM/dd/yyyy") ?? "";
        }
        if (!data.ContainsKey("OrderEndDate"))
        {
            var end = await _merge.GetOrderEndDateAsync(env.OrderId);
            data["OrderEndDate"] = end?.ToString("MM/dd/yyyy") ?? "";
        }
        if (!data.ContainsKey("OrderSignedDate"))
        {
            var signed = await _merge.GetOrderSignedDateAsync(env.OrderId);
            data["OrderSignedDate"] = signed?.ToString("MM/dd/yyyy") ?? "";
        }
        if (!data.ContainsKey("LeaseStartDate"))
        {
            var start = await _merge.GetOrderStartDateAsync(env.OrderId);
            data["LeaseStartDate"] = start?.ToString("MM/dd/yyyy") ?? "";
        }
        if (!data.ContainsKey("LeaseEndDate"))
        {
            var end = await _merge.GetOrderEndDateAsync(env.OrderId);
            data["LeaseEndDate"] = end?.ToString("MM/dd/yyyy") ?? "";
        }

        // Property Staff (Manager) fields - always use fresh recipient data at PDF generation time
        data["PropertyStaffName"] = manager?.FullName ?? "Property Staff";
        data["PropertyStaffDate"] = manager?.SignedAtUtc?.ToLocalTime().ToString("MM/dd/yyyy") ?? "";
        data["PropertyStaffSignature"] = GetSignatureFilePath(manager) ?? "";

        // Resident (Tenant) fields - always override with actual tenant data at PDF generation time.
        // SignField records may contain stale data (e.g., Manager fallback from envelope creation
        // when no Tenant recipient existed yet), so we always use the current tenant recipient data.
        if (tenant != null)
        {
            data["ResidentName"] = tenant.FullName;
            data["ResidentPhone"] = tenant.Phone ?? "";
            data["ResidentSignature"] = GetSignatureFilePath(tenant) ?? "";
        }
        else
        {
            // No tenant recipient exists - use empty values (not Manager fallback)
            if (!data.ContainsKey("ResidentName"))
                data["ResidentName"] = "";
            if (!data.ContainsKey("ResidentPhone"))
                data["ResidentPhone"] = "";
            if (!data.ContainsKey("ResidentSignature"))
                data["ResidentSignature"] = "";
        }

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
            _logger.LogDebug("Signature - Page: {PageWidth}x{PageHeight}pt, x={X}, y={Y}, size={Width}x{Height}",
                page.Width.Point.ToString("F1"), page.Height.Point.ToString("F1"), x, y, width, height);

            gfx.DrawImage(img, x, y, width, height);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error drawing signature from {ImagePath}", imagePath);
        }
    }

    /// <summary>
    /// Draw text on PDF
    /// </summary>
    private void DrawTextWithCoordinateConversion(XGraphics gfx, PdfPage page, XFont font, XBrush brush, string text, double x, double y)
    {
        // Use coordinates directly from configurator without adjustment
        // The configurator saves Y coordinate for the baseline position
        _logger.LogDebug("Text '{Text}' - Page: {PageWidth}x{PageHeight}pt, x={X}, y={Y}",
            text, page.Width.Point.ToString("F1"), page.Height.Point.ToString("F1"), x, y);

        //gfx.DrawString(text, font, brush, new XPoint(x, y), XStringFormats.Default);
        gfx.DrawString(text, font, brush, new XPoint(x, y), XStringFormats.TopLeft);
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
