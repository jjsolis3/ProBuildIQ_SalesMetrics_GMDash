-- ============================================================
-- AddHtmlBodyContentToSignTemplate.sql
-- Adds DB-stored HTML body field to SignTemplate, allowing
-- envelope document content to be authored from the frontend
-- without code deployments.
-- RazorViewPath is made nullable (kept as legacy fallback).
-- Safe to run multiple times.
-- ============================================================

-- 1. Make RazorViewPath nullable (was NOT NULL)
IF EXISTS (
    SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS
    WHERE TABLE_NAME = 'SignTemplate'
      AND COLUMN_NAME = 'RazorViewPath'
      AND IS_NULLABLE = 'NO'
)
BEGIN
    -- Provide empty string default for existing nulls before relaxing constraint
    UPDATE SignTemplate SET RazorViewPath = '' WHERE RazorViewPath IS NULL;
    ALTER TABLE SignTemplate ALTER COLUMN RazorViewPath nvarchar(260) NULL;
END

-- 2. Add HtmlBodyContent column
IF NOT EXISTS (
    SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS
    WHERE TABLE_NAME = 'SignTemplate'
      AND COLUMN_NAME = 'HtmlBodyContent'
)
BEGIN
    ALTER TABLE SignTemplate ADD HtmlBodyContent nvarchar(max) NULL;
END
