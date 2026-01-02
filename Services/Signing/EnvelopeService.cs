// Services/Signing/EnvelopeService.cs
using Microsoft.EntityFrameworkCore;
using SalesMetrics.Data;
using SalesMetrics.Domain.Signing;
using SalesMetrics.Models.Signing;
using SalesMetrics.Utilities.Security;

using Microsoft.Extensions.Options;
using SalesMetrics.Services.Signing; // for AppSettings
using System.Text.Json;

namespace SalesMetrics.Services.Signing;

public sealed class EnvelopeService : IEnvelopeService
{
    private readonly SalesMetricsDbContext _db;
    private readonly INotificationService _notify;
    private readonly IErpMergeService _merge;
    private readonly IPdfService _pdf;
    private readonly AppSettings _appSettings;

    public EnvelopeService(SalesMetricsDbContext db, INotificationService notify, IErpMergeService merge, IPdfService pdf, IOptions<AppSettings> appSettings)
    {
        _db = db; _notify = notify; _merge = merge; _pdf = pdf; _appSettings = appSettings.Value;
    }

    public async Task<long> CreateAsync(int createdByUsersId, CreateEnvelopeVm vm)
    {
        // Load the template to access MergeSpec and PDF path
        var template = await _db.SignTemplates
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.TemplateKey == vm.TemplateKey);

        if (template == null)
            throw new InvalidOperationException($"Template '{vm.TemplateKey}' not found");

        var env = new SignEnvelope
        {
            TemplateKey = vm.TemplateKey,
            Subject = vm.Subject,
            MessageBody = vm.MessageBody,
            PropertyID = vm.PropertyID,
            OrderId = vm.OrderId,
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
            Console.WriteLine($"[CreateFields] No MergeSpec found for template {template.TemplateKey}");
            return;
        }

        try
        {
            Console.WriteLine($"[CreateFields] Parsing MergeSpec for envelope {env.EnvelopeId}");
            var mergeSpec = JsonSerializer.Deserialize<MergeSpec>(template.MergeSpecJson);

            if (mergeSpec?.fieldMapping == null || mergeSpec.fieldMapping.Count == 0)
            {
                Console.WriteLine($"[CreateFields] No field mappings found in MergeSpec");
                return;
            }

            Console.WriteLine($"[CreateFields] Found {mergeSpec.fieldMapping.Count} fields in MergeSpec");

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
                Console.WriteLine($"[CreateFields] Created field: {fieldKey} (type: {fieldType}, role: {fieldInfo.role ?? "N/A"}, value: {fieldValue ?? "NULL"})");
            }

            Console.WriteLine($"[CreateFields] Created {env.Fields.Count} SignField records");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[CreateFields ERROR] Failed to parse MergeSpec: {ex.Message}");
            Console.WriteLine($"[CreateFields ERROR] Stack trace: {ex.StackTrace}");
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
            data["PropertyName"] = propertyName;
            data["PropertyAddress"] = "";
            data["PropertyPhone"] = "";

            // Order fields
            var unitNumber = await _merge.GetUnitNumberByOrderIdAsync(env.OrderId) ?? "";
            data["UnitNumber"] = unitNumber;
            data["InstallationDate"] = DateTime.UtcNow.ToString("MM/dd/yyyy");
            data["DeliveryDate"] = "";

            // Recipient fields - these will be populated at signing time
            var manager = env.Recipients.FirstOrDefault(r => r.Role == "Manager");
            var tenant = env.Recipients.FirstOrDefault(r => r.Role == "Tenant") ?? env.Recipients.First();

            data["PropertyStaffName"] = manager?.FullName ?? "Property Staff";
            data["PropertyStaffDate"] = ""; // Will be filled when signed
            data["PropertyStaffSignature"] = ""; // Will be filled when signed

            data["ResidentName"] = tenant.FullName;
            data["ResidentPhone"] = tenant.Phone ?? "";
            data["ResidentSignature"] = ""; // Will be filled when signed
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[GatherFieldData ERROR] {ex.Message}");
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
        var env = await _db.SignEnvelopes.Include(e => e.Recipients).FirstAsync(e => e.EnvelopeId == envelopeId);
        if (env.Status == "Draft") env.Status = "Sent";
        env.SentAtUtc = env.SentAtUtc ?? DateTime.UtcNow;
        await _db.SaveChangesAsync();

        foreach (var r in env.Recipients.OrderBy(x => x.SignerOrder))
        {
            var baseUrl = _appSettings.BaseUrl.TrimEnd('/');
            var link = $"{baseUrl}/sign/{r.AccessToken}";

            // Build email body with optional custom message
            var messageHtml = !string.IsNullOrWhiteSpace(env.MessageBody)
                ? $"<p>{env.MessageBody}</p>"
                : "";

            var html = $"""
                <p>Hello {r.FullName},</p>
                <p>Please review and sign the document: <b>{env.Subject}</b>.</p>
                {messageHtml}
                <p><a href="{link}">Open & Sign</a></p>
                """;
            await _notify.SendEnvelopeEmailAsync(r.Email, r.FullName, env.Subject, html);

            _db.SignEvents.Add(new SignEvent { EnvelopeId = env.EnvelopeId, RecipientId = r.RecipientId, EventType = "Sent", OccurredAtUtc = DateTime.UtcNow });
        }
        await _db.SaveChangesAsync();
    }

    public async Task<EnvelopeDetailsVm?> GetDetailsAsync(long envelopeId)
    {
        var e = await _db.SignEnvelopes
            .Include(x => x.Recipients)
            .Include(x => x.Events)
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.EnvelopeId == envelopeId);
        if (e is null) return null;

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
            Recipients = e.Recipients.OrderBy(r => r.SignerOrder).Select(r => new EnvelopeDetailsVm.RecipientVm
            {
                RecipientId = r.RecipientId,
                Role = r.Role,
                SignerOrder = r.SignerOrder,
                FullName = r.FullName,
                Email = r.Email,
                ViewedAtUtc = r.ViewedAtUtc,
                SignedAtUtc = r.SignedAtUtc,
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

    public async Task<(IReadOnlyList<EnvelopeListItemVm> Rows, int Total)> SearchAsync(string? status, string? office, int page, int pageSize)
    {
        var q = _db.SignEnvelopes.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(status)) q = q.Where(e => e.Status == status);
        if (!string.IsNullOrWhiteSpace(office)) q = q.Where(e => e.LocationCode == office);

        var total = await q.CountAsync();
        var rows = await q
            .OrderByDescending(e => e.SentAtUtc ?? e.CreatedDateUtc)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(e => new EnvelopeListItemVm
            {
                EnvelopeId = e.EnvelopeId,
                Subject = e.Subject,
                TemplateKey = e.TemplateKey,
                Status = e.Status,
                LocationCode = e.LocationCode,
                SentAtUtc = e.SentAtUtc,
                CompletedAtUtc = e.CompletedAtUtc,
                ExpiresAtUtc = e.ExpiresAtUtc,
                RecipientCount = e.Recipients.Count,
                SignedCount = e.Recipients.Count(r => r.SignedAtUtc != null),
            })
            .ToListAsync();

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

        // record "Opened"
        r.ViewedAtUtc ??= DateTime.UtcNow;
        r.IPAddressViewed ??= ip;
        r.UserAgentViewed ??= userAgent;

        _db.SignEvents.Add(new SignEvent
        {
            EnvelopeId = r.EnvelopeId,
            RecipientId = r.RecipientId,
            EventType = "Opened",
            OccurredAtUtc = DateTime.UtcNow
        });
        await _db.SaveChangesAsync();

        // render HTML body for template
        var html = await _merge.RenderHtmlAsync(r.Envelope.TemplateKey, r.EnvelopeId, r.RecipientId);

        // find if a tenant recipient exists
        var hasTenant = await _db.SignRecipients
            .AnyAsync(x => x.EnvelopeId == r.EnvelopeId && x.Role == "Tenant");

        // (optional) property summary; return null if you don't have this yet
        PropertyVm? prop = null;
        // prop = await _merge.GetPropertySummaryAsync(r.Envelope.PropertyID); // if you implement it

        return new ReviewVm
        {
            Token = token,
            Envelope = r.Envelope,
            Recipient = r,
            HasTenantRecipient = hasTenant,
            Property = prop,
            HtmlBody = html
        };
    }

    public async Task<(bool ok, string? downloadUrl)> SignAsync(SignSubmitDto dto, string userAgent, string ip)
    {
        if (!dto.Consented) return (false, null);

        var r = await _db.SignRecipients.Include(x => x.Envelope).FirstOrDefaultAsync(x => x.AccessToken == dto.Token);
        if (r is null) return (false, null);

        r.SignedAtUtc = DateTime.UtcNow;
        r.IPAddressSigned = ip;
        r.UserAgentSigned = userAgent;
        r.SignatureTyped = dto.SignatureTyped;
        if (!string.IsNullOrWhiteSpace(dto.SignatureImageBase64))
        {
            // save PNG to /Files/Sign/YYYY/MM/<recipient>.png
            var bytes = Convert.FromBase64String(dto.SignatureImageBase64.Split(',').Last());
            var dir = Path.Combine("wwwroot", "Files", "Sign", DateTime.UtcNow.ToString("yyyy"), DateTime.UtcNow.ToString("MM"));
            Directory.CreateDirectory(dir);
            var file = Path.Combine(dir, $"sig-{r.RecipientId}.png");
            await File.WriteAllBytesAsync(file, bytes);
            r.SignatureImagePath = file.Replace("wwwroot", "").Replace("\\", "/");
        }

        _db.SignEvents.Add(new SignEvent { EnvelopeId = r.EnvelopeId, RecipientId = r.RecipientId, EventType = "Signed", OccurredAtUtc = DateTime.UtcNow });
        await _db.SaveChangesAsync();

        // If all signers done → finalize
        var env = r.Envelope;
        var allSigned = await _db.SignRecipients.Where(x => x.EnvelopeId == env.EnvelopeId).AllAsync(x => x.SignedAtUtc != null);
        if (allSigned)
        {
            var pdf = await _pdf.RenderAndSealAsync(env.EnvelopeId);
            env.PdfStoragePath = pdf.storagePath.Replace("\\", "/");
            env.PdfSha256 = pdf.sha256;
            env.Status = "Completed";
            env.CompletedAtUtc = DateTime.UtcNow;

            _db.SignEvents.Add(new SignEvent { EnvelopeId = env.EnvelopeId, EventType = "Downloaded", OccurredAtUtc = DateTime.UtcNow, MetaJson = "{\"auto\":\"finalized\"}" });
            await _db.SaveChangesAsync();

            var downloadUrl = env.PdfStoragePath;
            // notify all recipients + front desk (basic example)
            foreach (var rr in await _db.SignRecipients.Where(x => x.EnvelopeId == env.EnvelopeId).ToListAsync())
                await _notify.SendCompletedReceiptAsync(rr.Email, rr.FullName, $"Completed: {env.Subject}",
                    "<p>Your document has been completed.</p>", downloadUrl);

            return (true, downloadUrl);
        }

        return (true, null);
    }

    public async Task UpsertTenantRecipientAsync(long envelopeId, string fullName, string email, string? phone = null)
    {
        var env = await _db.SignEnvelopes.Include(e => e.Recipients).FirstAsync(e => e.EnvelopeId == envelopeId);
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

    public async Task CaptureSignatureAsync(long envelopeId, long recipientId, string typedFullName, string sigDataBase64)
    {
        var rec = await _db.SignRecipients.FirstAsync(r => r.RecipientId == recipientId && r.EnvelopeId == envelopeId);

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
            .FirstAsync(e => e.EnvelopeId == envelopeId);

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

        // Send completion emails to all recipients
        var downloadUrl = env.PdfStoragePath;
        foreach (var recipient in env.Recipients)
        {
            await _notify.SendCompletedReceiptAsync(
                recipient.Email,
                recipient.FullName,
                $"Completed: {env.Subject}",
                $"<p>Thank you for signing. The document has been completed by all parties.</p>",
                downloadUrl);
        }

        return (true, downloadUrl);
    }

    public async Task ProgressToNextAsync(long envelopeId)
    {
        var env = await _db.SignEnvelopes.Include(e => e.Recipients)
                                         .FirstAsync(e => e.EnvelopeId == envelopeId);
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
        var html = $"""
        <p>Hello {next.FullName},</p>
        <p>Please review and sign the document: <b>{env.Subject}</b>.</p>
        <p><a href="{link}">Open & Sign</a></p>
        """;
        await _notify.SendEnvelopeEmailAsync(next.Email, next.FullName, env.Subject, html);

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

}
