-- ============================================================
-- AddBrandingSettings.sql
-- Seeds default rows for Company Branding settings in the
-- existing SecuritySettings table (Category = 'Branding').
-- Safe to run multiple times — inserts only if key is absent.
-- ============================================================

IF NOT EXISTS (SELECT 1 FROM SecuritySettings WHERE Category = 'Branding' AND SettingKey = 'CompanyName')
    INSERT INTO SecuritySettings (SettingKey, SettingValue, Description, Category, LastModifiedDate, LastModifiedByUserId)
    VALUES ('CompanyName', '', 'Your company display name used in emails and footers.', 'Branding', GETUTCDATE(), 1);

IF NOT EXISTS (SELECT 1 FROM SecuritySettings WHERE Category = 'Branding' AND SettingKey = 'LogoUrl')
    INSERT INTO SecuritySettings (SettingKey, SettingValue, Description, Category, LastModifiedDate, LastModifiedByUserId)
    VALUES ('LogoUrl', '', 'Absolute URL to the company logo shown in email headers.', 'Branding', GETUTCDATE(), 1);

IF NOT EXISTS (SELECT 1 FROM SecuritySettings WHERE Category = 'Branding' AND SettingKey = 'EnvelopeLogoUrl')
    INSERT INTO SecuritySettings (SettingKey, SettingValue, Description, Category, LastModifiedDate, LastModifiedByUserId)
    VALUES ('EnvelopeLogoUrl', '', 'Absolute URL for the E-Sign logo shown inside envelope emails. Leave blank to hide.', 'Branding', GETUTCDATE(), 1);

IF NOT EXISTS (SELECT 1 FROM SecuritySettings WHERE Category = 'Branding' AND SettingKey = 'Website')
    INSERT INTO SecuritySettings (SettingKey, SettingValue, Description, Category, LastModifiedDate, LastModifiedByUserId)
    VALUES ('Website', '', 'Company website URL shown in the email footer.', 'Branding', GETUTCDATE(), 1);

IF NOT EXISTS (SELECT 1 FROM SecuritySettings WHERE Category = 'Branding' AND SettingKey = 'Phone')
    INSERT INTO SecuritySettings (SettingKey, SettingValue, Description, Category, LastModifiedDate, LastModifiedByUserId)
    VALUES ('Phone', '', 'Company phone number shown in the email footer.', 'Branding', GETUTCDATE(), 1);
