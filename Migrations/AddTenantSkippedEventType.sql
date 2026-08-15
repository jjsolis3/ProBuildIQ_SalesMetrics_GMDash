-- Migration: Add TenantSkipped to CK_SignEvent_Type constraint
-- Date: 2026-03-13
-- Description: Extends the SignEvent type check constraint to allow 'TenantSkipped',
--              which is recorded when a Property Staff admin waives the tenant signature
--              on a sent envelope via the "Skip Tenant & Complete" action.

-- Drop existing constraint if present
IF EXISTS (
    SELECT 1
    FROM   sys.check_constraints
    WHERE  name = 'CK_SignEvent_Type'
      AND  parent_object_id = OBJECT_ID(N'[dbo].[SignEvent]')
)
BEGIN
    ALTER TABLE [dbo].[SignEvent] DROP CONSTRAINT [CK_SignEvent_Type];
END
GO

-- Recreate with full list including TenantSkipped
ALTER TABLE [dbo].[SignEvent]
    ADD CONSTRAINT [CK_SignEvent_Type]
    CHECK ([EventType] IN (
        'Sent','Opened','Consented','Signed',
        'Edited','Completed',
        'Voided','Declined','Expired','Downloaded','Reminded',
        'TenantSkipped'
    ));
GO

PRINT 'Migration AddTenantSkippedEventType completed successfully';
GO
