-- Migration: Fix CK_SignEvent_Type constraint
-- Date: 2026-02-19
-- Description: The original CK_SignEvent_Type constraint was missing 'Edited' and 'Completed',
--              which are valid event types recorded by EnvelopeService.  This migration drops
--              the existing constraint and recreates it with the full set of allowed values.

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

-- Recreate with complete list of all event types used by EnvelopeService
ALTER TABLE [dbo].[SignEvent]
    ADD CONSTRAINT [CK_SignEvent_Type]
    CHECK ([EventType] IN (
        'Sent','Opened','Consented','Signed',
        'Edited','Completed',
        'Voided','Declined','Expired','Downloaded','Reminded'
    ));
GO

PRINT 'Migration FixSignEventTypeConstraint completed successfully';
GO
