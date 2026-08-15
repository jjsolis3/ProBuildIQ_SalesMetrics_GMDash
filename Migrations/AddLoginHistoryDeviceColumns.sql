-- Add DeviceInfo, UserAgentRaw, and ErrorLog columns to LoginHistory.
-- These columns were added to the LogLoginAttemptAsync INSERT in the
-- login-security-hardening commit but were never added to the table schema,
-- causing a SQL exception (and a 500 error) on every login attempt.

IF NOT EXISTS (
    SELECT 1 FROM sys.columns
    WHERE object_id = OBJECT_ID(N'dbo.LoginHistory')
      AND name = N'DeviceInfo'
)
    ALTER TABLE [dbo].[LoginHistory]
        ADD [DeviceInfo] NVARCHAR(500) NULL;

IF NOT EXISTS (
    SELECT 1 FROM sys.columns
    WHERE object_id = OBJECT_ID(N'dbo.LoginHistory')
      AND name = N'UserAgentRaw'
)
    ALTER TABLE [dbo].[LoginHistory]
        ADD [UserAgentRaw] NVARCHAR(2000) NULL;

IF NOT EXISTS (
    SELECT 1 FROM sys.columns
    WHERE object_id = OBJECT_ID(N'dbo.LoginHistory')
      AND name = N'ErrorLog'
)
    ALTER TABLE [dbo].[LoginHistory]
        ADD [ErrorLog] NVARCHAR(MAX) NULL;
