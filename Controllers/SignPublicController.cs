// Controllers/SignPublicController.cs
using Microsoft.AspNetCore.Mvc;
using SalesMetrics.Services.Signing;
using SalesMetrics.Models.Signing;

namespace SalesMetrics.Controllers;

[Route("sign")]
public class SignPublicController : Controller
{
    private readonly IEnvelopeService _svc;
    private readonly ILogger<SignPublicController> _logger;

    public SignPublicController(IEnvelopeService svc, ILogger<SignPublicController> logger)
    {
        _svc = svc;
        _logger = logger;
    }

    [HttpGet("{token}")]
    public async Task<IActionResult> Review(string token)
    {
        _logger.LogInformation("GET Review called with token: {Token}", token);
        var vm = await _svc.GetReviewAsync(token, Request.Headers["User-Agent"], HttpContext.Connection.RemoteIpAddress?.ToString() ?? "n/a");
        
        if (vm == null)
        {
            _logger.LogWarning("Token {Token} is invalid or expired", token);
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

            return View("AlreadySigned");
        }

        ViewBag.Token = token;
        return View(vm);
    }

    [HttpPost("/sign/{token}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Review(string token, [FromForm] ReviewPostVm post)
    {
        _logger.LogInformation("POST Review called with token: {Token}", token);
        _logger.LogInformation("POST Data - Accept: {Accept}, TypedFullName: {Name}, SigDataLength: {SigLength}, TenantName: {TenantName}, TenantEmail: {TenantEmail}",
            post.Accept,
            post.TypedFullName,
            post.SigData?.Length ?? 0,
            post.TenantFullName,
            post.TenantEmail);

        // add UA + IP to match the interface
        var vm = await _svc.GetReviewAsync(
            token,
            Request.Headers["User-Agent"],
            HttpContext.Connection.RemoteIpAddress?.ToString() ?? "n/a");

        if (vm is null)
        {
            _logger.LogWarning("Token {Token} not found during POST", token);
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
            return View(vm);
        }

        // Manager must capture tenant info if no tenant recipient already
        if (vm.Recipient.Role == "Manager" && vm.HasTenantRecipient == false)
        {
            if (string.IsNullOrWhiteSpace(post.TenantFullName) || string.IsNullOrWhiteSpace(post.TenantEmail))
            {
                _logger.LogWarning("Manager validation failed: Tenant info missing");
                ModelState.AddModelError("", "Please enter the tenant name and email.");
                ViewBag.Token = token;
                return View(vm);
            }

            _logger.LogInformation("Adding tenant recipient: {TenantName} <{TenantEmail}>", post.TenantFullName, post.TenantEmail);
            await _svc.UpsertTenantRecipientAsync(vm.Envelope.EnvelopeId, post.TenantFullName.Trim(), post.TenantEmail.Trim());
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
        return View();
    }
}

