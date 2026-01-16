USE SalesMetrics;
GO

-- ============================================================
-- MIGRATION: Allow Multiple Entries for Same Field in GM Recap
-- ============================================================
-- Problem: PRIMARY KEY on (RecapID, FieldName) prevents
--          multiple submissions for the same field
-- Solution: Add FieldID as auto-incrementing primary key
-- ============================================================

BEGIN TRANSACTION;

-- Step 1: Drop the existing PRIMARY KEY constraint
DECLARE @ConstraintName NVARCHAR(200);
SELECT @ConstraintName = CONSTRAINT_NAME
FROM INFORMATION_SCHEMA.TABLE_CONSTRAINTS
WHERE TABLE_NAME = 'GMWeeklyRecapField' AND CONSTRAINT_TYPE = 'PRIMARY KEY';

IF @ConstraintName IS NOT NULL
BEGIN
    EXEC('ALTER TABLE GMWeeklyRecapField DROP CONSTRAINT [' + @ConstraintName + ']');
    PRINT 'Dropped old PRIMARY KEY constraint: ' + @ConstraintName;
END

-- Step 2: Add new FieldID column as auto-incrementing primary key
IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.COLUMNS
               WHERE TABLE_NAME = 'GMWeeklyRecapField' AND COLUMN_NAME = 'FieldID')
BEGIN
    ALTER TABLE GMWeeklyRecapField
    ADD FieldID INT IDENTITY(1,1) NOT NULL;

    PRINT 'Added FieldID column';
END

-- Step 3: Set FieldID as the new PRIMARY KEY
ALTER TABLE GMWeeklyRecapField
ADD CONSTRAINT PK_GMWeeklyRecapField PRIMARY KEY (FieldID);

PRINT 'Created new PRIMARY KEY on FieldID';

-- Step 4: Create an index on RecapID for performance
IF NOT EXISTS (SELECT * FROM sys.indexes
               WHERE name = 'IX_GMWeeklyRecapField_RecapID'
               AND object_id = OBJECT_ID('GMWeeklyRecapField'))
BEGIN
    CREATE NONCLUSTERED INDEX IX_GMWeeklyRecapField_RecapID
    ON GMWeeklyRecapField (RecapID);

    PRINT 'Created index on RecapID';
END

COMMIT TRANSACTION;

PRINT 'Table structure updated successfully!';
PRINT 'You can now submit multiple entries for the same field.';

-- Verify the changes
SELECT
    COLUMN_NAME,
    DATA_TYPE,
    IS_NULLABLE,
    COLUMNPROPERTY(OBJECT_ID('dbo.GMWeeklyRecapField'), COLUMN_NAME, 'IsIdentity') as IS_IDENTITY
FROM INFORMATION_SCHEMA.COLUMNS
WHERE TABLE_NAME = 'GMWeeklyRecapField'
ORDER BY ORDINAL_POSITION;
GO
