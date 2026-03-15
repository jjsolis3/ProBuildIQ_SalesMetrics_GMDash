-- AddAppCredentials.sql
-- Creates the AppCredentials table for encrypted storage of sensitive application
-- credentials (Google OAuth, SMTP) so they do not need to live in appsettings.json.

IF NOT EXISTS (
    SELECT 1 FROM INFORMATION_SCHEMA.TABLES
    WHERE TABLE_SCHEMA = 'dbo' AND TABLE_NAME = 'AppCredentials'
)
BEGIN
    CREATE TABLE [dbo].[AppCredentials] (
        [CredentialId]         INT IDENTITY(1,1) NOT NULL,
        [CredentialKey]        NVARCHAR(100)      NOT NULL,   -- e.g. "Google_ClientId"
        [EncryptedValue]       NVARCHAR(2000)     NOT NULL,   -- AES-256-CBC, base64
        [Category]             NVARCHAR(50)       NOT NULL,   -- "Google" | "Smtp"
        [Description]          NVARCHAR(500)      NULL,
        [LastModifiedDate]     DATETIME           NOT NULL CONSTRAINT [DF_AppCredentials_LastModifiedDate] DEFAULT GETDATE(),
        [LastModifiedByUserId] INT                NOT NULL,
        CONSTRAINT [PK_AppCredentials] PRIMARY KEY CLUSTERED ([CredentialId] ASC),
        CONSTRAINT [UQ_AppCredentials_Key] UNIQUE ([CredentialKey])
    );
END
