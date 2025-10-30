using Microsoft.EntityFrameworkCore;
using SCADASMSSystem.Web.Data;

namespace SCADASMSSystem.Web.Services
{
    /// <summary>
    /// Service for intelligent database initialization that checks and creates tables individually.
    /// Ensures database exists first, then creates only missing tables.
    /// </summary>
    public class DatabaseInitializationService
    {
        private readonly SCADADbContext _context;
        private readonly ILogger<DatabaseInitializationService> _logger;

        public DatabaseInitializationService(
            SCADADbContext context,
            ILogger<DatabaseInitializationService> logger)
        {
            _context = context;
            _logger = logger;
        }

        /// <summary>
        /// Initializes the database with intelligent table-by-table checking.
        /// Flow:
        /// 1. Check if database exists -> If NO, throw exception
        /// 2. Check each table individually
        /// 3. Create only missing tables
        /// </summary>
        public async Task<DatabaseInitializationResult> InitializeAsync()
        {
            var result = new DatabaseInitializationResult();

            try
            {
                _logger.LogInformation("=== Starting Database Initialization ===");

                // STEP 1: Check if database exists
                _logger.LogInformation("Step 1: Checking database connectivity...");
                var canConnect = await _context.Database.CanConnectAsync();

                if (!canConnect)
                {
                    var errorMessage = "Database does not exist or cannot be reached. Please create the database first.";
                    _logger.LogError(errorMessage);
                    result.Success = false;
                    result.ErrorMessage = errorMessage;
                    throw new InvalidOperationException(errorMessage);
                }

                _logger.LogInformation("? Database exists and is reachable");
                result.DatabaseExists = true;

                // STEP 2: Check and create each table individually
                _logger.LogInformation("Step 2: Checking and creating missing tables...");

                // Check our application tables (not WinCC OA tables)
                var tablesToCheck = new Dictionary<string, string>
                {
                    { "users", "Application users table" },
                    { "groups", "SMS notification groups table" },
                    { "group_members", "Group membership table" },
                    { "sms_audit", "SMS audit/history table" },
                    { "date_dimension", "Date dimension for holidays" },
                    { "alarm_action_audit", "Alarm action audit table (NEW)" }
                };

                foreach (var table in tablesToCheck)
                {
                    var exists = await TableExistsAsync(table.Key);
                    result.TableStatus[table.Key] = exists;

                    if (exists)
                    {
                        _logger.LogInformation("  ? Table '{TableName}' exists - {Description}", table.Key, table.Value);
                    }
                    else
                    {
                        _logger.LogWarning("  ? Table '{TableName}' is missing - {Description}", table.Key, table.Value);
                        result.MissingTables.Add(table.Key);
                    }
                }

                // STEP 3: Create missing tables if any
                if (result.MissingTables.Any())
                {
                    _logger.LogInformation("Step 3: Creating {Count} missing table(s)...", result.MissingTables.Count);

                    foreach (var tableName in result.MissingTables)
                    {
                        await CreateTableAsync(tableName);
                        result.CreatedTables.Add(tableName);
                        _logger.LogInformation("  ? Created table '{TableName}'", tableName);
                    }
                }
                else
                {
                    _logger.LogInformation("Step 3: All tables exist - no action needed");
                }

                // STEP 4: Verify WinCC OA tables (read-only check)
                _logger.LogInformation("Step 4: Verifying WinCC OA SCADA tables (read-only)...");
                
                var scadaTables = new Dictionary<string, string>
                {
                    { "AlarmConditions", "WinCC OA alarm configuration" },
                    { "DBLIST", "WinCC OA block metadata" }
                };

                foreach (var table in scadaTables)
                {
                    var exists = await TableExistsAsync(table.Key);
                    result.ScadaTableStatus[table.Key] = exists;

                    if (exists)
                    {
                        _logger.LogInformation("  ? SCADA table '{TableName}' exists - {Description}", table.Key, table.Value);
                    }
                    else
                    {
                        _logger.LogWarning("  ? SCADA table '{TableName}' not found - {Description}", table.Key, table.Value);
                        _logger.LogWarning("    Note: WinCC OA tables must be created by SCADA system");
                    }
                }

                _logger.LogInformation("=== Database Initialization Complete ===");
                _logger.LogInformation("Summary:");
                _logger.LogInformation("  - Database: ? Exists");
                _logger.LogInformation("  - Tables checked: {Total}", tablesToCheck.Count);
                _logger.LogInformation("  - Tables created: {Created}", result.CreatedTables.Count);
                _logger.LogInformation("  - SCADA tables found: {ScadaCount}/{ScadaTotal}", 
                    result.ScadaTableStatus.Count(x => x.Value), scadaTables.Count);

                result.Success = true;
                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Critical error during database initialization");
                result.Success = false;
                result.ErrorMessage = ex.Message;
                throw;
            }
        }

        /// <summary>
        /// Checks if a specific table exists in the database.
        /// </summary>
        private async Task<bool> TableExistsAsync(string tableName)
        {
            try
            {
                // Use direct SQL query with proper column alias
                var sql = $@"
                    SELECT CASE 
                        WHEN EXISTS (
                            SELECT 1 
                            FROM INFORMATION_SCHEMA.TABLES 
                            WHERE TABLE_NAME = '{tableName}'
                        ) 
                        THEN 1 
                        ELSE 0 
                    END AS TableExists";

                var connection = _context.Database.GetDbConnection();
                await _context.Database.OpenConnectionAsync();

                using var command = connection.CreateCommand();
                command.CommandText = sql;
                var result = await command.ExecuteScalarAsync();

                return Convert.ToInt32(result) == 1;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking if table '{TableName}' exists", tableName);
                return false;
            }
        }

        /// <summary>
        /// Creates a specific table based on the model definition.
        /// </summary>
        private async Task CreateTableAsync(string tableName)
        {
            try
            {
                string createTableSql = tableName switch
                {
                    "users" => GetCreateUsersTableSql(),
                    "groups" => GetCreateGroupsTableSql(),
                    "group_members" => GetCreateGroupMembersTableSql(),
                    "sms_audit" => GetCreateSmsAuditTableSql(),
                    "date_dimension" => GetCreateDateDimensionTableSql(),
                    "alarm_action_audit" => GetCreateAlarmActionAuditTableSql(),
                    _ => throw new ArgumentException($"Unknown table: {tableName}")
                };

                await _context.Database.ExecuteSqlRawAsync(createTableSql);
                _logger.LogInformation("Successfully created table '{TableName}'", tableName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating table '{TableName}'", tableName);
                throw;
            }
        }

        #region Table Creation SQL Scripts

        private string GetCreateUsersTableSql() => @"
            CREATE TABLE users (
                user_id INT PRIMARY KEY IDENTITY(1,1),
                phone_number NVARCHAR(20) NOT NULL,
                user_name NVARCHAR(255) NOT NULL,
                email NVARCHAR(255) NULL,
                sms_enabled BIT NOT NULL DEFAULT 1,
                special_days_enabled BIT NOT NULL DEFAULT 0,
                created_at DATETIME NOT NULL DEFAULT GETDATE()
            );
            CREATE INDEX IX_users_phone_number ON users(phone_number);
            CREATE INDEX IX_users_sms_enabled ON users(sms_enabled);
            CREATE INDEX IX_users_special_days_enabled ON users(special_days_enabled);
        ";

        private string GetCreateGroupsTableSql() => @"
            CREATE TABLE groups (
                group_id INT PRIMARY KEY IDENTITY(1,1),
                group_name NVARCHAR(255) NOT NULL,
                created_at DATETIME NOT NULL DEFAULT GETDATE()
            );
        ";

        private string GetCreateGroupMembersTableSql() => @"
            CREATE TABLE group_members (
                group_member_id INT PRIMARY KEY IDENTITY(1,1),
                group_id INT NOT NULL,
                user_id INT NOT NULL,
                created_at DATETIME NOT NULL DEFAULT GETDATE(),
                CONSTRAINT FK_group_members_groups FOREIGN KEY (group_id) REFERENCES groups(group_id) ON DELETE CASCADE,
                CONSTRAINT FK_group_members_users FOREIGN KEY (user_id) REFERENCES users(user_id) ON DELETE CASCADE
            );
            CREATE UNIQUE INDEX IX_group_members_group_id_user_id ON group_members(group_id, user_id);
        ";

        private string GetCreateSmsAuditTableSql() => @"
            CREATE TABLE sms_audit (
                audit_id INT PRIMARY KEY IDENTITY(1,1),
                alarm_id NVARCHAR(100) NOT NULL,
                user_id INT NOT NULL,
                group_id INT NULL,
                phone_number NVARCHAR(100) NULL,
                alarm_description NVARCHAR(MAX) NOT NULL,
                status NVARCHAR(200) NOT NULL,
                message_status NVARCHAR(200) NULL,
                api_response NVARCHAR(MAX) NULL,
                created_at DATETIME NOT NULL DEFAULT GETDATE(),
                CONSTRAINT FK_sms_audit_users FOREIGN KEY (user_id) REFERENCES users(user_id)
            );
            CREATE INDEX IX_sms_audit_alarm_id ON sms_audit(alarm_id);
            CREATE INDEX IX_sms_audit_created_at ON sms_audit(created_at);
            CREATE INDEX IX_sms_audit_user_id ON sms_audit(user_id);
        ";

        private string GetCreateDateDimensionTableSql() => @"
            CREATE TABLE date_dimension (
                date_id INT PRIMARY KEY IDENTITY(1,1),
                full_date DATE NOT NULL,
                day_of_week TINYINT NOT NULL,
                day_name NVARCHAR(10) NOT NULL,
                day_of_month TINYINT NOT NULL,
                day_of_year SMALLINT NOT NULL,
                week_of_year TINYINT NOT NULL,
                month TINYINT NOT NULL,
                month_name NVARCHAR(10) NOT NULL,
                quarter TINYINT NOT NULL,
                year SMALLINT NOT NULL,
                is_weekend BIT NOT NULL,
                hebrew_date NVARCHAR(50) NULL,
                jewish_holiday NVARCHAR(100) NULL,
                is_jewish_holiday BIT NOT NULL DEFAULT 0,
                is_sabbatical_holiday BIT NOT NULL DEFAULT 0
            );
            CREATE UNIQUE INDEX IX_date_dimension_full_date ON date_dimension(full_date);
            CREATE INDEX IX_date_dimension_is_sabbatical_holiday ON date_dimension(is_sabbatical_holiday);
        ";

        private string GetCreateAlarmActionAuditTableSql() => @"
            IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'alarm_action_audit')
            BEGIN
                CREATE TABLE alarm_action_audit (
                    audit_id INT PRIMARY KEY IDENTITY(1,1),
                    block_index INT NOT NULL,
                    action_type NVARCHAR(50) NOT NULL,
                    old_group_id INT NULL,
                    new_group_id INT NULL,
                    old_action NVARCHAR(MAX) NULL,
                    new_action NVARCHAR(MAX) NULL,
                    modified_by NVARCHAR(100) NOT NULL,
                    modified_at DATETIME NOT NULL DEFAULT GETDATE(),
                    ip_address NVARCHAR(50) NULL
                );
                CREATE INDEX IX_alarm_action_audit_block_index ON alarm_action_audit(block_index);
                CREATE INDEX IX_alarm_action_audit_action_type ON alarm_action_audit(action_type);
                CREATE INDEX IX_alarm_action_audit_modified_at ON alarm_action_audit(modified_at);
            END
        ";

        #endregion
    }

    /// <summary>
    /// Result of database initialization operation.
    /// </summary>
    public class DatabaseInitializationResult
    {
        public bool Success { get; set; }
        public bool DatabaseExists { get; set; }
        public string? ErrorMessage { get; set; }
        public Dictionary<string, bool> TableStatus { get; set; } = new();
        public Dictionary<string, bool> ScadaTableStatus { get; set; } = new();
        public List<string> MissingTables { get; set; } = new();
        public List<string> CreatedTables { get; set; } = new();
    }
}
