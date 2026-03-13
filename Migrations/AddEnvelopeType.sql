-- Migration: AddEnvelopeType
-- Adds EnvelopeType column to SignEnvelope.
-- 'Consent'       = standard signature-required envelope (default, backward-compatible).
-- 'Communication' = tracked email delivery only; no signature is collected.

IF NOT EXISTS (
    SELECT 1 FROM sys.columns
    WHERE object_id = OBJECT_ID('dbo.SignEnvelope') AND name = 'EnvelopeType'
)
BEGIN
    ALTER TABLE [dbo].[SignEnvelope]
        ADD [EnvelopeType] NVARCHAR(20) NOT NULL DEFAULT 'Consent';
END
