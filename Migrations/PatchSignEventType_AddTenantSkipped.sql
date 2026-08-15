-- Migration: Restore TenantSkipped to CK_SignEvent_Type constraint
-- Date: 2026-03-13
-- Problem: FixSignEventTypeConstraint.sql (alphabetically later than
--          AddTenantSkippedEventType.sql) overwrote the constraint and
--          removed 'TenantSkipped', breaking the Admin Skip Tenant action.
-- Fix: Drop and recreate with the complete list of all valid event types.

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

ALTER TABLE [dbo].[SignEvent]
    ADD CONSTRAINT [CK_SignEvent_Type]
    CHECK ([EventType] IN (
        'Sent','Opened','Consented','Signed',
        'Edited','Completed',
        'Voided','Declined','Expired','Downloaded','Reminded',
        'TenantSkipped'
    ));
GO

PRINT 'Migration PatchSignEventType_AddTenantSkipped completed successfully';
GO
