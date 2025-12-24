# Sign & Send Feature - Setup Guide

## Overview

The Sign & Send feature provides DocuSign-like functionality for sending documents for electronic signature. This feature includes:

- 📧 **Send Documents**: Create and send documents to recipients for signing
- ✍️ **Public Signing Portal**: Recipients can view and sign documents via secure link
- 📊 **Track Progress**: Monitor when recipients view and sign documents
- 📄 **Signed PDF**: Automatically generate and deliver signed PDFs
- 🔒 **Secure Access**: Token-based authentication with optional OTP
- 📝 **Audit Trail**: Complete history of document events

## Features

### For Senders (Authenticated Portal)
- Create envelopes from templates
- Add multiple recipients with sequential signing order
- Track envelope status (Sent, Viewed, Completed, Expired, Declined)
- Resend notifications to recipients
- Download completed signed PDFs
- View audit trail of all document events

### For Recipients (Public Portal)
- Secure access via unique token (no login required)
- View document content with property information
- Draw signature on canvas (desktop and mobile support)
- Type full name for consent
- Download signed PDF after completion
- Email notification upon completion

## Navigation

The Sign & Send feature has been added to the sidebar menu:

```
✍️ SIGN & SEND
├── Envelopes         (/SignAdmin/Index)
├── Send Document     (/SignAdmin/Create)
└── Templates         (/SignTemplates/Index) - Admin/GM only
```

## Required Configuration

### 1. SMTP Email Settings

Email notifications are essential for the Sign & Send feature. Update `appsettings.json` with your SMTP server details:

```json
"Smtp": {
  "Host": "smtp.gmail.com",           // Your SMTP server
  "Port": 587,                        // Usually 587 for TLS or 465 for SSL
  "User": "your-email@domain.com",    // SMTP username
  "Pass": "your-app-password",        // SMTP password or app password
  "FromEmail": "noreply@domain.com",  // From email address
  "FromName": "SalesMetrics",         // From name
  "EnableSsl": true                   // Enable SSL/TLS
}
```

**Common SMTP Providers:**

**Gmail:**
- Host: `smtp.gmail.com`
- Port: `587` (TLS) or `465` (SSL)
- EnableSsl: `true`
- Note: Use App Password if 2FA is enabled

**Office 365:**
- Host: `smtp.office365.com`
- Port: `587`
- EnableSsl: `true`

**SendGrid:**
- Host: `smtp.sendgrid.net`
- Port: `587`
- User: `apikey`
- Pass: Your SendGrid API Key
- EnableSsl: `true`

**AWS SES:**
- Host: `email-smtp.us-east-1.amazonaws.com` (region-specific)
- Port: `587`
- User: Your SMTP username
- Pass: Your SMTP password
- EnableSsl: `true`

### 2. Application Base URL

Update the base URL for email links in `appsettings.json`:

```json
"App": {
  "BaseUrl": "https://yourdomain.com"  // Your application URL
}
```

This URL is used to generate signing links sent to recipients.

### 3. Database Migrations

The Sign & Send feature requires database migrations. Ensure migrations are applied:

```bash
dotnet ef database update
```

**Migration files:**
- `20250925222207_AddSigningModule.cs` - Creates all signing tables
- `20250925223502_MoveSignTemplateSeedToMigration.cs` - Seeds templates
- `20250925225440_RemoveSignTemplateHasDataMetadata.cs` - Schema cleanup

## Database Tables

The feature uses the following tables:

- **SignEnvelope** - Main envelope/document container
- **SignRecipient** - Recipient information and signatures
- **SignTemplate** - Document templates (Razor views)
- **SignEvent** - Audit trail of all events
- **SignField** - Form field values
- **SignAttachment** - Supporting documents

## Usage Workflow

### Creating and Sending an Envelope

1. Navigate to **Sign & Send > Send Document**
2. Select a template (e.g., Tenant Consent, Occupied Release)
3. Fill in property information (optional)
4. Add recipient details:
   - Role: Manager, Tenant, or Other
   - Full Name and Email
   - Signing Order (for sequential signing)
5. Set expiration date (optional, defaults to 14 days)
6. Click **Create & Send**

### Recipient Signing Process

1. Recipient receives email with secure signing link
2. Clicks link to access public signing portal
3. Reviews document content and property information
4. For Managers: Can add tenant information if needed
5. Draws signature on canvas
6. Types full name for consent
7. Checks consent checkbox
8. Clicks **Sign & Finish**
9. Receives confirmation and download link (if all signers complete)

### Tracking Progress

1. Navigate to **Sign & Send > Envelopes**
2. View list of all envelopes with status
3. Click **Open** on any envelope to see details
4. View recipient status:
   - ⏳ Pending - Not yet viewed
   - 👁️ Viewed - Opened but not signed
   - ✅ Signed - Completed signature
5. Click **Resend** to send reminder emails
6. Download completed PDF when all signers finish

## Document Templates

Templates are Razor views that define document content. Two example templates are included:

1. **Tenant Consent** (`/Views/SignTemplates/TenantConsent.cshtml`)
2. **Occupied Release** (`/Views/SignTemplates/OccupiedRelease.cshtml`)

### Creating Custom Templates

Admins can create custom templates via **Sign & Send > Templates**:

1. Click **+ New Template**
2. Enter template details:
   - Template Key (unique identifier)
   - Display Name
   - Razor View Path (e.g., `/Views/SignTemplates/MyTemplate.cshtml`)
   - Default Subject and Message for emails
3. Upload template Razor view file
4. Configure merge specifications (JSON) for dynamic data
5. Activate template

## Public Signing URL Format

Recipients access documents via secure URLs:

```
https://yourdomain.com/sign/{token}
```

- `{token}` - Unique access token for recipient
- No authentication required (token-based access)
- Optional OTP support for enhanced security
- Token expiration enforced

## Security Features

- 🔐 Secure random tokens (32 bytes, cryptographically secure)
- ⏰ Token expiration (configurable, default 14 days)
- 📍 IP address tracking for views and signatures
- 🖥️ User-Agent tracking for audit trail
- 🔏 SHA-256 hashing of completed PDFs
- 📝 Complete audit trail (Sent, Opened, Signed, Downloaded, etc.)
- 🚫 Already-signed protection (recipients can't sign twice)
- ✅ Server-side validation of all inputs

## Files and Directories

**Controllers:**
- `/Controllers/SignAdminController.cs` - Admin envelope management
- `/Controllers/SignPublicController.cs` - Public recipient signing
- `/Controllers/SignTemplateController.cs` - Template management

**Services:**
- `/Services/Signing/EnvelopeService.cs` - Core envelope logic
- `/Services/Signing/PdfService.cs` - PDF generation and sealing
- `/Services/Signing/NotificationService.cs` - Email notifications
- `/Services/Signing/ErpMergeService.cs` - ERP data integration

**Views:**
- `/Views/SignAdmin/` - Admin portal views
- `/Views/SignPublic/` - Public signing portal views
- `/Views/SignTemplates/` - Template management and examples

**Domain Models:**
- `/Domain/Signing/` - All signing entities

**Migrations:**
- `/Migrations/20250925222207_AddSigningModule.cs`

**Signed PDFs Storage:**
- `/Files/Sign/YYYY/MM/` - Organized by year and month

## Troubleshooting

### Emails Not Sending

**Issue:** Recipients not receiving signing invitation emails

**Solutions:**
1. Verify SMTP settings in `appsettings.json`
2. Check SMTP server firewall rules (allow outbound on port 587/465)
3. Test SMTP credentials manually
4. Check spam/junk folders
5. Verify `FromEmail` is allowed by SMTP server
6. Enable logging to see SMTP errors:
   ```json
   "Logging": {
     "LogLevel": {
       "Default": "Information"
     }
   }
   ```

### Token Invalid or Expired

**Issue:** Recipients see "Invalid or Expired" page

**Solutions:**
1. Check token expiration in database (`AccessTokenExpiresAt`)
2. Verify URL wasn't truncated in email
3. Resend envelope from admin portal
4. Check system clock (UTC timestamp issues)

### Signature Not Saving

**Issue:** Signature canvas appears but doesn't submit

**Solutions:**
1. Ensure JavaScript is enabled in browser
2. Check browser console for errors
3. Verify `SigData` hidden input has base64 data
4. Check server-side validation errors
5. Ensure consent checkbox is checked

### PDF Not Generated

**Issue:** Envelope marked complete but no PDF available

**Solutions:**
1. Check file system permissions for `/Files/Sign/` directory
2. Verify PdfSharpCore library is installed
3. Check server logs for PDF generation errors
4. Ensure all signatures captured successfully

## API Endpoints Reference

### Public Routes (No Authentication)

| Method | Route | Description |
|--------|-------|-------------|
| GET | `/sign/{token}` | View document and signing form |
| POST | `/sign/{token}` | Submit signature |
| GET | `/sign/signed` | Success page with download link |

### Admin Routes (Authentication Required)

| Method | Route | Description |
|--------|-------|-------------|
| GET | `/SignAdmin` | List all envelopes |
| GET | `/SignAdmin/Create` | Create new envelope form |
| POST | `/SignAdmin/Create` | Submit new envelope |
| GET | `/SignAdmin/Details/{id}` | View envelope details |
| POST | `/SignAdmin/Resend/{id}` | Resend notification emails |

### Template Routes (Admin/GM Only)

| Method | Route | Description |
|--------|-------|-------------|
| GET | `/SignTemplates` | List templates |
| GET | `/SignTemplates/Create` | Create template form |
| POST | `/SignTemplates/Create` | Save new template |
| GET | `/SignTemplates/Edit/{key}` | Edit template |
| POST | `/SignTemplates/Edit/{key}` | Update template |

## Support

For questions or issues with the Sign & Send feature, please contact the development team or create an issue in the project repository.

## Version History

- **v1.0.0** (2025-12-22) - Initial Sign & Send feature release
  - Multi-recipient signing with sequential order
  - Email notifications
  - Public signing portal
  - Admin tracking dashboard
  - Template management
  - Audit trail
  - PDF generation
