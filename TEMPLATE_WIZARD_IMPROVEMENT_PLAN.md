# Template Creation Wizard - Improvement Plan

## Problem Statement

The current template creation form is too technical for non-developer users:
- Manual JSON entry for MergeSpec
- Unclear Razor view path selection
- No visual feedback on field mapping
- No connection between PDF and data fields

## Proposed Solution: Step-by-Step Template Wizard

### Step 1: Basic Information
- Template Key (auto-generated from Display Name)
- Display Name
- Template Category (dropdown: "Installation Release", "Work Order", "Inspection", etc.)
- Active status

### Step 2: Upload PDF & Auto-Detect Fields
- Upload PDF template
- System analyzes PDF and suggests field locations (optional)
- Preview PDF with grid overlay

### Step 3: Select Available Fields (Checkbox List)
Instead of JSON, show a visual field selector:

#### Property Fields
☑ Property Name
☑ Property Address
☑ Property City, State, Zip
☐ Property Manager Name
☐ Property Phone

#### Work Order Fields
☑ Installation Date
☑ Unit Number
☐ Delivery Date
☐ Work Order Number

#### Signature Fields
☑ Property Staff Name
☑ Property Staff Signature
☑ Property Staff Date
☑ Resident Name
☑ Resident Signature
☑ Resident Phone Number

#### Custom Fields
[+ Add Custom Field]

### Step 4: Position Fields on PDF (Visual Configurator)
- Drag and drop fields onto PDF preview
- Click to place fields
- Adjust size and font properties
- Real-time preview

### Step 5: Configure Document Content
- Select or create Razor view template
- Default subject line
- Default email message
- Preview final document

### Step 6: Review & Save
- Summary of all settings
- Test envelope creation
- Save and activate

---

## Technical Implementation

### Database Changes

1. **Add SignTemplateField table** (already designed in TEMPLATE_CONFIGURATOR_PLAN.md)

2. **Update SignTemplate table:**
```sql
ALTER TABLE SignTemplates
ADD TemplateCategory NVARCHAR(100) NULL,
    UseFieldMapping BIT DEFAULT 1,
    FieldMappingJson NVARCHAR(MAX) NULL, -- Store field selections
    CreatedByUserId INT NULL,
    ModifiedByUserId INT NULL
```

### New Model: Template Field Definitions

```csharp
public class TemplateFieldDefinition
{
    public string FieldKey { get; set; }
    public string DisplayName { get; set; }
    public string Category { get; set; } // "Property", "Order", "Signature", "Custom"
    public string DataType { get; set; } // "text", "signature", "date", "checkbox"
    public string DefaultValue { get; set; }
    public bool IsSystemField { get; set; }
}
```

### Available System Fields

```csharp
public static class SystemFieldDefinitions
{
    public static List<TemplateFieldDefinition> GetAll() => new()
    {
        // Property Fields
        new() { FieldKey = "PropertyName", DisplayName = "Property Name", Category = "Property", DataType = "text", IsSystemField = true },
        new() { FieldKey = "PropertyAddress", DisplayName = "Property Address", Category = "Property", DataType = "text", IsSystemField = true },
        new() { FieldKey = "PropertyCity", DisplayName = "City", Category = "Property", DataType = "text", IsSystemField = true },
        new() { FieldKey = "PropertyState", DisplayName = "State", Category = "Property", DataType = "text", IsSystemField = true },
        new() { FieldKey = "PropertyZip", DisplayName = "Zip Code", Category = "Property", DataType = "text", IsSystemField = true },
        new() { FieldKey = "PropertyManagerName", DisplayName = "Property Manager", Category = "Property", DataType = "text", IsSystemField = true },
        new() { FieldKey = "PropertyPhone", DisplayName = "Property Phone", Category = "Property", DataType = "text", IsSystemField = true },

        // Work Order Fields
        new() { FieldKey = "InstallationDate", DisplayName = "Installation Date", Category = "Work Order", DataType = "date", IsSystemField = true },
        new() { FieldKey = "UnitNumber", DisplayName = "Unit Number", Category = "Work Order", DataType = "text", IsSystemField = true },
        new() { FieldKey = "DeliveryDate", DisplayName = "Delivery Date", Category = "Work Order", DataType = "date", IsSystemField = true },
        new() { FieldKey = "WorkOrderNumber", DisplayName = "Work Order #", Category = "Work Order", DataType = "text", IsSystemField = true },

        // Signature Fields
        new() { FieldKey = "PropertyStaffName", DisplayName = "Property Staff Name", Category = "Signatures", DataType = "text", IsSystemField = true },
        new() { FieldKey = "PropertyStaffSignature", DisplayName = "Property Staff Signature", Category = "Signatures", DataType = "signature", IsSystemField = true },
        new() { FieldKey = "PropertyStaffDate", DisplayName = "Property Staff Date", Category = "Signatures", DataType = "date", IsSystemField = true },
        new() { FieldKey = "ResidentName", DisplayName = "Resident Name", Category = "Signatures", DataType = "text", IsSystemField = true },
        new() { FieldKey = "ResidentSignature", DisplayName = "Resident Signature", Category = "Signatures", DataType = "signature", IsSystemField = true },
        new() { FieldKey = "ResidentPhone", DisplayName = "Resident Phone", Category = "Signatures", DataType = "text", IsSystemField = true },
        new() { FieldKey = "ResidentEmail", DisplayName = "Resident Email", Category = "Signatures", DataType = "text", IsSystemField = true }
    };
}
```

---

## UI Improvements

### Step 3: Field Selection UI (Replace JSON Editor)

```html
<div class="field-selector">
    <h4>Select Fields to Include in This Template</h4>

    <!-- Property Fields -->
    <div class="card mb-3">
        <div class="card-header">
            <input type="checkbox" id="selectAllProperty" /> Property Information
        </div>
        <div class="card-body">
            <div class="row">
                <div class="col-md-4">
                    <div class="form-check">
                        <input class="form-check-input" type="checkbox" name="fields" value="PropertyName" id="field_PropertyName" checked>
                        <label class="form-check-label" for="field_PropertyName">Property Name</label>
                    </div>
                </div>
                <div class="col-md-4">
                    <div class="form-check">
                        <input class="form-check-input" type="checkbox" name="fields" value="PropertyAddress" id="field_PropertyAddress">
                        <label class="form-check-label" for="field_PropertyAddress">Property Address</label>
                    </div>
                </div>
                <div class="col-md-4">
                    <div class="form-check">
                        <input class="form-check-input" type="checkbox" name="fields" value="PropertyPhone" id="field_PropertyPhone">
                        <label class="form-check-label" for="field_PropertyPhone">Property Phone</label>
                    </div>
                </div>
            </div>
        </div>
    </div>

    <!-- Work Order Fields -->
    <div class="card mb-3">
        <div class="card-header">
            <input type="checkbox" id="selectAllOrder" /> Work Order Information
        </div>
        <div class="card-body">
            <div class="row">
                <div class="col-md-4">
                    <div class="form-check">
                        <input class="form-check-input" type="checkbox" name="fields" value="InstallationDate" id="field_InstallationDate" checked>
                        <label class="form-check-label" for="field_InstallationDate">Installation Date</label>
                    </div>
                </div>
                <div class="col-md-4">
                    <div class="form-check">
                        <input class="form-check-input" type="checkbox" name="fields" value="UnitNumber" id="field_UnitNumber" checked>
                        <label class="form-check-label" for="field_UnitNumber">Unit Number</label>
                    </div>
                </div>
            </div>
        </div>
    </div>

    <!-- Signature Fields -->
    <div class="card mb-3">
        <div class="card-header">
            <input type="checkbox" id="selectAllSignatures" /> Signature Fields
        </div>
        <div class="card-body">
            <div class="row">
                <div class="col-md-6">
                    <h6>Property Staff</h6>
                    <div class="form-check">
                        <input class="form-check-input" type="checkbox" name="fields" value="PropertyStaffName" id="field_PropertyStaffName" checked>
                        <label class="form-check-label" for="field_PropertyStaffName">Staff Name</label>
                    </div>
                    <div class="form-check">
                        <input class="form-check-input" type="checkbox" name="fields" value="PropertyStaffSignature" id="field_PropertyStaffSignature" checked>
                        <label class="form-check-label" for="field_PropertyStaffSignature">Staff Signature</label>
                    </div>
                    <div class="form-check">
                        <input class="form-check-input" type="checkbox" name="fields" value="PropertyStaffDate" id="field_PropertyStaffDate" checked>
                        <label class="form-check-label" for="field_PropertyStaffDate">Staff Date</label>
                    </div>
                </div>
                <div class="col-md-6">
                    <h6>Resident</h6>
                    <div class="form-check">
                        <input class="form-check-input" type="checkbox" name="fields" value="ResidentName" id="field_ResidentName" checked>
                        <label class="form-check-label" for="field_ResidentName">Resident Name</label>
                    </div>
                    <div class="form-check">
                        <input class="form-check-input" type="checkbox" name="fields" value="ResidentSignature" id="field_ResidentSignature" checked>
                        <label class="form-check-label" for="field_ResidentSignature">Resident Signature</label>
                    </div>
                    <div class="form-check">
                        <input class="form-check-input" type="checkbox" name="fields" value="ResidentPhone" id="field_ResidentPhone" checked>
                        <label class="form-check-label" for="field_ResidentPhone">Resident Phone</label>
                    </div>
                </div>
            </div>
        </div>
    </div>

    <!-- Custom Fields Section -->
    <div class="card mb-3">
        <div class="card-header">Custom Fields</div>
        <div class="card-body">
            <div id="customFieldsList"></div>
            <button type="button" class="btn btn-sm btn-outline-primary" id="addCustomField">
                <i class="bi bi-plus-circle"></i> Add Custom Field
            </button>
        </div>
    </div>
</div>

<!-- Hidden field to store selected fields as JSON -->
<input type="hidden" name="FieldMappingJson" id="fieldMappingJson" />
```

### JavaScript to Convert Checkboxes to JSON

```javascript
// Auto-generate MergeSpec JSON from checkbox selections
function generateMergeSpec() {
    const selectedFields = [];
    document.querySelectorAll('input[name="fields"]:checked').forEach(checkbox => {
        selectedFields.push({
            fieldKey: checkbox.value,
            dataSource: getDataSourceForField(checkbox.value),
            required: true
        });
    });

    document.getElementById('fieldMappingJson').value = JSON.stringify(selectedFields);
}

function getDataSourceForField(fieldKey) {
    const propertyFields = ['PropertyName', 'PropertyAddress', 'PropertyCity', 'PropertyState', 'PropertyZip'];
    const orderFields = ['InstallationDate', 'UnitNumber', 'DeliveryDate'];

    if (propertyFields.includes(fieldKey)) return 'Property';
    if (orderFields.includes(fieldKey)) return 'WorkOrder';
    return 'Recipient';
}

// Auto-update on checkbox change
document.querySelectorAll('input[name="fields"]').forEach(checkbox => {
    checkbox.addEventListener('change', generateMergeSpec);
});
```

---

## Simplified Razor View Selection

Instead of dropdown with technical paths, show cards:

```html
<div class="razor-view-selector">
    <h4>Select Document Template</h4>

    <div class="row">
        <div class="col-md-6">
            <div class="card template-card" data-view="/Views/SignTemplates/OccupiedReleaseForm.cshtml">
                <div class="card-body">
                    <h5>Occupied Installation Release</h5>
                    <p class="text-muted">Standard release form for occupied unit installations</p>
                    <span class="badge bg-info">Most Popular</span>
                </div>
            </div>
        </div>

        <div class="col-md-6">
            <div class="card template-card" data-view="/Views/SignTemplates/WorkOrderConsent.cshtml">
                <div class="card-body">
                    <h5>Work Order Consent</h5>
                    <p class="text-muted">General consent form for work orders</p>
                </div>
            </div>
        </div>

        <div class="col-md-6">
            <div class="card template-card" data-view="/Views/SignTemplates/CustomTemplate.cshtml">
                <div class="card-body">
                    <h5>Custom Template</h5>
                    <p class="text-muted">Create a new custom template from scratch</p>
                </div>
            </div>
        </div>
    </div>

    <input type="hidden" name="RazorViewPath" id="razorViewPath" />
</div>
```

---

## Improved Process Flow

### Old Flow (Current):
1. User fills out confusing form
2. User manually enters JSON
3. User uploads PDF (maybe)
4. Template saved with no validation
5. User hopes it works when creating envelope

### New Flow (Proposed):
1. **Step 1:** Basic info (name, category, active)
   - Auto-generate template key from display name
   - Show helpful tooltips

2. **Step 2:** Upload PDF
   - Required for PDF-based templates
   - Optional for HTML-only templates
   - Show PDF preview with page dimensions

3. **Step 3:** Select fields (checkboxes)
   - Visual field selector grouped by category
   - Add custom fields inline
   - System auto-generates JSON behind the scenes

4. **Step 4:** Position fields on PDF
   - Interactive PDF configurator (from TEMPLATE_CONFIGURATOR_PLAN.md)
   - Drag-and-drop field placement
   - Save coordinates to database

5. **Step 5:** Configure document
   - Select Razor view template (visual cards)
   - Set default subject and message
   - Preview final document

6. **Step 6:** Test & Save
   - Create test envelope button
   - Validation checks
   - Save and activate

---

## Additional Features

### 1. Template Duplication
- "Duplicate Template" button on template list
- Creates copy with "(Copy)" suffix
- Allows easy creation of similar templates

### 2. Template Preview
- "Preview" button shows how envelope will look
- Mock data preview
- Test signing flow without creating real envelope

### 3. Template Categories
- Filter templates by category
- Quick templates (frequently used)
- Recently used templates

### 4. Field Library Management
- Admin can add new system fields
- Define custom field types
- Set validation rules

### 5. Template Validation
- Check all required fields are mapped
- Verify PDF coordinates don't overlap
- Ensure Razor view exists
- Test signature field placements

---

## Benefits of New Approach

✅ **User-Friendly:** No JSON or technical knowledge required
✅ **Visual:** See exactly what you're creating
✅ **Validated:** Catch errors before template is used
✅ **Flexible:** Easy to add/remove fields
✅ **Reusable:** Duplicate and modify existing templates
✅ **Self-Documenting:** Clear labels and tooltips
✅ **Testable:** Preview and test before going live

---

## Implementation Priority

### Phase 1 (Quick Wins):
1. Replace JSON editor with checkbox field selector
2. Add visual Razor view selector (cards instead of dropdown)
3. Add template duplication feature

### Phase 2 (Medium Term):
1. Implement multi-step wizard
2. Add PDF preview on upload
3. Create template categories

### Phase 3 (Long Term):
1. Build visual PDF field configurator
2. Add template testing feature
3. Implement field library management

Would you like me to implement Phase 1 first?
