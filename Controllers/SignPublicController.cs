// Controllers/SignPublicController.cs
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using System.Text.RegularExpressions;
using SalesMetrics.Services.Signing;
using SalesMetrics.Models.Signing;

namespace SalesMetrics.Controllers;

[Route("sign")]
public class SignPublicController : Controller
{
    private readonly IEnvelopeService _svc;
    private readonly ILogger<SignPublicController> _logger;
    private readonly CompanyBrandingSettings _branding;

    public SignPublicController(
        IEnvelopeService svc,
        ILogger<SignPublicController> logger,
        IOptions<CompanyBrandingSettings> branding)
    {
        _svc = svc;
        _logger = logger;
        _branding = branding.Value;
    }

    [HttpGet("{token}")]
    public async Task<IActionResult> Review(string token)
    {
        _logger.LogInformation("GET Review called with token: {Token}", token);
        var vm = await _svc.GetReviewAsync(token, Request.Headers["User-Agent"], HttpContext.Connection.RemoteIpAddress?.ToString() ?? "n/a");

        if (vm == null)
        {
            _logger.LogWarning("Token {Token} is invalid or expired", token);
            ViewBag.CompanyBranding = _branding;
            return View("InvalidOrExpired");
        }

        // Check if this recipient already signed
        if (vm.Recipient.SignedAtUtc != null)
        {
            _logger.LogInformation("Recipient {RecipientId} already signed, redirecting to AlreadySigned view",
                vm.Recipient.RecipientId);

            // Check if envelope is complete and has PDF
            var downloadUrl = vm.Envelope.Status == "Completed" ? vm.Envelope.PdfStoragePath : null;
            ViewBag.DownloadUrl = downloadUrl;
            ViewBag.RecipientName = vm.Recipient.FullName;
            ViewBag.SignedDate = vm.Recipient.SignedAtUtc.Value.ToLocalTime();
            ViewBag.CompanyBranding = _branding;

            return View("AlreadySigned");
        }

        ViewBag.Token = token;
        ViewBag.CompanyBranding = _branding;
        return View(vm);
    }

    [HttpPost("/sign/{token}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Review(string token, [FromForm] ReviewPostVm post)
    {
        _logger.LogInformation("POST Review called with token: {Token}", token);
        _logger.LogInformation("POST Data - Accept: {Accept}, TypedFullName: {Name}, SigDataLength: {SigLength}, TenantName: {TenantName}, TenantEmail: {TenantEmail}, TenantPhone: {TenantPhone}",
            post.Accept,
            post.TypedFullName,
            post.SigData?.Length ?? 0,
            post.TenantFullName,
            post.TenantEmail,
            post.TenantPhone);

        // add UA + IP to match the interface
        var vm = await _svc.GetReviewAsync(
            token,
            Request.Headers["User-Agent"],
            HttpContext.Connection.RemoteIpAddress?.ToString() ?? "n/a");

        if (vm is null)
        {
            _logger.LogWarning("Token {Token} not found during POST", token);
            ViewBag.CompanyBranding = _branding;
            return View("InvalidOrExpired");
        }

        // Server-side validation with detailed logging
        if (!post.Accept)
        {
            _logger.LogWarning("Validation failed: Accept checkbox not checked");
            ModelState.AddModelError("", "Please check the consent checkbox.");
        }

        if (string.IsNullOrWhiteSpace(post.TypedFullName))
        {
            _logger.LogWarning("Validation failed: TypedFullName is empty");
            ModelState.AddModelError("", "Please type your full name.");
        }

        if (string.IsNullOrWhiteSpace(post.SigData))
        {
            _logger.LogWarning("Validation failed: SigData is empty");
            ModelState.AddModelError("", "Please draw your signature.");
        }

        if (!ModelState.IsValid)
        {
            _logger.LogWarning("Form validation failed, returning view");
            ViewBag.Token = token;
            ViewBag.CompanyBranding = _branding;
            return View(vm);
        }

        // Manager must capture tenant info if no tenant recipient already (unless skipping tenant)
        if (vm.Recipient.Role == "Manager" && vm.HasTenantRecipient == false)
        {
            if (!post.SkipTenant)
            {
                // Only require tenant info if not skipping
                if (string.IsNullOrWhiteSpace(post.TenantFullName) || string.IsNullOrWhiteSpace(post.TenantEmail))
                {
                    _logger.LogWarning("Manager validation failed: Tenant info missing");
                    ModelState.AddModelError("", "Please enter the tenant name and email, or check 'Skip tenant signature'.");
                    ViewBag.Token = token;
                    ViewBag.CompanyBranding = _branding;
                    return View(vm);
                }

                // Validate tenant email format
                if (!Regex.IsMatch(post.TenantEmail.Trim(), @"^[^@\s]+@[^@\s]+\.[^@\s]+$"))
                {
                    _logger.LogWarning("Manager validation failed: Invalid tenant email format: {Email}", post.TenantEmail);
                    ModelState.AddModelError("", "Please enter a valid email address for the tenant.");
                    ViewBag.Token = token;
                    ViewBag.CompanyBranding = _branding;
                    return View(vm);
                }

                _logger.LogInformation("Adding tenant recipient: {TenantName} <{TenantEmail}> Phone: {TenantPhone}", post.TenantFullName, post.TenantEmail, post.TenantPhone);
                await _svc.UpsertTenantRecipientAsync(vm.Envelope.EnvelopeId, post.TenantFullName.Trim(), post.TenantEmail.Trim(), post.TenantPhone?.Trim());
            }
            else
            {
                _logger.LogInformation("Manager chose to skip tenant signature for envelope {EnvelopeId}", vm.Envelope.EnvelopeId);
                // Mark envelope as tenant skipped
                await _svc.MarkTenantSkippedAsync(vm.Envelope.EnvelopeId, vm.Recipient.FullName);
            }
        }

        // Capture the signature
        _logger.LogInformation("Capturing signature for recipient {RecipientId}", vm.Recipient.RecipientId);
        await _svc.CaptureSignatureAsync(vm.Envelope.EnvelopeId, vm.Recipient.RecipientId, post.TypedFullName!.Trim(), post.SigData!);

        // NEW: Check if this was the last signer and finalize if needed
        var (isComplete, downloadUrl) = await _svc.TryFinalizeEnvelopeAsync(vm.Envelope.EnvelopeId);

        if (isComplete)
        {
            _logger.LogInformation("Envelope {EnvelopeId} completed, redirecting to Signed page with download",
                vm.Envelope.EnvelopeId);
            return RedirectToAction("Signed", new { url = downloadUrl });
        }

        // Not yet complete, Progress to next signer (or complete if all done)
        _logger.LogInformation("Progressing to next signer for envelope {EnvelopeId}", vm.Envelope.EnvelopeId);
        await _svc.ProgressToNextAsync(vm.Envelope.EnvelopeId);

        _logger.LogInformation("Signature process completed, redirecting to Signed page");
        return RedirectToAction("Signed");
    }

    // Success page after signing
    [HttpGet("signed")]
    public IActionResult Signed(string? url)
    {
        _logger.LogInformation("Signed page displayed, download URL: {Url}", url ?? "none");
        ViewBag.DownloadUrl = url;
        ViewBag.CompanyBranding = _branding;
        return View();
    }

    // Preview PDF template before signing
    [HttpGet("{token}/pdf")]
    public async Task<IActionResult> PreviewPDF(string token, [FromServices] IPdfService pdfService)
    {
        _logger.LogInformation("PDF Preview requested for token: {Token}", token);

        var vm = await _svc.GetReviewAsync(token, Request.Headers["User-Agent"], HttpContext.Connection.RemoteIpAddress?.ToString() ?? "n/a");

        if (vm == null)
        {
            _logger.LogWarning("Token {Token} is invalid", token);
            return NotFound("Invalid or expired link");
        }

        try
        {
            // Generate progressive preview showing only fields visible to this recipient
            _logger.LogInformation("Generating preview PDF for envelope {EnvelopeId}, recipient {RecipientId} ({Role})",
                vm.Envelope.EnvelopeId, vm.Recipient.RecipientId, vm.Recipient.Role);

            var fileBytes = await pdfService.GeneratePreviewPdfAsync(vm.Envelope.EnvelopeId, vm.Recipient.RecipientId);

            Response.Headers.Add("Content-Disposition", "inline");
            return File(fileBytes, "application/pdf");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating preview PDF for envelope {EnvelopeId}", vm.Envelope.EnvelopeId);

            // Fallback to blank template if preview generation fails
            var template = await _svc.GetTemplateByKeyAsync(vm.Envelope.TemplateKey);
            if (template == null || string.IsNullOrWhiteSpace(template.PdfFilePath))
            {
                _logger.LogWarning("No PDF template found for template key: {TemplateKey}", vm.Envelope.TemplateKey);
                return NotFound("PDF template not found");
            }

            var filePath = Path.Combine("Content", "Templates", template.PdfFilePath);

            if (!System.IO.File.Exists(filePath))
            {
                _logger.LogWarning("PDF file not found: {FilePath}", filePath);
                return NotFound("PDF file not found");
            }

            _logger.LogInformation("Serving fallback blank PDF template: {FilePath}", filePath);

            var fileBytes = await System.IO.File.ReadAllBytesAsync(filePath);
            Response.Headers.Add("Content-Disposition", "inline");
            return File(fileBytes, "application/pdf");
        }
    }

    /// <summary>
    /// GET /sign/{token}/status - Check envelope status
    /// Allows recipients to check the status of their signature request
    /// </summary>
    [HttpGet("{token}/status")]
    public async Task<IActionResult> Status(string token)
    {
        _logger.LogInformation("GET Status called with token: {Token}", token);
        var vm = await _svc.GetReviewAsync(token, Request.Headers["User-Agent"], HttpContext.Connection.RemoteIpAddress?.ToString() ?? "n/a");

        if (vm == null)
        {
            _logger.LogWarning("Token {Token} is invalid or expired", token);
            ViewBag.CompanyBranding = _branding;
            return View("InvalidOrExpired");
        }

        // Pass status information to view
        ViewBag.CompanyBranding = _branding;
        ViewBag.Token = token;

        return View(vm);
    }

    /// <summary>
    /// POST /sign/{token}/decline - Decline to sign envelope
    /// </summary>
    [HttpPost("{token}/decline")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Decline(string token, string? reason)
    {
        _logger.LogInformation("POST Decline called with token: {Token}", token);

        var success = await _svc.DeclineEnvelopeAsync(
            token,
            reason ?? "No reason provided",
            Request.Headers["User-Agent"],
            HttpContext.Connection.RemoteIpAddress?.ToString() ?? "n/a");

        if (!success)
        {
            _logger.LogWarning("Failed to decline envelope with token: {Token}", token);
            ViewBag.CompanyBranding = _branding;
            return View("InvalidOrExpired");
        }

        ViewBag.CompanyBranding = _branding;
        ViewBag.Reason = reason;
        return View("Declined");
    }
}

