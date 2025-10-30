-- Add group_id column to existing sms_audit table
-- This script adds the missing group_id column without affecting existing data

USE [bat_yam]
GO

-- Check if group_id column already exists
IF NOT EXISTS (SELECT * FROM sys.columns 
               WHERE object_id = OBJECT_ID(N'[dbo].[sms_audit]') 
               AND name = 'group_id')
BEGIN
    -- Add the group_id column
    ALTER TABLE [dbo].[sms_audit] 
    ADD [group_id] int NULL;
    
    -- Add foreign key constraint to groups table
    IF EXISTS (SELECT * FROM sys.tables WHERE name = 'groups')
    BEGIN
        ALTER TABLE [dbo].[sms_audit]  
        ADD CONSTRAINT [FK_sms_audit_groups_group_id] 
        FOREIGN KEY([group_id]) REFERENCES [dbo].[groups]([group_id]);
    END
    
    -- Create index on group_id for performance
    CREATE INDEX [IX_sms_audit_group_id] ON [dbo].[sms_audit]([group_id]);
    
    PRINT 'Successfully added group_id column to sms_audit table';
END
ELSE
BEGIN
    PRINT 'group_id column already exists in sms_audit table';
END
GO