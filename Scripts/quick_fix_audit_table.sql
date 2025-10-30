-- Simple SQL to add group_id column to sms_audit table
-- Run this in SQL Server Management Studio or any SQL client

-- Add group_id column (nullable)
IF NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.COLUMNS 
               WHERE TABLE_NAME = 'sms_audit' AND COLUMN_NAME = 'group_id')
BEGIN
    ALTER TABLE sms_audit ADD group_id int NULL;
    PRINT 'Added group_id column to sms_audit table';
END
ELSE
BEGIN
    PRINT 'group_id column already exists';
END

-- Add foreign key constraint if groups table exists
IF EXISTS (SELECT * FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'groups')
   AND NOT EXISTS (SELECT * FROM INFORMATION_SCHEMA.REFERENTIAL_CONSTRAINTS 
                   WHERE CONSTRAINT_NAME = 'FK_sms_audit_groups_group_id')
BEGIN
    ALTER TABLE sms_audit 
    ADD CONSTRAINT FK_sms_audit_groups_group_id 
    FOREIGN KEY (group_id) REFERENCES groups(group_id);
    PRINT 'Added foreign key constraint';
END

-- Add index for performance
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_sms_audit_group_id')
BEGIN
    CREATE INDEX IX_sms_audit_group_id ON sms_audit(group_id);
    PRINT 'Added index on group_id column';
END