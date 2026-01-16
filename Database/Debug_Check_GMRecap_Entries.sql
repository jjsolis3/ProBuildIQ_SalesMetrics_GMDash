USE SalesMetrics;
GO

-- Check all GM Weekly Recap entries for the current week
PRINT 'GM Weekly Recap Entries for January 12, 2026:';
SELECT
    E.RecapID,
    E.WeekStartDate,
    E.GMUserID,
    U.FirstName + ' ' + U.LastName as GMName,
    E.CreatedDate
FROM GMWeeklyRecapEntry E
LEFT JOIN Users U ON E.GMUserID = U.Users_ID
WHERE E.WeekStartDate = '2026-01-12'
ORDER BY E.RecapID;

PRINT '';
PRINT 'All Field Entries (showing FieldID to verify multiple entries):';
SELECT
    F.FieldID,
    F.RecapID,
    F.FieldName,
    F.FieldValue,
    F.CreatedDate,
    F.ModifiedDate
FROM GMWeeklyRecapField F
WHERE F.RecapID IN (
    SELECT RecapID FROM GMWeeklyRecapEntry
    WHERE WeekStartDate = '2026-01-12'
)
ORDER BY F.RecapID, F.FieldName, F.FieldID;

PRINT '';
PRINT 'Count of entries per field per recap:';
SELECT
    F.RecapID,
    F.FieldName,
    COUNT(*) as EntryCount,
    STRING_AGG(F.FieldValue, ' | ') as AllValues
FROM GMWeeklyRecapField F
WHERE F.RecapID IN (
    SELECT RecapID FROM GMWeeklyRecapEntry
    WHERE WeekStartDate = '2026-01-12'
)
GROUP BY F.RecapID, F.FieldName
ORDER BY F.RecapID, F.FieldName;
GO
