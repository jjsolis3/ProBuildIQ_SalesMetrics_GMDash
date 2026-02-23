// Services/Signing/EnvelopeService.cs
using Microsoft.EntityFrameworkCore;
using SalesMetrics.Data;
using SalesMetrics.Domain.Signing;
using SalesMetrics.Models.Signing;
using SalesMetrics.Utilities.Security;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SalesMetrics.Services.Signing; // for AppSettings
using System.Text.Json;

// Alias to disambiguate from the email INotificationService in this namespace
using IAppNotificationService = SalesMetrics.Services.Notifications.INotificationService;

namespace SalesMetrics.Services.Signing;

public sealed class EnvelopeService : IEnvelopeService
{
    private readonly SalesMetricsDbContext _db;
    private readonly INotificationService _notify;          // email (SMTP) notification service
    private readonly IAppNotificationService _appNotify;    // in-app + SignalR notification service
    private readonly IErpMergeService _merge;
    private readonly IPdfService _pdf;
    private readonly AppSettings _appSettings;
    private readonly IEmailTemplateService _emailTemplate;
    private readonly ILogger<EnvelopeService> _logger;

    public EnvelopeService(SalesMetricsDbContext db, INotificationService notify, IAppNotificationService appNotify, IErpMergeService merge, IPdfService pdf, IOptions<AppSettings> appSettings, IEmailTemplateService emailTemplate, ILogger<EnvelopeService> logger)
    {
        _db = db; _notify = notify; _appNotify = appNotify; _merge = merge; _pdf = pdf; _appSettings = appSettings.Value; _emailTemplate = emailTemplate; _logger = logger;
    }

    public async Task<long> CreateAsync(int createdByUsersId, CreateEnvelopeVm vm)
    {
        // Load the template to access MergeSpec and PDF path
        var template = await _db.SignTemplates
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.TemplateKey == vm.TemplateKey);

        if (template == null)
            throw new InvalidOperationException($"Template '{vm.TemplateKey}' not found");

        // Fetch OrderNumber from ERP if OrderId is provided; fall back to manually-entered value
        string? orderNumber = null;
        if (vm.OrderId.HasValue)
        {
            var orders = await _merge.GetOrdersForPropertyAsync(vm.PropertyID ?? 0);
            var matchingOrder = orders.FirstOrDefault(o => o.OrderID == vm.OrderId.Value.ToString());
            orderNumber = matchingOrder?.OrderID; // OrderID contains the display number like "90805.4"
        }
        else if (!string.IsNullOrWhiteSpace(vm.CustomOrderNumber))
        {
            orderNumber = vm.CustomOrderNumber.Trim();
        }

        var env = new SignEnvelope
        {
            TemplateKey = vm.TemplateKey,
            Subject = vm.Subject,
            MessageBody = vm.MessageBody,
            PropertyID = vm.PropertyID,
            PropertyName = !vm.PropertyID.HasValue ? vm.CustomPropertyName : null, // only store free-text when no ERP match
            OrderId = vm.OrderId,
            OrderNumber = orderNumber,
            CustomerNumber = vm.CustomerNumber,
            LocationCode = vm.LocationCode,
            Status = "Draft",
            ExpiresAtUtc = vm.ExpiresAtUtc,
            CreatedByUsers_ID = createdByUsersId,
            CreatedDateUtc = DateTime.UtcNow
        };

        foreach (var r in vm.Recipients)
        {
            env.Recipients.Add(new SignRecipient
            {
                Role = r.Role,
                SignerOrder = r.SignerOrder,
                FullName = r.FullName,
                Email = r.Email,
                Phone = r.Phone,
                AccessToken = TokenHelper.CreateSecureToken(32),
                AccessTokenExpiresAt = vm.ExpiresAtUtc
            });
        }

        _db.SignEnvelopes.Add(env);
        await _db.SaveChangesAsync(); // Save to get EnvelopeId

        // Create SignAttachment record for template PDF
        if (!string.IsNullOrWhiteSpace(template.PdfFilePath))
        {
            env.Attachments = new List<SignAttachment>
            {
                new SignAttachment
                {
                    EnvelopeId = env.EnvelopeId,
                    FileName = template.PdfFilePath,
                    BlobPath = Path.Combine("Content", "Templates", template.PdfFilePath),
                    MimeType = "application/pdf",
                    UploadedByUsers_ID = createdByUsersId,
                    UploadedDateUtc = DateTime.UtcNow
                }
            };
        }

        // Create SignField records from template MergeSpec
        await CreateFieldsFromMergeSpecAsync(env, template, vm);

        // When no ERP order was selected, store a manually-entered unit number as a SignField
        if (!vm.OrderId.HasValue && !string.IsNullOrWhiteSpace(vm.CustomUnitNumber))
        {
            var existingUnitField = env.Fields?.FirstOrDefault(f => f.FieldKey == "UnitNumber");
            if (existingUnitField != null)
                existingUnitField.FieldValue = vm.CustomUnitNumber.Trim();
            else
                _db.SignFields.Add(new SignField { EnvelopeId = env.EnvelopeId, FieldKey = "UnitNumber", FieldValue = vm.CustomUnitNumber.Trim() });
        }

        await _db.SaveChangesAsync();
        return env.EnvelopeId;
    }

    /// <summary>
    /// Create SignField records from template's MergeSpec JSON
    /// </summary>
    private async Task CreateFieldsFromMergeSpecAsync(SignEnvelope env, SignTemplate template, CreateEnvelopeVm vm)
    {
        if (string.IsNullOrWhiteSpace(template.MergeSpecJson))
        {
            _logger.LogDebug("No MergeSpec found for template {TemplateKey}", template.TemplateKey);
            return;
        }

        try
        {
            _logger.LogDebug("Parsing MergeSpec for envelope {EnvelopeId}", env.EnvelopeId);
            var mergeSpec = JsonSerializer.Deserialize<MergeSpec>(template.MergeSpecJson);

            if (mergeSpec?.fieldMapping == null || mergeSpec.fieldMapping.Count == 0)
            {
                _logger.LogDebug("No field mappings found in MergeSpec");
                return;
            }

            _logger.LogDebug("Found {FieldCount} fields in MergeSpec", mergeSpec.fieldMapping.Count);

            // Gather field data using the merge service
            var fieldData = await GatherFieldDataForEnvelopeAsync(env);

            env.Fields = new List<SignField>();

            foreach (var (fieldKey, fieldInfo) in mergeSpec.fieldMapping)
            {
                // Determine which recipient this field belongs to
                long? recipientId = null;
                if (!string.IsNullOrWhiteSpace(fieldInfo.role))
                {
                    var recipient = env.Recipients.FirstOrDefault(r =>
                        r.Role.Equals(fieldInfo.role, StringComparison.OrdinalIgnoreCase));
                    recipientId = recipient?.RecipientId;
                }

                // Get the field value if available
                string? fieldValue = null;
                if (fieldData.TryGetValue(fieldKey, out var value))
                {
                    fieldValue = value?.ToString();
                }

                // Determine field type and map to valid database values
                // Database constraint allows: text, checkbox, initials, date
                var fieldType = fieldInfo.type switch
                {
                    "signature" => "text", // Signatures are stored as file paths (text)
                    "checkbox" => "checkbox",
                    "initials" => "initials",
                    "date" => "date",
                    _ => "text" // Default to text
                };

                var field = new SignField
                {
                    EnvelopeId = env.EnvelopeId,
                    RecipientId = recipientId,
                    FieldKey = fieldKey,
                    FieldType = fieldType,
                    FieldValue = fieldValue
                };

                env.Fields.Add(field);
                _logger.LogDebug("Created field: {FieldKey} (type: {FieldType}, role: {Role}, value: {FieldValue})", fieldKey, fieldType, fieldInfo.role ?? "N/A", fieldValue ?? "NULL");
            }

            _logger.LogDebug("Created {FieldCount} SignField records", env.Fields.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to parse MergeSpec for envelope {EnvelopeId}", env.EnvelopeId);
            // Don't throw - allow envelope creation to continue without fields
        }
    }

    /// <summary>
    /// Gather field data for envelope creation
    /// </summary>
    private async Task<Dictionary<string, object>> GatherFieldDataForEnvelopeAsync(SignEnvelope env)
    {
        var data = new Dictionary<string, object>();

        try
        {
            // Property fields
            var propertyName = await _merge.GetPropertyNameAsync(env.PropertyID) ?? "";
            var propertyAddress = await _merge.GetPropertyAddressAsync(env.PropertyID) ?? "";
            var propertyPhone = await _merge.GetCustomerPhoneAsync(env.PropertyID) ?? "";
            var customerEmail = await _merge.GetCustomerEmailAsync(env.PropertyID) ?? "";
            var customerName = await _merge.GetCustomerNameAsync(env.PropertyID) ?? propertyName;

            data["PropertyName"] = propertyName;
            data["PropertyAddress"] = propertyAddress;
            data["PropertyPhone"] = propertyPhone;
            data["PropertyCity"] = await _merge.GetPropertyCityAsync(env.PropertyID) ?? "";
            data["PropertyState"] = await _merge.GetPropertyStateAsync(env.PropertyID) ?? "";
            data["PropertyZip"] = await _merge.GetPropertyZipAsync(env.PropertyID) ?? "";

            data["CustomerName"] = customerName;
            data["CustomerEmail"] = customerEmail;
            data["CustomerPhone"] = propertyPhone; // Same as property phone
            data["CustomerCompany"] = customerName; // Company name is the customer name in this ERP

            // Order number already resolved and stored on envelope during creation
            data["OrderNumber"] = env.OrderNumber ?? "";

            // Order fields
            var unitNumber = await _merge.GetUnitNumberByOrderIdAsync(env.OrderId) ?? "";
            var orderStartDate = await _merge.GetOrderStartDateAsync(env.OrderId);
            var orderEndDate = await _merge.GetOrderEndDateAsync(env.OrderId);
            var orderSignedDate = await _merge.GetOrderSignedDateAsync(env.OrderId);

            data["UnitNumber"] = unitNumber;
            data["OrderStartDate"] = orderStartDate?.ToString("MM/dd/yyyy") ?? "";
            data["OrderEndDate"] = orderEndDate?.ToString("MM/dd/yyyy") ?? "";
            data["OrderSignedDate"] = orderSignedDate?.ToString("MM/dd/yyyy") ?? "";
            data["InstallationDate"] = orderStartDate?.ToString("MM/dd/yyyy") ?? DateTime.UtcNow.ToString("MM/dd/yyyy");
            data["DeliveryDate"] = orderStartDate?.ToString("MM/dd/yyyy") ?? "";
            data["LeaseStartDate"] = orderStartDate?.ToString("MM/dd/yyyy") ?? "";
            data["LeaseEndDate"] = orderEndDate?.ToString("MM/dd/yyyy") ?? "";

            // Envelope metadata
            data["EnvelopeSubject"] = env.Subject;
            data["EnvelopeMessage"] = env.MessageBody ?? "";
            data["LocationCode"] = env.LocationCode ?? "";
            data["CurrentDate"] = DateTime.UtcNow.ToString("MM/dd/yyyy");

            // Recipient fields - these will be populated at signing time
            var manager = env.Recipients.FirstOrDefault(r => r.Role == "Manager");
            var tenant = env.Recipients.FirstOrDefault(r => r.Role == "Tenant");

            data["PropertyStaffName"] = manager?.FullName ?? "Property Staff";
            data["PropertyStaffEmail"] = manager?.Email ?? "";
            data["PropertyStaffPhone"] = manager?.Phone ?? "";
            data["PropertyStaffDate"] = ""; // Will be filled when signed
            data["PropertyStaffSignature"] = ""; // Will be filled when signed

            // Only populate resident fields from actual Tenant recipient (not Manager fallback)
            data["ResidentName"] = tenant?.FullName ?? "";
            data["ResidentEmail"] = tenant?.Email ?? "";
            data["ResidentPhone"] = tenant?.Phone ?? "";
            data["ResidentSignature"] = ""; // Will be filled when signed
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to gather field data for envelope {EnvelopeId}", env.EnvelopeId);
        }

        return data;
    }

    // MergeSpec JSON structure classes
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
        public Coordinates? coordinates { get; set; }
        public List<Coordinates>? placements { get; set; }
    }

    private class Coordinates
    {
        public int page { get; set; }
        public double x { get; set; }
        public double y { get; set; }
        public double width { get; set; }
        public double height { get; set; }
    }

    public async Task SendAsync(long envelopeId)
    {
        var env = await _db.SignEnvelopes.Include(e => e.Recipients).FirstOrDefaultAsync(e => e.EnvelopeId == envelopeId)
            ?? throw new InvalidOperationException($"Envelope {envelopeId} not found");
        if (env.Status == "Draft") env.Status = "Sent";
        env.SentAtUtc = env.SentAtUtc ?? DateTime.UtcNow;
        await _db.SaveChangesAsync();

        // Fetch property context once before the recipient loop
        string? sendPropertyName = null;
        string? sendPropertyAddress = null;
        if (env.PropertyID.HasValue)
        {
            try
            {
                sendPropertyName    = await _merge.GetPropertyNameAsync(env.PropertyID.Value, env.LocationCode);
                sendPropertyAddress = await _merge.GetPropertyAddressAsync(env.PropertyID.Value, env.LocationCode);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Could not fetch property for invitation email (envelope {EnvelopeId})", envelopeId);
            }
        }
        sendPropertyName ??= env.PropertyName; // fall back to stored free-text name

        // Build property/order context block (shown in email, not part of MessageBody)
        var contextRows = "";
        if (!string.IsNullOrWhiteSpace(sendPropertyName))
            contextRows += $"<tr><td style=\"padding:4px 16px 4px 0;color:#6b7280;font-size:13px;white-space:nowrap;\">Property</td><td style=\"padding:4px 0;font-size:13px;\">{System.Net.WebUtility.HtmlEncode(sendPropertyName)}</td></tr>";
        if (!string.IsNullOrWhiteSpace(sendPropertyAddress))
            contextRows += $"<tr><td style=\"padding:4px 16px 4px 0;color:#6b7280;font-size:13px;white-space:nowrap;\">Address</td><td style=\"padding:4px 0;font-size:13px;\">{System.Net.WebUtility.HtmlEncode(sendPropertyAddress)}</td></tr>";
        if (!string.IsNullOrWhiteSpace(env.OrderNumber))
            contextRows += $"<tr><td style=\"padding:4px 16px 4px 0;color:#6b7280;font-size:13px;white-space:nowrap;\">Order #</td><td style=\"padding:4px 0;font-size:13px;\">{System.Net.WebUtility.HtmlEncode(env.OrderNumber)}</td></tr>";
        if (!string.IsNullOrWhiteSpace(env.LocationCode))
            contextRows += $"<tr><td style=\"padding:4px 16px 4px 0;color:#6b7280;font-size:13px;white-space:nowrap;\">Branch</td><td style=\"padding:4px 0;font-size:13px;\">{System.Net.WebUtility.HtmlEncode(env.LocationCode)}</td></tr>";

        var contextHtml = string.IsNullOrEmpty(contextRows) ? "" : $@"
            <table cellpadding=""0"" cellspacing=""0"" border=""0"" style=""margin:12px 0 16px 0;border-left:3px solid #f59e0b;padding-left:12px;"">
                {contextRows}
            </table>";

        // Resolve Reply-To once — branch-specific email takes priority; falls back to company-wide.
        // This lets customers who print and reply by email reach the correct branch inbox
        // rather than the generic sending account.
        var replyToEmail = await GetReplyToEmailAsync(env.LocationCode);

        foreach (var r in env.Recipients.OrderBy(x => x.SignerOrder))
        {
            var baseUrl = _appSettings.BaseUrl.TrimEnd('/');
            var link = $"{baseUrl}/sign/{r.AccessToken}";

            // Optional staff message
            var messageHtml = !string.IsNullOrWhiteSpace(env.MessageBody)
                ? $"<p style=\"margin:12px 0;font-size:14px;color:#374151;\">{env.MessageBody}</p>"
                : "";

            var innerHtml = $@"
                <h2 style=""margin:0 0 16px 0;font-size:20px;color:#1e293b;font-weight:600;"">Envelope Signing Request</h2>
                <p style=""margin:0 0 12px 0;font-size:15px;color:#374151;"">Hello {r.FullName},</p>
                <p style=""margin:0 0 8px 0;font-size:15px;color:#374151;"">Please review and sign the envelope: <strong>{env.Subject}</strong>.</p>
                {contextHtml}
                {messageHtml}
                <table role=""presentation"" cellpadding=""0"" cellspacing=""0"" border=""0"" style=""margin:24px 0;"">
                  <tr>
                    <td align=""center"" style=""background-color:#3b82f6;border-radius:6px;"">
                      <a href=""{link}"" style=""display:inline-block;padding:12px 32px;font-size:16px;font-weight:600;color:#ffffff;text-decoration:none;border-radius:6px;"">Open &amp; Sign</a>
                    </td>
                  </tr>
                </table>
                <p style=""margin:0;font-size:12px;color:#9ca3af;"">If the button above doesn't work, copy and paste this link into your browser:</p>
                <p style=""margin:4px 0 0 0;font-size:12px;color:#3b82f6;word-break:break-all;""><a href=""{link}"" style=""color:#3b82f6;"">{link}</a></p>";

            var html = await _emailTemplate.WrapInBrandedTemplateAsync(innerHtml);
            await _notify.SendEnvelopeEmailAsync(r.Email, r.FullName, env.Subject, html, replyToEmail);

            _db.SignEvents.Add(new SignEvent { EnvelopeId = env.EnvelopeId, RecipientId = r.RecipientId, EventType = "Sent", OccurredAtUtc = DateTime.UtcNow });
        }
        await _db.SaveChangesAsync();
    }

    public async Task EditEnvelopeAsync(EditEnvelopeVm vm, int modifiedByUserId)
    {
        var env = await _db.SignEnvelopes
            .Include(e => e.Recipients)
            .Include(e => e.Fields)
            .FirstOrDefaultAsync(e => e.EnvelopeId == vm.EnvelopeId)
            ?? throw new InvalidOperationException($"Envelope {vm.EnvelopeId} not found");

        if (env.Status == "Completed")
            throw new InvalidOperationException("Completed envelopes cannot be edited.");
        if (env.Status == "Voided")
            throw new InvalidOperationException("Voided envelopes cannot be edited.");

        var changes = new List<string>();

        // ── Envelope header fields ──────────────────────────────────────────
        if (env.Subject != vm.Subject)
        {
            env.Subject = vm.Subject;
            changes.Add("Subject");
        }
        if (env.MessageBody != vm.MessageBody)
        {
            env.MessageBody = vm.MessageBody;
            changes.Add("MessageBody");
        }

        // ── Context fields (correctable after send) ─────────────────────────
        var newPropertyName = string.IsNullOrWhiteSpace(vm.PropertyName) ? null : vm.PropertyName.Trim();
        if (env.PropertyName != newPropertyName)
        {
            env.PropertyName = newPropertyName;
            changes.Add("PropertyName");
        }

        var newOrderNumber = string.IsNullOrWhiteSpace(vm.OrderNumber) ? null : vm.OrderNumber.Trim();
        if (env.OrderNumber != newOrderNumber)
        {
            env.OrderNumber = newOrderNumber;
            changes.Add("OrderNumber");
        }

        // UnitNumber lives in SignFields; upsert the row
        var newUnitNumber = string.IsNullOrWhiteSpace(vm.UnitNumber) ? null : vm.UnitNumber.Trim();
        var unitField = env.Fields?.FirstOrDefault(f => f.FieldKey == "UnitNumber");
        if (unitField != null)
        {
            if (unitField.FieldValue != newUnitNumber)
            {
                unitField.FieldValue = newUnitNumber ?? "";
                changes.Add("UnitNumber");
            }
        }
        else if (!string.IsNullOrEmpty(newUnitNumber))
        {
            _db.SignFields.Add(new SignField
            {
                EnvelopeId  = env.EnvelopeId,
                FieldKey    = "UnitNumber",
                FieldValue  = newUnitNumber
            });
            changes.Add("UnitNumber");
        }

        env.ModifiedByUsers_ID = modifiedByUserId;
        env.ModifiedDateUtc    = DateTime.UtcNow;

        // ── Unsigned recipients ─────────────────────────────────────────────
        var recipientsToResend = new List<SignRecipient>();

        foreach (var editR in vm.Recipients)
        {
            var existing = env.Recipients.FirstOrDefault(r => r.RecipientId == editR.RecipientId);
            // Never modify a recipient who has already signed
            if (existing == null || existing.SignedAtUtc.HasValue)
                continue;

            bool emailChanged = !string.Equals(existing.Email, editR.Email, StringComparison.OrdinalIgnoreCase);
            bool nameChanged  = existing.FullName != editR.FullName;
            bool phoneChanged = existing.Phone    != editR.Phone;

            if (emailChanged || nameChanged || phoneChanged)
            {
                existing.FullName = editR.FullName;
                existing.Phone    = editR.Phone;
                changes.Add($"Recipient:{existing.Role}");

                if (emailChanged)
                {
                    existing.Email = editR.Email;
                    // Invalidate old link — generate a fresh secure token
                    existing.AccessToken     = Utilities.Security.TokenHelper.CreateSecureToken(32);
                    // Clear view tracking so the new recipient starts fresh
                    existing.ViewedAtUtc     = null;
                    existing.IPAddressViewed = null;
                    existing.UserAgentViewed = null;
                    // An email change always triggers a resend
                    editR.ResendInvite = true;
                }

                if (editR.ResendInvite)
                    recipientsToResend.Add(existing);
            }
        }

        if (changes.Count > 0)
        {
            _db.SignEvents.Add(new SignEvent
            {
                EnvelopeId    = env.EnvelopeId,
                EventType     = "Edited",
                OccurredAtUtc = DateTime.UtcNow,
                MetaJson      = JsonSerializer.Serialize(new { modifiedByUserId, changes })
            });
        }

        await _db.SaveChangesAsync();

        // ── Re-send invitations to affected unsigned recipients ─────────────
        // Build property context block once for all recipients
        string? editResendPropertyName = null;
        string? editResendPropertyAddress = null;
        if (env.PropertyID.HasValue)
        {
            try { editResendPropertyName = await _merge.GetPropertyNameAsync(env.PropertyID.Value, env.LocationCode); editResendPropertyAddress = await _merge.GetPropertyAddressAsync(env.PropertyID.Value, env.LocationCode); }
            catch { /* non-fatal */ }
        }
        editResendPropertyName ??= env.PropertyName;

        var editContextRows = "";
        if (!string.IsNullOrWhiteSpace(editResendPropertyName))
            editContextRows += $"<tr><td style=\"padding:4px 16px 4px 0;color:#6b7280;font-size:13px;white-space:nowrap;\">Property</td><td style=\"padding:4px 0;font-size:13px;\">{System.Net.WebUtility.HtmlEncode(editResendPropertyName)}</td></tr>";
        if (!string.IsNullOrWhiteSpace(editResendPropertyAddress))
            editContextRows += $"<tr><td style=\"padding:4px 16px 4px 0;color:#6b7280;font-size:13px;white-space:nowrap;\">Address</td><td style=\"padding:4px 0;font-size:13px;\">{System.Net.WebUtility.HtmlEncode(editResendPropertyAddress)}</td></tr>";
        if (!string.IsNullOrWhiteSpace(env.OrderNumber))
            editContextRows += $"<tr><td style=\"padding:4px 16px 4px 0;color:#6b7280;font-size:13px;white-space:nowrap;\">Order #</td><td style=\"padding:4px 0;font-size:13px;\">{System.Net.WebUtility.HtmlEncode(env.OrderNumber)}</td></tr>";
        if (!string.IsNullOrWhiteSpace(env.LocationCode))
            editContextRows += $"<tr><td style=\"padding:4px 16px 4px 0;color:#6b7280;font-size:13px;white-space:nowrap;\">Branch</td><td style=\"padding:4px 0;font-size:13px;\">{System.Net.WebUtility.HtmlEncode(env.LocationCode)}</td></tr>";
        var editContextHtml = string.IsNullOrEmpty(editContextRows) ? "" : $@"<table cellpadding=""0"" cellspacing=""0"" border=""0"" style=""margin:12px 0 16px 0;border-left:3px solid #f59e0b;padding-left:12px;"">{editContextRows}</table>";

        var editReplyToEmail = await GetReplyToEmailAsync(env.LocationCode);

        foreach (var recipient in recipientsToResend)
        {
            try
            {
                var baseUrl = _appSettings.BaseUrl.TrimEnd('/');
                var link    = $"{baseUrl}/sign/{recipient.AccessToken}";

                var messageHtml = !string.IsNullOrWhiteSpace(env.MessageBody)
                    ? $"<p style=\"margin:12px 0;font-size:14px;color:#374151;\">{env.MessageBody}</p>"
                    : "";

                var innerHtml = $@"
                    <h2 style=""margin:0 0 16px 0;font-size:20px;color:#1e293b;font-weight:600;"">Updated Envelope Signing Request</h2>
                    <p style=""margin:0 0 12px 0;font-size:15px;color:#374151;"">Hello {recipient.FullName},</p>
                    <p style=""margin:0 0 8px 0;font-size:15px;color:#374151;"">
                        Your envelope signing invitation for <strong>{env.Subject}</strong> has been updated.
                        Please use the new link below to review and sign.
                    </p>
                    {editContextHtml}
                    {messageHtml}
                    <table role=""presentation"" cellpadding=""0"" cellspacing=""0"" border=""0"" style=""margin:24px 0;"">
                      <tr>
                        <td align=""center"" style=""background-color:#3b82f6;border-radius:6px;"">
                          <a href=""{link}"" style=""display:inline-block;padding:12px 32px;font-size:16px;font-weight:600;color:#ffffff;text-decoration:none;border-radius:6px;"">Open &amp; Sign</a>
                        </td>
                      </tr>
                    </table>
                    <p style=""margin:0;font-size:12px;color:#9ca3af;"">If the button above doesn't work, copy and paste this link into your browser:</p>
                    <p style=""margin:4px 0 0 0;font-size:12px;color:#3b82f6;word-break:break-all;""><a href=""{link}"" style=""color:#3b82f6;"">{link}</a></p>";

                var html = await _emailTemplate.WrapInBrandedTemplateAsync(innerHtml);
                await _notify.SendEnvelopeEmailAsync(recipient.Email, recipient.FullName, env.Subject, html, editReplyToEmail);

                _db.SignEvents.Add(new SignEvent
                {
                    EnvelopeId    = env.EnvelopeId,
                    RecipientId   = recipient.RecipientId,
                    EventType     = "Sent",
                    OccurredAtUtc = DateTime.UtcNow
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to re-send invite to recipient {RecipientId} after edit", recipient.RecipientId);
            }
        }

        await _db.SaveChangesAsync();
    }

    public async Task VoidEnvelopeAsync(long envelopeId, int voidedByUserId, string? reason = null)
    {
        var env = await _db.SignEnvelopes
            .Include(e => e.Recipients)
            .FirstOrDefaultAsync(e => e.EnvelopeId == envelopeId);

        if (env == null)
            throw new InvalidOperationException($"Envelope {envelopeId} not found");

        // Only allow voiding if envelope is not already completed or voided
        if (env.Status == "Completed")
            throw new InvalidOperationException("Cannot void a completed envelope");

        if (env.Status == "Voided")
            throw new InvalidOperationException("Envelope is already voided");

        // Update envelope status
        env.Status = "Voided";
        env.ModifiedByUsers_ID = voidedByUserId;
        env.ModifiedDateUtc = DateTime.UtcNow;

        // Add void event
        _db.SignEvents.Add(new SignEvent
        {
            EnvelopeId = env.EnvelopeId,
            EventType = "Voided",
            OccurredAtUtc = DateTime.UtcNow,
            MetaJson = !string.IsNullOrWhiteSpace(reason)
                ? System.Text.Json.JsonSerializer.Serialize(new { reason, voidedByUserId })
                : System.Text.Json.JsonSerializer.Serialize(new { voidedByUserId })
        });

        await _db.SaveChangesAsync();

        // Optionally send notification emails to recipients (if envelope was already sent)
        if (env.SentAtUtc.HasValue)
        {
            foreach (var recipient in env.Recipients.Where(r => !r.SignedAtUtc.HasValue))
            {
                try
                {
                    var reasonHtml = !string.IsNullOrWhiteSpace(reason)
                        ? $"<p style=\"margin:12px 0;font-size:14px;color:#374151;\"><strong>Reason:</strong> {reason}</p>"
                        : "";
                    var innerHtml = $@"
                        <h2 style=""margin:0 0 16px 0;font-size:20px;color:#1e293b;font-weight:600;"">Envelope Signing Request Cancelled</h2>
                        <p style=""margin:0 0 12px 0;font-size:15px;color:#374151;"">Hello {recipient.FullName},</p>
                        <p style=""margin:0 0 12px 0;font-size:15px;color:#374151;"">The signature request for <strong>{env.Subject}</strong> has been cancelled.</p>
                        {reasonHtml}
                        <p style=""margin:12px 0 0 0;font-size:15px;color:#374151;"">No further action is required.</p>";
                    var html = await _emailTemplate.WrapInBrandedTemplateAsync(innerHtml);
                    await _notify.SendEnvelopeEmailAsync(recipient.Email, recipient.FullName, $"Cancelled: {env.Subject}", html);
                }
                catch
                {
                    // Don't fail the void operation if email fails
                }
            }
        }
    }

    public async Task<EnvelopeDetailsVm?> GetDetailsAsync(long envelopeId)
    {
        var e = await _db.SignEnvelopes
            .Include(x => x.Recipients)
            .Include(x => x.Events)
            .Include(x => x.Fields)
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.EnvelopeId == envelopeId);
        if (e is null) return null;

        // Fetch property name from ERP; fall back to stored free-text name
        string? propertyName = null;
        if (e.PropertyID.HasValue)
        {
            propertyName = await _merge.GetPropertyNameAsync(e.PropertyID.Value);
        }
        propertyName ??= e.PropertyName;

        // Get UnitNumber from fields
        var unitNumber = e.Fields?.FirstOrDefault(f => f.FieldKey == "UnitNumber")?.FieldValue;

        return new EnvelopeDetailsVm
        {
            EnvelopeId = e.EnvelopeId,
            TemplateKey = e.TemplateKey,
            Subject = e.Subject,
            MessageBody = e.MessageBody,
            Status = e.Status,
            LocationCode = e.LocationCode,
            SentAtUtc = e.SentAtUtc,
            CompletedAtUtc = e.CompletedAtUtc,
            ExpiresAtUtc = e.ExpiresAtUtc,
            PdfPath = e.PdfStoragePath,
            PdfSha256Hex = e.PdfSha256 is null ? null : Convert.ToHexString(e.PdfSha256),
            PropertyName = propertyName,
            OrderNumber = e.OrderNumber,
            UnitNumber = unitNumber,
            Recipients = e.Recipients.OrderBy(r => r.SignerOrder).Select(r => new EnvelopeDetailsVm.RecipientVm
            {
                RecipientId   = r.RecipientId,
                Role          = r.Role,
                SignerOrder   = r.SignerOrder,
                FullName      = r.FullName,
                Email         = r.Email,
                Phone         = r.Phone,
                ViewedAtUtc   = r.ViewedAtUtc,
                SignedAtUtc   = r.SignedAtUtc,
                DeclinedAtUtc = r.DeclinedAtUtc
            }).ToList(),
            Events = e.Events.OrderByDescending(ev => ev.OccurredAtUtc).Select(ev => new EnvelopeDetailsVm.EventVm
            {
                EventType = ev.EventType,
                OccurredAtUtc = ev.OccurredAtUtc,
                Recipient = e.Recipients.FirstOrDefault(r => r.RecipientId == ev.RecipientId)?.FullName,
                Meta = ev.MetaJson
            }).ToList()
        };
    }

    public async Task<(IReadOnlyList<EnvelopeListItemVm> Rows, int Total)> SearchAsync(string? status, string? office, int page, int pageSize, int? createdByUserId = null, string? scope = null, int? userRoleId = null, int? userLocationId = null)
    {
        var q = _db.SignEnvelopes.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(status)) q = q.Where(e => e.Status == status);
        // The explicit office filter is only meaningful in the "all" scope where the user can browse
        // across locations.  For "branch" and "mine" the location is already enforced below.
        if (!string.IsNullOrWhiteSpace(office) && scope == "all")
            q = q.Where(e => e.LocationCode == office);

        // Scope-based filtering:
        //   "mine"   → only envelopes created by this user
        //   "branch" → all envelopes for the user's current location (default for every role)
        //   "all"    → no additional filter (admin / GM only, granted by the controller)
        if (scope == "mine" && createdByUserId.HasValue)
        {
            q = q.Where(e => e.CreatedByUsers_ID == createdByUserId.Value);
        }
        else if (scope == "branch" && userLocationId.HasValue)
        {
            var locationCode = GetLocationCodeById(userLocationId.Value);
            if (!string.IsNullOrWhiteSpace(locationCode))
                q = q.Where(e => e.LocationCode == locationCode);
        }
        // scope == "all" → no additional filter

        var total = await q.CountAsync();
        var envelopes = await q
            .OrderByDescending(e => e.SentAtUtc ?? e.CreatedDateUtc)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(e => new
            {
                e.EnvelopeId,
                e.Subject,
                e.TemplateKey,
                e.Status,
                e.LocationCode,
                e.SentAtUtc,
                e.CompletedAtUtc,
                e.ExpiresAtUtc,
                e.PropertyID,
                e.PropertyName,
                e.OrderNumber,
                RecipientCount = e.Recipients.Count,
                SignedCount = e.Recipients.Count(r => r.SignedAtUtc != null),
            })
            .ToListAsync();

        // Populate PropertyName from ERP (cached to avoid N+1 calls); fall back to stored free-text
        var propertyNameCache = new Dictionary<int, string?>();
        var rows = new List<EnvelopeListItemVm>();
        foreach (var e in envelopes)
        {
            string? propertyName = null;
            if (e.PropertyID.HasValue)
            {
                if (!propertyNameCache.TryGetValue(e.PropertyID.Value, out propertyName))
                {
                    propertyName = await _merge.GetPropertyNameAsync(e.PropertyID.Value);
                    propertyNameCache[e.PropertyID.Value] = propertyName;
                }
            }
            propertyName ??= e.PropertyName;

            rows.Add(new EnvelopeListItemVm
            {
                EnvelopeId = e.EnvelopeId,
                Subject = e.Subject,
                TemplateKey = e.TemplateKey,
                Status = e.Status,
                LocationCode = e.LocationCode,
                SentAtUtc = e.SentAtUtc,
                CompletedAtUtc = e.CompletedAtUtc,
                ExpiresAtUtc = e.ExpiresAtUtc,
                RecipientCount = e.RecipientCount,
                SignedCount = e.SignedCount,
                PropertyName = propertyName,
                OrderNumber = e.OrderNumber
            });
        }

        return (rows, total);
    }

    // PUBLIC FLOW

    public async Task<ReviewVm?> GetReviewAsync(string token, string userAgent, string ip)
    {
        var r = await _db.SignRecipients
            .Include(x => x.Envelope)
            .FirstOrDefaultAsync(x => x.AccessToken == token);

        if (r is null || (r.AccessTokenExpiresAt is not null && r.AccessTokenExpiresAt < DateTime.UtcNow))
            return null;

        // Record "Opened" only the first time (every page load calls this, so guard with null check)
        bool firstOpen = r.ViewedAtUtc is null;
        r.ViewedAtUtc ??= DateTime.UtcNow;
        r.IPAddressViewed ??= ip;
        r.UserAgentViewed ??= userAgent;

        if (firstOpen)
        {
            _db.SignEvents.Add(new SignEvent
            {
                EnvelopeId = r.EnvelopeId,
                RecipientId = r.RecipientId,
                EventType = "Opened",
                OccurredAtUtc = DateTime.UtcNow
            });
        }
        await _db.SaveChangesAsync();

        // find if a tenant recipient exists
        var hasTenant = await _db.SignRecipients
            .AnyAsync(x => x.EnvelopeId == r.EnvelopeId && x.Role == "Tenant");

        // Populate property summary for the review page
        PropertyVm? prop = null;
        if (r.Envelope.PropertyID.HasValue)
        {
            try
            {
                var pName    = await _merge.GetPropertyNameAsync(r.Envelope.PropertyID.Value);
                var pAddress = await _merge.GetPropertyAddressAsync(r.Envelope.PropertyID.Value);
                if (!string.IsNullOrEmpty(pName))
                    prop = new PropertyVm { PropertyId = r.Envelope.PropertyID.Value, Name = pName, Address = pAddress ?? "" };
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Could not fetch property for review page (envelope {EnvelopeId})", r.EnvelopeId);
            }
        }
        // Fall back to stored free-text property name when no ERP match
        prop ??= string.IsNullOrWhiteSpace(r.Envelope.PropertyName)
            ? null
            : new PropertyVm { Name = r.Envelope.PropertyName };

        return new ReviewVm
        {
            Token = token,
            Envelope = r.Envelope,
            Recipient = r,
            HasTenantRecipient = hasTenant,
            Property = prop
        };
    }

    public async Task<bool> DeclineEnvelopeAsync(string token, string reason, string userAgent, string ip)
    {
        var recipient = await _db.SignRecipients
            .Include(r => r.Envelope)
            .ThenInclude(e => e.Recipients)
            .FirstOrDefaultAsync(r => r.AccessToken == token);

        if (recipient == null)
            return false;

        // Check if already signed or declined
        if (recipient.SignedAtUtc != null)
            return false; // Already signed, can't decline

        if (recipient.DeclinedAtUtc != null)
            return false; // Already declined

        // Mark recipient as declined
        recipient.DeclinedAtUtc = DateTime.UtcNow;
        recipient.IPAddressSigned = ip; // Track IP of decline action
        recipient.UserAgentSigned = userAgent;

        // Update envelope status to Declined
        var envelope = recipient.Envelope;
        envelope.Status = "Declined";
        envelope.ModifiedDateUtc = DateTime.UtcNow;

        // Add decline event
        _db.SignEvents.Add(new SignEvent
        {
            EnvelopeId = envelope.EnvelopeId,
            RecipientId = recipient.RecipientId,
            EventType = "Declined",
            OccurredAtUtc = DateTime.UtcNow,
            MetaJson = System.Text.Json.JsonSerializer.Serialize(new { reason, ip, userAgent })
        });

        await _db.SaveChangesAsync();

        // Notify other recipients and envelope creator
        try
        {
            // Notify all other recipients that envelope was declined
            foreach (var otherRecipient in envelope.Recipients.Where(r => r.RecipientId != recipient.RecipientId))
            {
                var reasonHtml = !string.IsNullOrWhiteSpace(reason)
                    ? $"<p style=\"margin:12px 0;font-size:14px;color:#374151;\"><strong>Reason:</strong> {reason}</p>"
                    : "";
                var innerHtml = $@"
                    <h2 style=""margin:0 0 16px 0;font-size:20px;color:#1e293b;font-weight:600;"">Envelope Signing Request Declined</h2>
                    <p style=""margin:0 0 12px 0;font-size:15px;color:#374151;"">Hello {otherRecipient.FullName},</p>
                    <p style=""margin:0 0 12px 0;font-size:15px;color:#374151;"">The signature request for <strong>{envelope.Subject}</strong> has been declined by {recipient.FullName}.</p>
                    {reasonHtml}
                    <p style=""margin:12px 0 0 0;font-size:15px;color:#374151;"">No further action is required.</p>";
                var html = await _emailTemplate.WrapInBrandedTemplateAsync(innerHtml);
                await _notify.SendEnvelopeEmailAsync(otherRecipient.Email, otherRecipient.FullName, $"Declined: {envelope.Subject}", html);
            }
        }
        catch
        {
            // Don't fail the decline operation if email fails
        }

        return true;
    }

    public async Task UpsertTenantRecipientAsync(long envelopeId, string fullName, string email, string? phone = null)
    {
        var env = await _db.SignEnvelopes.Include(e => e.Recipients).FirstOrDefaultAsync(e => e.EnvelopeId == envelopeId)
            ?? throw new InvalidOperationException($"Envelope {envelopeId} not found");
        var existing = env.Recipients.FirstOrDefault(r => r.Role == "Tenant");

        if (existing == null)
        {
            var order = env.Recipients.Any() ? env.Recipients.Max(r => r.SignerOrder) + 1 : 1;

            _db.SignRecipients.Add(new SignRecipient
            {
                EnvelopeId = envelopeId,
                Role = "Tenant",
                FullName = fullName,
                Email = email,
                Phone = phone,                                       // NEW: Set tenant phone
                SignerOrder = order,
                AccessToken = TokenHelper.CreateSecureToken(32),    // REQUIRED: Generate access token
                AccessTokenExpiresAt = env.ExpiresAtUtc,            // Set expiration
                RequiresOtp = false,                                 // Not requiring OTP for tenant
                CreatedDateUtc = DateTime.UtcNow                     // Set creation time
            });

            await _db.SaveChangesAsync();
        }
        else
        {
            existing.FullName = fullName;
            existing.Email = email;
            existing.Phone = phone;  // NEW: Update phone if provided
            await _db.SaveChangesAsync();
        }
    }

    public async Task MarkTenantSkippedAsync(long envelopeId, string skippedByName)
    {
        var env = await _db.SignEnvelopes.Include(e => e.Recipients).FirstOrDefaultAsync(e => e.EnvelopeId == envelopeId)
            ?? throw new InvalidOperationException($"Envelope {envelopeId} not found");

        env.TenantSkipped = true;
        env.TenantSkippedByName = skippedByName;
        env.TenantSkippedAtUtc = DateTime.UtcNow;

        // Remove any existing unsigned Tenant recipients so they don't block envelope finalization
        var unsignedTenants = env.Recipients.Where(r => r.Role == "Tenant" && r.SignedAtUtc == null).ToList();
        foreach (var t in unsignedTenants)
            _db.SignRecipients.Remove(t);

        await _db.SaveChangesAsync();
    }

    public async Task CaptureSignatureAsync(long envelopeId, long recipientId, string typedFullName, string sigDataBase64)
    {
        var rec = await _db.SignRecipients.FirstOrDefaultAsync(r => r.RecipientId == recipientId && r.EnvelopeId == envelopeId)
            ?? throw new InvalidOperationException($"Recipient {recipientId} not found for envelope {envelopeId}");

        if (rec.SignedAtUtc != null)
            throw new InvalidOperationException("Recipient has already signed");

        if (rec.AccessTokenExpiresAt != null && rec.AccessTokenExpiresAt < DateTime.UtcNow)
            throw new InvalidOperationException("Signing token has expired");

        // save typed name
        rec.TypedFullName = typedFullName;

        // save drawn signature image
        var dir = Path.Combine("wwwroot", "Files", "Sign", DateTime.UtcNow.ToString("yyyy"), DateTime.UtcNow.ToString("MM"));
        Directory.CreateDirectory(dir);
        var bytes = Convert.FromBase64String(sigDataBase64.Split(',').Last()); // handle data:image/png;base64,...
        var relPath = $"/Files/Sign/{DateTime.UtcNow:yyyy}/{DateTime.UtcNow:MM}/sig-{recipientId}.png";
        var fsPath = Path.Combine("wwwroot", relPath.TrimStart('/').Replace("/", Path.DirectorySeparatorChar.ToString()));
        await File.WriteAllBytesAsync(fsPath, bytes);

        rec.SignatureImagePath = relPath;
        rec.SignedAtUtc = DateTime.UtcNow;
        await _db.SaveChangesAsync();
    }

    public async Task<(bool isComplete, string? downloadUrl)> TryFinalizeEnvelopeAsync(long envelopeId)
    {
        // Use a fresh query with AsNoTracking to avoid cache issues, then reload for update
        var signedCount = await _db.SignRecipients
            .Where(r => r.EnvelopeId == envelopeId && r.SignedAtUtc != null)
            .CountAsync();

        var totalCount = await _db.SignRecipients
            .Where(r => r.EnvelopeId == envelopeId)
            .CountAsync();

        if (signedCount != totalCount)
        {
            return (false, null);
        }

        // All signed! Now load the envelope for updating (use a fresh query)
        var env = await _db.SignEnvelopes
            .Include(e => e.Recipients)
            .FirstOrDefaultAsync(e => e.EnvelopeId == envelopeId)
            ?? throw new InvalidOperationException($"Envelope {envelopeId} not found");

        // Generate PDF and finalize
        var pdf = await _pdf.RenderAndSealAsync(env.EnvelopeId);
        env.PdfStoragePath = pdf.storagePath.Replace("\\", "/");
        env.PdfSha256 = pdf.sha256;
        env.Status = "Completed";
        env.CompletedAtUtc = DateTime.UtcNow;

        _db.SignEvents.Add(new SignEvent
        {
            EnvelopeId = env.EnvelopeId,
            EventType = "Completed",
            OccurredAtUtc = DateTime.UtcNow,
            MetaJson = "{\"auto\":\"finalized\",\"allSigned\":true}"
        });

        await _db.SaveChangesAsync();

        // ── In-app notification to the envelope creator ──────────────────────
        // Fires via SignalR so the bell icon lights up immediately (red dot)
        // and appears in the creator's Notification Center.
        try
        {
            await _appNotify.SendNotificationToUserAsync(
                userId:            env.CreatedByUsers_ID,
                type:              "DocumentSigning",
                title:             "Envelope Completed",
                message:           $"\"{env.Subject}\" has been signed by all parties and is ready to download.",
                actionUrl:         $"/SignAdmin/Details/{env.EnvelopeId}",
                relatedTaskId:     null,
                relatedEnvelopeId: (int?)env.EnvelopeId
            );
        }
        catch (Exception ex)
        {
            // Non-fatal — log and continue so the PDF/email flow is not interrupted
            _logger.LogWarning(ex, "Failed to send in-app completion notification for envelope {EnvelopeId}", envelopeId);
        }

        // Send completion emails to all recipients
        var downloadUrl = env.PdfStoragePath;
        foreach (var recipient in env.Recipients)
        {
            // Build download button if a PDF path is available
            var downloadBtnHtml = "";
            if (!string.IsNullOrWhiteSpace(downloadUrl))
            {
                var absoluteDownload = downloadUrl.StartsWith("/")
                    ? $"{_appSettings.BaseUrl.TrimEnd('/')}{downloadUrl}"
                    : downloadUrl;
                downloadBtnHtml = $@"
                <table role=""presentation"" cellpadding=""0"" cellspacing=""0"" border=""0"" style=""margin:24px 0;"">
                  <tr>
                    <td align=""center"" style=""background-color:#16a34a;border-radius:6px;"">
                      <a href=""{absoluteDownload}"" style=""display:inline-block;padding:12px 32px;font-size:16px;font-weight:600;color:#ffffff;text-decoration:none;border-radius:6px;"">Download Completed PDF</a>
                    </td>
                  </tr>
                </table>";
            }

            var completionInner = $@"
                <h2 style=""margin:0 0 16px 0;font-size:20px;color:#1e293b;font-weight:600;"">Document Completed</h2>
                <p style=""margin:0 0 12px 0;font-size:15px;color:#374151;"">Hello {recipient.FullName},</p>
                <p style=""margin:0 0 12px 0;font-size:15px;color:#374151;"">Thank you for signing. The document <strong>{env.Subject}</strong> has been completed by all parties.</p>
                {downloadBtnHtml}";

            var completionHtml = await _emailTemplate.WrapInBrandedTemplateAsync(completionInner);
            // Pass null for downloadUrl since it's already included in the branded template
            await _notify.SendCompletedReceiptAsync(
                recipient.Email,
                recipient.FullName,
                $"Completed: {env.Subject}",
                completionHtml,
                null);
        }

        // ---------------------------------------------------------------
        // Fetch property name + address for notification emails
        // ---------------------------------------------------------------
        string? notifPropertyName    = null;
        string? notifPropertyAddress = null;
        if (env.PropertyID.HasValue)
        {
            try
            {
                // Pass env.LocationCode so the correct branch ERP database is queried.
                // Without this, GetErpContext() would read from the HTTP session which is
                // absent (or wrong) during anonymous signing — causing cross-branch data leakage.
                notifPropertyName    = await _merge.GetPropertyNameAsync(env.PropertyID.Value, env.LocationCode);
                notifPropertyAddress = await _merge.GetPropertyAddressAsync(env.PropertyID.Value, env.LocationCode);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Could not fetch property info for completion notification email (envelope {EnvelopeId})", envelopeId);
            }
        }
        notifPropertyName ??= env.PropertyName; // fall back to stored free-text name

        // ---------------------------------------------------------------
        // Send internal company / branch notification emails
        // ---------------------------------------------------------------
        var internalNotifSettings = await _db.EnvelopeNotificationSettings
            .Where(s => s.IsEnabled
                     && s.NotificationEmail != null
                     && s.NotificationEmail != "")
            .ToListAsync();

        if (internalNotifSettings.Count > 0)
        {
            var absoluteInternalDownload = !string.IsNullOrWhiteSpace(downloadUrl)
                ? (downloadUrl.StartsWith("/")
                    ? $"{_appSettings.BaseUrl.TrimEnd('/')}{downloadUrl}"
                    : downloadUrl)
                : null;

            var internalDownloadBtnHtml = absoluteInternalDownload != null
                ? $@"<table role=""presentation"" cellpadding=""0"" cellspacing=""0"" border=""0"" style=""margin:24px 0;"">
                  <tr>
                    <td align=""center"" style=""background-color:#16a34a;border-radius:6px;"">
                      <a href=""{absoluteInternalDownload}"" style=""display:inline-block;padding:12px 32px;font-size:16px;font-weight:600;color:#ffffff;text-decoration:none;border-radius:6px;"">Download Completed PDF</a>
                    </td>
                  </tr>
                </table>"
                : "";

            foreach (var notif in internalNotifSettings)
            {
                // Company-wide (LocationCode == null) → always send.
                // Branch-specific → only send when the envelope's LocationCode matches.
                bool shouldSend = notif.LocationCode == null
                    || string.Equals(notif.LocationCode, env.LocationCode, StringComparison.OrdinalIgnoreCase);

                if (!shouldSend)
                    continue;

                var branchLabel = notif.LocationCode == null
                    ? "All Branches"
                    : $"{notif.LocationName} ({notif.LocationCode})";

                var completedAt = env.CompletedAtUtc.HasValue
                    ? env.CompletedAtUtc.Value.ToString("f") + " UTC"
                    : DateTime.UtcNow.ToString("f") + " UTC";

                // Build optional property rows
                var propertyRows = "";
                if (!string.IsNullOrWhiteSpace(notifPropertyName))
                {
                    propertyRows += $@"
                        <tr style=""border-bottom:1px solid #e5e7eb;"">
                            <td style=""padding:8px 12px 8px 0;font-weight:600;white-space:nowrap;"">Property</td>
                            <td style=""padding:8px 0;"">{System.Net.WebUtility.HtmlEncode(notifPropertyName)}</td>
                        </tr>";
                }
                if (!string.IsNullOrWhiteSpace(notifPropertyAddress))
                {
                    propertyRows += $@"
                        <tr style=""border-bottom:1px solid #e5e7eb;"">
                            <td style=""padding:8px 12px 8px 0;font-weight:600;white-space:nowrap;"">Address</td>
                            <td style=""padding:8px 0;"">{System.Net.WebUtility.HtmlEncode(notifPropertyAddress)}</td>
                        </tr>";
                }

                var innerHtml = $@"
                    <h2 style=""margin:0 0 16px 0;font-size:20px;color:#1e293b;font-weight:600;"">Envelope Completed</h2>
                    <p style=""margin:0 0 16px 0;font-size:15px;color:#374151;"">
                        An envelope has been completed by all signers. Please find the details below.
                    </p>
                    <table style=""width:100%;border-collapse:collapse;font-size:14px;color:#374151;margin-bottom:16px;"">
                        <tr style=""border-bottom:1px solid #e5e7eb;"">
                            <td style=""padding:8px 12px 8px 0;font-weight:600;white-space:nowrap;"">Envelope #</td>
                            <td style=""padding:8px 0;"">{env.EnvelopeId}</td>
                        </tr>
                        {propertyRows}
                        <tr style=""border-bottom:1px solid #e5e7eb;"">
                            <td style=""padding:8px 12px 8px 0;font-weight:600;white-space:nowrap;"">Subject</td>
                            <td style=""padding:8px 0;"">{System.Net.WebUtility.HtmlEncode(env.Subject)}</td>
                        </tr>

                        <tr style=""border-bottom:1px solid #e5e7eb;"">
                            <td style=""padding:8px 12px 8px 0;font-weight:600;white-space:nowrap;"">Order</td>
                            <td style=""padding:8px 0;"">{System.Net.WebUtility.HtmlEncode(env.OrderNumber ?? "N/A")}</td>
                        </tr>
                        <tr style=""border-bottom:1px solid #e5e7eb;"">
                            <td style=""padding:8px 12px 8px 0;font-weight:600;white-space:nowrap;"">Branch</td>
                            <td style=""padding:8px 0;"">{System.Net.WebUtility.HtmlEncode(branchLabel)}</td>
                        </tr>
                        <tr>
                            <td style=""padding:8px 12px 8px 0;font-weight:600;white-space:nowrap;"">Completed</td>
                            <td style=""padding:8px 0;"">{completedAt}</td>
                        </tr>
                    </table>
                    {internalDownloadBtnHtml}";

                var notifHtml = await _emailTemplate.WrapInBrandedTemplateAsync(innerHtml);

                try
                {
                    await _notify.SendEnvelopeEmailAsync(
                        notif.NotificationEmail!,
                        notif.LocationName,
                        $"Envelope Completed: {env.Subject}",
                        notifHtml);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to send internal envelope completion notification to {Email} for envelope {EnvelopeId}",
                        notif.NotificationEmail, envelopeId);
                }
            }
        }

        return (true, downloadUrl);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Offline / Manual Completion
    // ─────────────────────────────────────────────────────────────────────────

    public async Task MarkOfflineCompleteAsync(
        long envelopeId,
        Microsoft.AspNetCore.Http.IFormFile signedPdf,
        string? staffNote,
        int staffUserId,
        string staffName)
    {
        var env = await _db.SignEnvelopes
            .Include(e => e.Recipients)
            .FirstOrDefaultAsync(e => e.EnvelopeId == envelopeId)
            ?? throw new InvalidOperationException($"Envelope {envelopeId} not found.");

        if (env.Status is "Completed" or "Voided" or "Declined")
            throw new InvalidOperationException($"Envelope is already {env.Status} and cannot be marked offline complete.");

        // ── Mark any unsigned recipients as offline-signed ───────────────────
        var offlineMetaJson = System.Text.Json.JsonSerializer.Serialize(new
        {
            method    = "offline",
            markedBy  = staffName,
            note      = staffNote ?? ""
        });

        foreach (var r in env.Recipients.Where(r => r.SignedAtUtc == null))
        {
            r.SignedAtUtc         = DateTime.UtcNow;
            r.TypedFullName       = r.FullName;           // physical signature attested by name
            r.SignatureMetaJson   = offlineMetaJson;

            _db.SignEvents.Add(new SignEvent
            {
                EnvelopeId  = envelopeId,
                RecipientId = r.RecipientId,
                EventType   = "Signed",
                OccurredAtUtc = DateTime.UtcNow,
                MetaJson    = offlineMetaJson
            });
        }

        // ── Store the uploaded PDF ───────────────────────────────────────────
        var dir = Path.Combine("wwwroot", "Files", "Sign",
            DateTime.UtcNow.ToString("yyyy"), DateTime.UtcNow.ToString("MM"));
        Directory.CreateDirectory(dir);
        var fsPath = Path.Combine(dir, $"envelope-{envelopeId}.pdf");

        byte[] pdfBytes;
        await using (var ms = new System.IO.MemoryStream())
        {
            await signedPdf.CopyToAsync(ms);
            pdfBytes = ms.ToArray();
        }
        await File.WriteAllBytesAsync(fsPath, pdfBytes);

        var webPath    = $"/Files/Sign/{DateTime.UtcNow:yyyy}/{DateTime.UtcNow:MM}/envelope-{envelopeId}.pdf";
        var sha256Hash = System.Security.Cryptography.SHA256.HashData(pdfBytes);

        // ── Finalise the envelope ────────────────────────────────────────────
        env.PdfStoragePath  = webPath;
        env.PdfSha256       = sha256Hash;
        env.Status          = "Completed";
        env.CompletedAtUtc  = DateTime.UtcNow;

        var completionMetaJson = System.Text.Json.JsonSerializer.Serialize(new
        {
            method     = "offline",
            uploadedBy = staffName,
            note       = staffNote ?? ""
        });

        _db.SignEvents.Add(new SignEvent
        {
            EnvelopeId    = envelopeId,
            EventType     = "Completed",
            OccurredAtUtc = DateTime.UtcNow,
            MetaJson      = completionMetaJson
        });

        await _db.SaveChangesAsync();

        // ── In-app notification to creator ───────────────────────────────────
        try
        {
            await _appNotify.SendNotificationToUserAsync(
                userId:            env.CreatedByUsers_ID,
                type:              "DocumentSigning",
                title:             "Envelope Completed (Offline)",
                message:           $"\"{env.Subject}\" was marked as offline-signed by {staffName} and is ready to download.",
                actionUrl:         $"/SignAdmin/Details/{env.EnvelopeId}",
                relatedTaskId:     null,
                relatedEnvelopeId: (int?)env.EnvelopeId
            );
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to send in-app offline completion notification for envelope {EnvelopeId}", envelopeId);
        }

        // ── Completion emails to all recipients ──────────────────────────────
        var absoluteDownload = webPath.StartsWith("/")
            ? $"{_appSettings.BaseUrl.TrimEnd('/')}{webPath}"
            : webPath;

        var downloadBtnHtml = $@"
            <table role=""presentation"" cellpadding=""0"" cellspacing=""0"" border=""0"" style=""margin:24px 0;"">
              <tr>
                <td align=""center"" style=""background-color:#16a34a;border-radius:6px;"">
                  <a href=""{absoluteDownload}"" style=""display:inline-block;padding:12px 32px;font-size:16px;font-weight:600;color:#ffffff;text-decoration:none;border-radius:6px;"">Download Completed PDF</a>
                </td>
              </tr>
            </table>";

        foreach (var recipient in env.Recipients)
        {
            var inner = $@"
                <h2 style=""margin:0 0 16px 0;font-size:20px;color:#1e293b;font-weight:600;"">Document Completed</h2>
                <p style=""margin:0 0 12px 0;font-size:15px;color:#374151;"">Hello {recipient.FullName},</p>
                <p style=""margin:0 0 12px 0;font-size:15px;color:#374151;"">The document <strong>{env.Subject}</strong> has been completed and is available to download.</p>
                {downloadBtnHtml}";
            var html = await _emailTemplate.WrapInBrandedTemplateAsync(inner);
            await _notify.SendCompletedReceiptAsync(recipient.Email, recipient.FullName,
                $"Completed: {env.Subject}", html, null);
        }

        // ── Internal branch / company notification emails ────────────────────
        var internalSettings = await _db.EnvelopeNotificationSettings
            .Where(s => s.IsEnabled && s.NotificationEmail != null && s.NotificationEmail != "")
            .ToListAsync();

        if (internalSettings.Count > 0)
        {
            string? notifPropertyName    = null;
            string? notifPropertyAddress = null;
            if (env.PropertyID.HasValue)
            {
                try
                {
                    notifPropertyName    = await _merge.GetPropertyNameAsync(env.PropertyID.Value, env.LocationCode);
                    notifPropertyAddress = await _merge.GetPropertyAddressAsync(env.PropertyID.Value, env.LocationCode);
                }
                catch { /* non-fatal */ }
            }
            notifPropertyName ??= env.PropertyName;

            var propertyRows = "";
            if (!string.IsNullOrWhiteSpace(notifPropertyName))
                propertyRows += $@"<tr style=""border-bottom:1px solid #e5e7eb;""><td style=""padding:8px 12px 8px 0;font-weight:600;white-space:nowrap;"">Property</td><td style=""padding:8px 0;"">{System.Net.WebUtility.HtmlEncode(notifPropertyName)}</td></tr>";
            if (!string.IsNullOrWhiteSpace(notifPropertyAddress))
                propertyRows += $@"<tr style=""border-bottom:1px solid #e5e7eb;""><td style=""padding:8px 12px 8px 0;font-weight:600;white-space:nowrap;"">Address</td><td style=""padding:8px 0;"">{System.Net.WebUtility.HtmlEncode(notifPropertyAddress)}</td></tr>";

            foreach (var notif in internalSettings)
            {
                bool shouldSend = notif.LocationCode == null
                    || string.Equals(notif.LocationCode, env.LocationCode, StringComparison.OrdinalIgnoreCase);
                if (!shouldSend) continue;

                var branchLabel = notif.LocationCode == null
                    ? "All Branches" : $"{notif.LocationName} ({notif.LocationCode})";
                var completedAt = env.CompletedAtUtc?.ToString("f") + " UTC";

                var innerHtml = $@"
                    <h2 style=""margin:0 0 16px 0;font-size:20px;color:#1e293b;font-weight:600;"">Envelope Completed (Offline)</h2>
                    <p style=""margin:0 0 16px 0;font-size:15px;color:#374151;"">
                        An envelope was marked as completed via offline/manual signing by <strong>{System.Net.WebUtility.HtmlEncode(staffName)}</strong>.
                    </p>
                    <table style=""width:100%;border-collapse:collapse;font-size:14px;color:#374151;margin-bottom:16px;"">
                        <tr style=""border-bottom:1px solid #e5e7eb;"">
                            <td style=""padding:8px 12px 8px 0;font-weight:600;white-space:nowrap;"">Envelope #</td>
                            <td style=""padding:8px 0;"">{env.EnvelopeId}</td>
                        </tr>
                        {propertyRows}
                        <tr style=""border-bottom:1px solid #e5e7eb;"">
                            <td style=""padding:8px 12px 8px 0;font-weight:600;white-space:nowrap;"">Subject</td>
                            <td style=""padding:8px 0;"">{System.Net.WebUtility.HtmlEncode(env.Subject)}</td>
                        </tr>
                        <tr style=""border-bottom:1px solid #e5e7eb;"">
                            <td style=""padding:8px 12px 8px 0;font-weight:600;white-space:nowrap;"">Order</td>
                            <td style=""padding:8px 0;"">{System.Net.WebUtility.HtmlEncode(env.OrderNumber ?? "N/A")}</td>
                        </tr>
                        <tr style=""border-bottom:1px solid #e5e7eb;"">
                            <td style=""padding:8px 12px 8px 0;font-weight:600;white-space:nowrap;"">Branch</td>
                            <td style=""padding:8px 0;"">{System.Net.WebUtility.HtmlEncode(branchLabel)}</td>
                        </tr>
                        <tr style=""border-bottom:1px solid #e5e7eb;"">
                            <td style=""padding:8px 12px 8px 0;font-weight:600;white-space:nowrap;"">Completed By</td>
                            <td style=""padding:8px 0;"">{System.Net.WebUtility.HtmlEncode(staffName)} (offline)</td>
                        </tr>
                        <tr>
                            <td style=""padding:8px 12px 8px 0;font-weight:600;white-space:nowrap;"">Completed</td>
                            <td style=""padding:8px 0;"">{completedAt}</td>
                        </tr>
                    </table>
                    {downloadBtnHtml}";

                try
                {
                    await _notify.SendEnvelopeEmailAsync(notif.NotificationEmail!, notif.LocationName,
                        $"Envelope Completed (Offline): {env.Subject}",
                        await _emailTemplate.WrapInBrandedTemplateAsync(innerHtml));
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to send offline completion notification to {Email} for envelope {EnvelopeId}",
                        notif.NotificationEmail, envelopeId);
                }
            }
        }
    }

    public async Task ProgressToNextAsync(long envelopeId)
    {
        var env = await _db.SignEnvelopes.Include(e => e.Recipients)
                                         .FirstOrDefaultAsync(e => e.EnvelopeId == envelopeId)
            ?? throw new InvalidOperationException($"Envelope {envelopeId} not found");
        // find next unsigned recipient in order
        var next = env.Recipients
                      .OrderBy(r => r.SignerOrder)
                      .FirstOrDefault(r => r.SignedAtUtc == null);

        if (next == null)
        {
            // no one left; let your SignAsync path finalize when last signer submits,
            // or finalize here if you prefer.
            return;
        }

        // send their invite
        var baseUrl = _appSettings.BaseUrl.TrimEnd('/');
        var link = $"{baseUrl}/sign/{next.AccessToken}";
        var innerHtml = $@"
            <h2 style=""margin:0 0 16px 0;font-size:20px;color:#1e293b;font-weight:600;"">Envelope Document Signing Request</h2>
            <p style=""margin:0 0 12px 0;font-size:15px;color:#374151;"">Hello {next.FullName},</p>
            <p style=""margin:0 0 12px 0;font-size:15px;color:#374151;"">Please review and sign the document: <strong>{env.Subject}</strong>.</p>
            <table role=""presentation"" cellpadding=""0"" cellspacing=""0"" border=""0"" style=""margin:24px 0;"">
              <tr>
                <td align=""center"" style=""background-color:#3b82f6;border-radius:6px;"">
                  <a href=""{link}"" style=""display:inline-block;padding:12px 32px;font-size:16px;font-weight:600;color:#ffffff;text-decoration:none;border-radius:6px;"">Open &amp; Sign</a>
                </td>
              </tr>
            </table>
            <p style=""margin:0;font-size:12px;color:#9ca3af;"">If the button above doesn't work, copy and paste this link into your browser:</p>
            <p style=""margin:4px 0 0 0;font-size:12px;color:#3b82f6;word-break:break-all;""><a href=""{link}"" style=""color:#3b82f6;"">{link}</a></p>";
        var html = await _emailTemplate.WrapInBrandedTemplateAsync(innerHtml);
        var progressReplyTo = await GetReplyToEmailAsync(env.LocationCode);
        await _notify.SendEnvelopeEmailAsync(next.Email, next.FullName, env.Subject, html, progressReplyTo);

        _db.SignEvents.Add(new SignEvent
        {
            EnvelopeId = env.EnvelopeId,
            RecipientId = next.RecipientId,
            EventType = "Sent",
            OccurredAtUtc = DateTime.UtcNow
        });

        await _db.SaveChangesAsync();
    }

    public async Task<SignTemplate?> GetTemplateByKeyAsync(string templateKey)
    {
        return await _db.SignTemplates
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.TemplateKey == templateKey);
    }

    /// <summary>
    /// Returns the Reply-To email address for outbound signer emails.
    /// Prefers the branch-specific setting (matching locationCode); falls back to the
    /// company-wide row (LocationCode == null) when no branch email is configured.
    /// Returns null when neither is set, which means no Reply-To header is added.
    /// </summary>
    private async Task<string?> GetReplyToEmailAsync(string? locationCode)
    {
        var setting = await _db.EnvelopeNotificationSettings
            .Where(s => s.IsEnabled
                     && s.NotificationEmail != null
                     && s.NotificationEmail != ""
                     && (s.LocationCode == locationCode || s.LocationCode == null))
            .OrderByDescending(s => s.LocationCode != null) // branch-specific beats company-wide
            .FirstOrDefaultAsync();

        return setting?.NotificationEmail;
    }

    /// <summary>
    /// Helper method to convert LocationId to LocationCode
    /// </summary>
    private string? GetLocationCodeById(int locationId)
    {
        return locationId switch
        {
            1 => "LAX",  // Los Angeles
            2 => "LSV",  // Las Vegas
            3 => "CHN",  // Chino
            4 => "PHX",  // Phoenix
            5 => "SND",  // San Diego
            _ => null
        };
    }
}
