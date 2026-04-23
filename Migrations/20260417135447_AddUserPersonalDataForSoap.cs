using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SCADASMSSystem.Web.Migrations
{
    /// <inheritdoc />
    public partial class AddUserPersonalDataForSoap : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Add personal data columns to users table (idempotent — skips if columns already exist)
            migrationBuilder.Sql(@"
                IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'users' AND COLUMN_NAME = 'first_name')
                    ALTER TABLE [users] ADD [first_name] nvarchar(100) NULL;
                IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'users' AND COLUMN_NAME = 'last_name')
                    ALTER TABLE [users] ADD [last_name] nvarchar(100) NULL;
                IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'users' AND COLUMN_NAME = 'tz')
                    ALTER TABLE [users] ADD [tz] nvarchar(20) NULL;
                IF EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'users' AND COLUMN_NAME = 'email' AND IS_NULLABLE = 'NO')
                BEGIN
                    ALTER TABLE [users] ALTER COLUMN [email] nvarchar(255) NULL;
                END
            ");

            // Create alarm_action_audit table (idempotent)
            migrationBuilder.Sql(@"
                IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'alarm_action_audit')
                BEGIN
                    CREATE TABLE [alarm_action_audit] (
                        [audit_id]     int            NOT NULL IDENTITY(1,1),
                        [block_index]  int            NOT NULL,
                        [action_type]  nvarchar(50)   NOT NULL,
                        [old_group_id] int            NULL,
                        [new_group_id] int            NULL,
                        [old_action]   nvarchar(max)  NULL,
                        [new_action]   nvarchar(max)  NULL,
                        [modified_by]  nvarchar(100)  NOT NULL,
                        [modified_at]  datetime2      NOT NULL DEFAULT GETDATE(),
                        [ip_address]   nvarchar(50)   NULL,
                        CONSTRAINT [PK_alarm_action_audit] PRIMARY KEY ([audit_id])
                    );
                    CREATE INDEX [IX_alarm_action_audit_action_type]  ON [alarm_action_audit] ([action_type]);
                    CREATE INDEX [IX_alarm_action_audit_block_index]  ON [alarm_action_audit] ([block_index]);
                    CREATE INDEX [IX_alarm_action_audit_modified_at]  ON [alarm_action_audit] ([modified_at]);
                END
            ");

            // AlarmConditions — SCADA native table, create only if absent (do NOT alter existing production table)
            migrationBuilder.Sql(@"
                IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'AlarmConditions')
                BEGIN
                    CREATE TABLE [AlarmConditions] (
                        [BLOCK_INDEX]           int           NOT NULL IDENTITY(1,1),
                        [ALARM_ID]              int           NULL,
                        [DESCRIPTION]           nvarchar(max) NULL,
                        [ALARM_CONDITION_NAME]  nvarchar(max) NULL,
                        [ACTIONS_ON_ACTIVE]     nvarchar(max) NULL,
                        [DELETED]               int           NOT NULL,
                        CONSTRAINT [PK_AlarmConditions] PRIMARY KEY ([BLOCK_INDEX])
                    );
                    IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_AlarmConditions_ALARM_ID')
                        CREATE INDEX [IX_AlarmConditions_ALARM_ID]  ON [AlarmConditions] ([ALARM_ID]);
                    IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_AlarmConditions_DELETED')
                        CREATE INDEX [IX_AlarmConditions_DELETED]   ON [AlarmConditions] ([DELETED]);
                END
            ");

            // DBLIST — SCADA native table, create only if absent
            migrationBuilder.Sql(@"
                IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'DBLIST')
                BEGIN
                    CREATE TABLE [DBLIST] (
                        [BLOCK_INDEX] int            NOT NULL IDENTITY(1,1),
                        [BLOCK_NAME]  nvarchar(450)  NOT NULL,
                        CONSTRAINT [PK_DBLIST] PRIMARY KEY ([BLOCK_INDEX])
                    );
                    IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_DBLIST_BLOCK_NAME')
                        CREATE INDEX [IX_DBLIST_BLOCK_NAME] ON [DBLIST] ([BLOCK_NAME]);
                END
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                IF EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'alarm_action_audit')
                    DROP TABLE [alarm_action_audit];
            ");

            migrationBuilder.Sql(@"
                IF EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'users' AND COLUMN_NAME = 'first_name')
                    ALTER TABLE [users] DROP COLUMN [first_name];
                IF EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'users' AND COLUMN_NAME = 'last_name')
                    ALTER TABLE [users] DROP COLUMN [last_name];
                IF EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = 'users' AND COLUMN_NAME = 'tz')
                    ALTER TABLE [users] DROP COLUMN [tz];
            ");
        }
    }
}
