# Interactive PDF Template Configurator - Implementation Plan

## Overview
Allow authenticated users to visually configure where data stamps should appear on PDF templates, eliminating the need for hard-coded coordinates.

## Database Schema Changes

### 1. Add `SignTemplateField` Table
```sql
CREATE TABLE SignTemplateFields (
    FieldId INT PRIMARY KEY IDENTITY,
    TemplateId INT NOT NULL,
    FieldName NVARCHAR(100) NOT NULL,  -- e.g., "PropertyName", "InstallDate", "TenantSignature"
    FieldType NVARCHAR(50) NOT NULL,    -- "text", "signature", "date", "checkbox"
    XPosition DECIMAL(10,2) NOT NULL,   -- X coordinate in points
    YPosition DECIMAL(10,2) NOT NULL,   -- Y coordinate in points
    Width DECIMAL(10,2),                -- Width for signature/checkbox fields
    Height DECIMAL(10,2),               -- Height for signature/checkbox fields
    FontSize INT NULL,                  -- Font size for text fields
    FontBold BIT DEFAULT 0,             -- Bold font flag
    DisplayOrder INT DEFAULT 0,
    IsRequired BIT DEFAULT 1,
    CreatedAt DATETIME2 DEFAULT GETUTCDATE(),
    FOREIGN KEY (TemplateId) REFERENCES SignTemplates(TemplateId) ON DELETE CASCADE
)
```

### 2. Update `SignTemplate` Table
Add field to track if template uses custom field mapping:
```sql
ALTER TABLE SignTemplates
ADD UseCustomFieldMapping BIT DEFAULT 0,
    DefaultFieldMapping NVARCHAR(MAX) NULL  -- JSON fallback for simple templates
```

## Frontend Components

### 1. PDF Template Field Configurator Page

**Location:** `/Views/SignTemplates/ConfigureFields.cshtml`

**Features:**
- PDF.js viewer to display the template PDF
- Drag-and-drop field placeholders
- Click to place fields on PDF
- Field property editor (size, font, type)
- Visual grid overlay for precise placement
- Save coordinates to database

**UI Controls:**
- Field palette (left sidebar):
  - Property Name (text)
  - Installation Date (text)
  - Unit Number (text)
  - Property Staff Name (text)
  - Property Staff Signature (image)
  - Property Staff Date (text)
  - Resident Name (text)
  - Resident Signature (image)
  - Resident Phone (text)

- Canvas area (center):
  - PDF preview with zoom controls
  - Draggable field boxes
  - Click-to-place mode
  - Coordinate display

- Properties panel (right sidebar):
  - Selected field properties
  - X, Y coordinates (inches)
  - Font size, bold toggle
  - Width/height for signatures
  - Delete field button

### 2. JavaScript Implementation

**File:** `/wwwroot/assets/js/template-field-config.js`

```javascript
class PdfFieldConfigurator {
    constructor(pdfUrl, templateId) {
        this.pdfUrl = pdfUrl;
        this.templateId = templateId;
        this.canvas = document.getElementById('pdf-canvas');
        this.ctx = this.canvas.getContext('2d');
        this.fields = [];
        this.selectedField = null;
        this.pdfPage = null;
        this.scale = 1.0;

        this.init();
    }

    async init() {
        // Load PDF using PDF.js
        const pdf = await pdfjsLib.getDocument(this.pdfUrl).promise;
        this.pdfPage = await pdf.getPage(1);
        this.renderPDF();
        this.loadExistingFields();
        this.setupEventListeners();
    }

    renderPDF() {
        const viewport = this.pdfPage.getViewport({ scale: this.scale });
        this.canvas.width = viewport.width;
        this.canvas.height = viewport.height;

        const renderContext = {
            canvasContext: this.ctx,
            viewport: viewport
        };

        this.pdfPage.render(renderContext);
    }

    addField(fieldType, x, y) {
        const field = {
            id: Date.now(),
            fieldType: fieldType,
            x: x,
            y: y,
            width: fieldType.includes('Signature') ? 120 : 200,
            height: fieldType.includes('Signature') ? 40 : 20,
            fontSize: 11,
            fontBold: false
        };

        this.fields.push(field);
        this.drawFields();
        return field;
    }

    drawFields() {
        // Redraw PDF
        this.renderPDF();

        // Draw all field boxes
        this.fields.forEach(field => {
            this.ctx.strokeStyle = field === this.selectedField ? '#007bff' : '#28a745';
            this.ctx.lineWidth = 2;
            this.ctx.strokeRect(field.x, field.y, field.width, field.height);

            // Draw field label
            this.ctx.fillStyle = '#fff';
            this.ctx.fillRect(field.x, field.y - 20, field.width, 20);
            this.ctx.fillStyle = '#000';
            this.ctx.font = '12px Arial';
            this.ctx.fillText(field.fieldType, field.x + 5, field.y - 5);
        });
    }

    async saveFields() {
        // Convert canvas coordinates to PDF points
        const fieldsData = this.fields.map(f => ({
            fieldName: f.fieldType,
            fieldType: f.fieldType.includes('Signature') ? 'signature' : 'text',
            xPosition: f.x / this.scale,
            yPosition: (this.canvas.height - f.y - f.height) / this.scale, // Convert to bottom-left origin
            width: f.width / this.scale,
            height: f.height / this.scale,
            fontSize: f.fontSize,
            fontBold: f.fontBold
        }));

        // POST to server
        const response = await fetch(`/SignTemplates/SaveFieldMapping/${this.templateId}`, {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify(fieldsData)
        });

        if (response.ok) {
            alert('Field configuration saved successfully!');
        }
    }
}
```

## Backend Implementation

### 1. Domain Entity

**File:** `/Domain/Signing/SignTemplateField.cs`

```csharp
public class SignTemplateField
{
    public int FieldId { get; set; }
    public int TemplateId { get; set; }
    public string FieldName { get; set; } = "";
    public string FieldType { get; set; } = "text"; // text, signature, date, checkbox
    public decimal XPosition { get; set; }
    public decimal YPosition { get; set; }
    public decimal? Width { get; set; }
    public decimal? Height { get; set; }
    public int? FontSize { get; set; }
    public bool FontBold { get; set; }
    public int DisplayOrder { get; set; }
    public bool IsRequired { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Navigation
    public SignTemplate Template { get; set; } = null!;
}
```

### 2. Updated PdfService

**File:** `/Services/Signing/PdfService.cs`

```csharp
public async Task<(byte[] bytes, string storagePath, byte[] sha256)> RenderAndSealAsync(long envelopeId)
{
    // ... existing code ...

    // Get template and its field mapping
    var template = await _db.SignTemplates
        .Include(t => t.Fields)
        .FirstOrDefaultAsync(t => t.TemplateKey == env.TemplateKey);

    if (template?.UseCustomFieldMapping == true && template.Fields.Any())
    {
        // Use custom field mapping
        await RenderFieldsFromMapping(gfx, template.Fields, env, tenant, manager);
    }
    else
    {
        // Fall back to hard-coded coordinates for legacy templates
        await RenderFieldsLegacy(gfx, env, tenant, manager);
    }

    // ... rest of code ...
}

private async Task RenderFieldsFromMapping(
    XGraphics gfx,
    IEnumerable<SignTemplateField> fields,
    SignEnvelope env,
    SignRecipient tenant,
    SignRecipient? manager)
{
    // Build data dictionary
    var propertyName = await _merge.GetPropertyNameAsync(env.PropertyID) ?? "";
    var data = new Dictionary<string, string>
    {
        ["PropertyName"] = propertyName,
        ["InstallationDate"] = DateTime.UtcNow.ToString("MM/dd/yyyy"),
        ["UnitNumber"] = env.Property?.Unit ?? "",
        ["PropertyStaffName"] = manager?.FullName ?? "",
        ["PropertyStaffDate"] = manager?.SignedAtUtc?.ToString("MM/dd/yyyy") ?? "",
        ["ResidentName"] = tenant.FullName,
        ["ResidentPhone"] = tenant.Phone ?? ""
    };

    foreach (var field in fields.OrderBy(f => f.DisplayOrder))
    {
        if (field.FieldType == "text" && data.ContainsKey(field.FieldName))
        {
            var font = new XFont("Roboto", field.FontSize ?? 11,
                field.FontBold ? XFontStyle.Bold : XFontStyle.Regular);
            gfx.DrawString(
                data[field.FieldName],
                font,
                XBrushes.Black,
                new XPoint((double)field.XPosition, (double)field.YPosition),
                XStringFormats.Default
            );
        }
        else if (field.FieldType == "signature")
        {
            string? sigPath = field.FieldName.Contains("Staff")
                ? manager?.SignatureImagePath
                : tenant.SignatureImagePath;

            if (!string.IsNullOrEmpty(sigPath))
            {
                var fsPath = Path.Combine("wwwroot", sigPath.TrimStart('/'));
                if (File.Exists(fsPath))
                {
                    using var fs = File.OpenRead(fsPath);
                    using var img = XImage.FromStream(() => fs);
                    gfx.DrawImage(img,
                        (double)field.XPosition,
                        (double)field.YPosition,
                        (double)(field.Width ?? 120),
                        (double)(field.Height ?? 40));
                }
            }
        }
    }
}
```

### 3. Controller Actions

**File:** `/Controllers/SignTemplatesController.cs`

```csharp
[Authorize(Roles = "Admin")]
[HttpGet("ConfigureFields/{id}")]
public async Task<IActionResult> ConfigureFields(int id)
{
    var template = await _db.SignTemplates
        .Include(t => t.Fields)
        .FirstOrDefaultAsync(t => t.TemplateId == id);

    if (template == null) return NotFound();

    return View(template);
}

[Authorize(Roles = "Admin")]
[HttpPost("SaveFieldMapping/{id}")]
public async Task<IActionResult> SaveFieldMapping(int id, [FromBody] List<SignTemplateFieldDto> fields)
{
    var template = await _db.SignTemplates.FindAsync(id);
    if (template == null) return NotFound();

    // Remove existing fields
    var existing = await _db.SignTemplateFields.Where(f => f.TemplateId == id).ToListAsync();
    _db.SignTemplateFields.RemoveRange(existing);

    // Add new fields
    foreach (var dto in fields)
    {
        _db.SignTemplateFields.Add(new SignTemplateField
        {
            TemplateId = id,
            FieldName = dto.FieldName,
            FieldType = dto.FieldType,
            XPosition = dto.XPosition,
            YPosition = dto.YPosition,
            Width = dto.Width,
            Height = dto.Height,
            FontSize = dto.FontSize,
            FontBold = dto.FontBold,
            DisplayOrder = dto.DisplayOrder
        });
    }

    template.UseCustomFieldMapping = true;
    await _db.SaveChangesAsync();

    return Ok();
}
```

## Migration Path

1. **Phase 1:** Create database schema
2. **Phase 2:** Build UI configurator
3. **Phase 3:** Update PdfService to use field mappings
4. **Phase 4:** Migrate existing templates (optional)

## Benefits

✅ No code changes needed for new PDF templates
✅ Visual configuration - no coordinate guesswork
✅ Template reusability
✅ Easy maintenance and updates
✅ Support for multiple template variations

## Next Steps

Would you like me to implement this feature? I can start with:
1. Database migration
2. Basic configurator UI
3. Updated PdfService integration
