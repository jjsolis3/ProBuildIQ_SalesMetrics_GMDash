-- AddPasswordResetTokens.sql
-- Creates the PasswordResetTokens table for the self-service password reset flow.

IF NOT EXISTS (
    SELECT 1 FROM INFORMATION_SCHEMA.TABLES
    WHERE TABLE_SCHEMA = 'dbo' AND TABLE_NAME = 'PasswordResetTokens'
)
BEGIN
    CREATE TABLE [dbo].[PasswordResetTokens] (
        [Id]        INT           IDENTITY(1,1) NOT NULL
                        CONSTRAINT PK_PasswordResetTokens PRIMARY KEY,
        [Token]     NVARCHAR(300) NOT NULL,
        [Email]     NVARCHAR(255) NOT NULL,
        [ExpiresAt] DATETIME      NOT NULL,
        [UsedAt]    DATETIME      NULL,
        [CreatedAt] DATETIME      NOT NULL CONSTRAINT DF_PasswordResetTokens_CreatedAt DEFAULT GETDATE()
    );

    CREATE UNIQUE INDEX [UX_PasswordResetTokens_Token]
        ON [dbo].[PasswordResetTokens] ([Token]);
END
