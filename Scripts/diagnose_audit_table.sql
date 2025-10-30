-- Check if audit table exists and has the expected structure
SELECT 
    TABLE_NAME,
    COLUMN_NAME,
    DATA_TYPE,
    IS_NULLABLE,
    COLUMN_DEFAULT
FROM INFORMATION_SCHEMA.COLUMNS 
WHERE TABLE_NAME = 'SmsAudits'
ORDER BY ORDINAL_POSITION;

-- Check if any audit records exist
SELECT COUNT(*) AS TotalAuditRecords FROM SmsAudits;

-- Check recent audit records (if any)
SELECT TOP 10 
    AlarmId,
    UserId, 
    PhoneNumber,
    AlarmDescription,
    Status,
    MessageStatus,
    CreatedAt
FROM SmsAudits 
ORDER BY CreatedAt DESC;

-- Check if there are any failed inserts or constraint issues
SELECT 
    TABLE_NAME,
    CONSTRAINT_NAME,
    CONSTRAINT_TYPE
FROM INFORMATION_SCHEMA.TABLE_CONSTRAINTS
WHERE TABLE_NAME = 'SmsAudits';

-- Check for any database errors
SELECT TOP 10
    error_number,
    error_message, 
    error_procedure,
    error_line
FROM sys.messages
WHERE language_id = 1033
AND error_number IN (2, 8152, 515, 547)  -- Common insert errors
ORDER BY error_number;