using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using SCADASMSSystem.Web.Data;
using SCADASMSSystem.Web.Models;
using System.Text.RegularExpressions;

namespace SCADASMSSystem.Web.Services
{
    public class AlarmActionService : IAlarmActionService
    {
        private readonly SCADADbContext _context;
        private readonly IGroupService _groupService;
        private readonly ILogger<AlarmActionService> _logger;
        private readonly SmsSettings _smsSettings;

        // SMS command identifiers
        private const string SMS_SCRIPT_NAME = "Scada_sms.bat";

        public AlarmActionService(
            SCADADbContext context, 
            IGroupService groupService,
            ILogger<AlarmActionService> logger,
            IOptions<SmsSettings> smsSettings)
        {
            _context = context;
            _groupService = groupService;
            _logger = logger;
            _smsSettings = smsSettings.Value;
        }

        public async Task<IEnumerable<AlarmAction>> GetAllAlarmActionsAsync()
        {
            try
            {
                // Execute SQL query - exclude BLOCK_NAME from SELECT since it's NotMapped
                var query = @"
                    SELECT 
                        ac.BLOCK_INDEX,
                        ac.ALARM_ID,
                        ISNULL(ac.DESCRIPTION, '') AS DESCRIPTION,
                        ISNULL(ac.ALARM_CONDITION_NAME, '') AS ALARM_CONDITION_NAME,
                        ac.ACTIONS_ON_ACTIVE,
                        ac.DELETED
                    FROM AlarmConditions ac
                    INNER JOIN DBLIST db ON db.BLOCK_INDEX = ac.BLOCK_INDEX
                    WHERE ac.DELETED = 0
                    ORDER BY db.BLOCK_NAME";

                var alarms = await _context.Set<AlarmAction>()
                    .FromSqlRaw(query)
                    .ToListAsync();

                _logger.LogInformation("Retrieved {Count} alarm actions from SCADA database", alarms.Count);

                // Populate BlockName from DBLIST separately
                var dbListBlocks = await _context.Set<DBListBlock>().ToListAsync();
                
                // Get latest audit records for each block (if audit table exists)
                Dictionary<int, (DateTime modifiedAt, string modifiedBy)> auditLookup = new();
                try
                {
                    var latestAudits = await _context.Set<AlarmActionAudit>()
                        .GroupBy(a => a.BlockId)
                        .Select(g => new {
                            BlockId = g.Key,
                            ModifiedAt = g.Max(x => x.ModifiedAt),
                            ModifiedBy = g.OrderByDescending(x => x.ModifiedAt).First().ModifiedBy
                        })
                        .ToListAsync();

                    auditLookup = latestAudits.ToDictionary(
                        a => a.BlockId,
                        a => (a.ModifiedAt, a.ModifiedBy)
                    );
                    
                    _logger.LogInformation("Loaded audit info for {Count} alarms", auditLookup.Count);
                }
                catch (Exception auditEx)
                {
                    _logger.LogWarning(auditEx, "Could not load audit information - table may not exist yet");
                }
                
                foreach (var alarm in alarms)
                {
                    var block = dbListBlocks.FirstOrDefault(b => b.BlockIndex == alarm.BlockId);
                    if (block != null)
                    {
                        alarm.BlockName = block.BlockName;
                    }
                    
                    // Populate audit information if available
                    if (auditLookup.TryGetValue(alarm.BlockId, out var auditInfo))
                    {
                        alarm.LastModified = auditInfo.modifiedAt;
                        alarm.ModifiedBy = auditInfo.modifiedBy;
                    }
                }

                // Only log first few alarms in debug mode for troubleshooting
                if (_logger.IsEnabled(LogLevel.Debug) && alarms.Any())
                {
                    var sampleCount = Math.Min(3, alarms.Count);
                    _logger.LogDebug("Sample of first {Count} alarms loaded (total: {Total})", sampleCount, alarms.Count);
                    for (int i = 0; i < sampleCount; i++)
                    {
                        var alarm = alarms[i];
                        _logger.LogDebug("  Alarm {BlockId}: BlockName={BlockName}, AlarmId={AlarmId}, HasAction={HasAction}, LastModified={LastModified}", 
                            alarm.BlockId, 
                            alarm.BlockName, 
                            alarm.AlarmId,
                            !string.IsNullOrEmpty(alarm.Action),
                            alarm.LastModified?.ToString("yyyy-MM-dd HH:mm:ss") ?? "Never");
                    }
                }

                return alarms;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving alarm actions from SCADA database");
                return Enumerable.Empty<AlarmAction>();
            }
        }

        public async Task<AlarmAction?> GetAlarmActionByIdAsync(int blockId)
        {
            try
            {
                var alarm = await _context.Set<AlarmAction>()
                    .FirstOrDefaultAsync(a => a.BlockId == blockId && a.Deleted == 0);

                if (alarm != null)
                {
                    // Get block name from DBLIST
                    var block = await _context.Set<DBListBlock>()
                        .FirstOrDefaultAsync(b => b.BlockIndex == blockId);
                    
                    if (block != null)
                    {
                        alarm.BlockName = block.BlockName;
                    }
                }

                return alarm;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving alarm action {BlockId}", blockId);
                return null;
            }
        }

        public async Task<AlarmGroupAssignment?> GetAlarmGroupAssignmentAsync(int blockId)
        {
            try
            {
                var alarm = await GetAlarmActionByIdAsync(blockId);
                if (alarm == null)
                    return null;

                // Try to get last modification info from audit table (may not exist)
                AlarmActionAudit? lastAudit = null;
                try
                {
                    lastAudit = await _context.Set<AlarmActionAudit>()
                        .Where(a => a.BlockId == blockId)
                        .OrderByDescending(a => a.ModifiedAt)
                        .FirstOrDefaultAsync();
                }
                catch (Exception auditEx)
                {
                    _logger.LogWarning(auditEx, "Could not retrieve audit info for block {BlockId} - audit table may not exist", blockId);
                }

                var assignment = new AlarmGroupAssignment
                {
                    BlockId = alarm.BlockId,
                    BlockName = alarm.BlockName,
                    AlarmId = alarm.AlarmId,
                    AlarmDescription = alarm.AlarmDescription,
                    OriginalAction = alarm.Action,
                    AssignedGroupIds = alarm.AssignedGroupIds,
                    IsActive = alarm.IsActive,
                    LastModified = lastAudit?.ModifiedAt,
                    ModifiedBy = lastAudit?.ModifiedBy
                };

                // Load group details
                var allGroups = await _groupService.GetAllGroupsAsync();
                assignment.AssignedGroups = allGroups
                    .Where(g => assignment.AssignedGroupIds.Contains(g.GroupId))
                    .ToList();

                return assignment;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving alarm group assignment for block {BlockId}", blockId);
                return null;
            }
        }

        public async Task<bool> AddGroupToAlarmAsync(int blockId, int groupId, string modifiedBy, string? ipAddress = null)
        {
            // Add a new group to the alarm's existing groups
            _logger.LogInformation("Adding group {GroupId} to alarm block {BlockId}", groupId, blockId);
            
            try
            {
                var alarm = await GetAlarmActionByIdAsync(blockId);
                if (alarm == null)
                {
                    _logger.LogWarning("Alarm block {BlockId} not found", blockId);
                    return false;
                }

                // Get current group IDs and add the new one
                var currentGroupIds = alarm.AssignedGroupIds;
                if (currentGroupIds.Contains(groupId))
                {
                    _logger.LogInformation("Group {GroupId} already assigned to alarm {BlockId}", groupId, blockId);
                    return true; // Already assigned
                }

                var newGroupIds = currentGroupIds.ToList();
                newGroupIds.Add(groupId);

                return await UpdateAlarmGroupsAsync(blockId, newGroupIds, modifiedBy, ipAddress);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error adding group {GroupId} to alarm {BlockId}", groupId, blockId);
                return false;
            }
        }

        public async Task<bool> RemoveGroupFromAlarmAsync(int blockId, int groupId, string modifiedBy, string? ipAddress = null)
        {
            // Remove a specific group from the alarm
            _logger.LogInformation("Removing SMS notification group {GroupId} from alarm block {BlockId}", groupId, blockId);
            
            try
            {
                var alarm = await GetAlarmActionByIdAsync(blockId);
                if (alarm == null)
                {
                    _logger.LogWarning("Alarm block {BlockId} not found", blockId);
                    return false;
                }

                var oldAction = alarm.Action;
                var currentGroupIds = alarm.AssignedGroupIds;

                if (!currentGroupIds.Contains(groupId))
                {
                    _logger.LogInformation("Group {GroupId} not assigned to alarm {BlockId}", groupId, blockId);
                    return true; // Not assigned
                }

                // Remove the group and update
                var newGroupIds = currentGroupIds.Where(id => id != groupId).ToList();

                string newAction;
                if (newGroupIds.Any())
                {
                    // Update with remaining groups
                    newAction = BuildActionCommandWithMultipleGroups(alarm.Action, newGroupIds);
                }
                else
                {
                    // Remove all SMS commands if no groups left
                    newAction = RemoveSmsCommandFromAction(alarm.Action);
                }

                // Update the alarm in AlarmConditions table
                // Don't call Update() since entity is already tracked - just modify the property
                alarm.Action = newAction;
                
                // Mark the Action property as modified explicitly
                var entry = _context.Entry(alarm);
                entry.Property(a => a.Action).IsModified = true;
                
                await _context.SaveChangesAsync();

                // Create audit record
                await CreateAuditRecordAsync(blockId, "REMOVE_GROUP", groupId, null, oldAction, newAction, modifiedBy, ipAddress);

                _logger.LogInformation("Removed SMS group {GroupId} from alarm block {BlockId}", groupId, blockId);

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error removing group {GroupId} from alarm {BlockId}", groupId, blockId);
                return false;
            }
        }

        public async Task<bool> RemoveAllGroupsFromAlarmAsync(int blockId, string modifiedBy, string? ipAddress = null)
        {
            // Remove ALL SMS notification groups from the alarm
            // IMPORTANT: Preserves all non-SMS commands (like WF:OnCamALARM)
            _logger.LogInformation("Removing ALL SMS notification groups from alarm block {BlockId}", blockId);
            
            try
            {
                var alarm = await GetAlarmActionByIdAsync(blockId);
                if (alarm == null)
                {
                    _logger.LogWarning("Alarm block {BlockId} not found", blockId);
                    return false;
                }

                var oldAction = alarm.Action;
                var oldGroupIds = alarm.AssignedGroupIds;

                if (!oldGroupIds.Any())
                {
                    _logger.LogInformation("No SMS groups assigned to alarm {BlockId}", blockId);
                    return true; // Nothing to remove
                }

                // Remove ONLY SMS commands, preserve all other commands
                var newAction = RemoveSmsCommandFromAction(alarm.Action);

                _logger.LogInformation("Removing {Count} SMS group(s) from alarm {BlockId}", oldGroupIds.Count, blockId);
                _logger.LogInformation("OLD Action: {OldAction}", oldAction);
                _logger.LogInformation("NEW Action (SMS removed): {NewAction}", newAction);

                // Update the alarm in AlarmConditions table
                alarm.Action = newAction;
                
                // Mark the Action property as modified explicitly
                var entry = _context.Entry(alarm);
                entry.Property(a => a.Action).IsModified = true;
                
                var changeCount = await _context.SaveChangesAsync();
                _logger.LogInformation("SaveChanges returned: {Count} changes", changeCount);

                // Create audit record for each removed group
                foreach (var oldGroupId in oldGroupIds)
                {
                    await CreateAuditRecordAsync(blockId, "REMOVE_ALL_GROUPS", oldGroupId, null, oldAction, newAction, modifiedBy, ipAddress);
                }

                _logger.LogInformation("Removed all SMS groups from alarm block {BlockId}, preserved non-SMS commands", blockId);

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error removing all groups from alarm {BlockId}", blockId);
                return false;
            }
        }

        public async Task<bool> UpdateAlarmGroupsAsync(int blockId, List<int> groupIds, string modifiedBy, string? ipAddress = null)
        {
            try
            {
                _logger.LogInformation("=== UpdateAlarmGroupsAsync START ===");
                _logger.LogInformation("Block ID: {BlockId}, Group IDs: {GroupIds}", blockId, string.Join(", ", groupIds));

                var alarm = await GetAlarmActionByIdAsync(blockId);
                if (alarm == null)
                {
                    _logger.LogWarning("Alarm block {BlockId} not found in SCADA database", blockId);
                    return false;
                }

                if (!groupIds.Any())
                {
                    _logger.LogError("UpdateAlarmGroupsAsync called with zero groups for block {BlockId}. Use RemoveAllGroupsFromAlarmAsync instead.", blockId);
                    return false;
                }

                var oldAction = alarm.Action;
                var oldGroupIds = alarm.AssignedGroupIds;

                _logger.LogInformation("OLD Action: {OldAction}", oldAction);
                _logger.LogInformation("OLD Groups: {OldGroups}", string.Join(", ", oldGroupIds));

                // Build new action: preserve non-SMS commands, update/add SMS commands for all groups
                // CRITICAL: Non-SMS commands (like WF:OnCamALARM) are NEVER modified
                var newAction = BuildActionCommandWithMultipleGroups(alarm.Action, groupIds);

                _logger.LogInformation("NEW Action: {NewAction}", newAction);
                _logger.LogInformation("Non-SMS commands preserved: YES");

                // Update the alarm in AlarmConditions table
                // Don't call Update() since entity is already tracked - just modify the property
                alarm.Action = newAction;
                
                // Mark the Action property as modified explicitly
                var entry = _context.Entry(alarm);
                entry.Property(a => a.Action).IsModified = true;
                
                _logger.LogInformation("Entity State: {State}", entry.State);
                _logger.LogInformation("Action Modified: {Modified}", entry.Property(a => a.Action).IsModified);

                var changeCount = await _context.SaveChangesAsync();
                
                _logger.LogInformation("SaveChanges returned: {Count} changes", changeCount);

                // Create audit record for each group change
                foreach (var groupId in groupIds)
                {
                    if (!oldGroupIds.Contains(groupId))
                    {
                        await CreateAuditRecordAsync(blockId, "ADD_GROUP", null, groupId, oldAction, newAction, modifiedBy, ipAddress);
                    }
                }
                
                foreach (var oldGroupId in oldGroupIds)
                {
                    if (!groupIds.Contains(oldGroupId))
                    {
                        await CreateAuditRecordAsync(blockId, "REMOVE_GROUP", oldGroupId, null, oldAction, newAction, modifiedBy, ipAddress);
                    }
                }

                _logger.LogInformation("Updated alarm block {BlockId} ({BlockName}) with {Count} group(s): {Groups}", 
                    blockId, alarm.BlockName, groupIds.Count, string.Join(", ", groupIds));

                _logger.LogInformation("=== UpdateAlarmGroupsAsync END (SUCCESS) ===");
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating groups for alarm {BlockId} in SCADA database", blockId);
                _logger.LogInformation("=== UpdateAlarmGroupsAsync END (FAILED) ===");
                return false;
            }
        }

        public string BuildActionCommand(string baseCommand, List<int> groupIds)
        {
            return BuildActionCommandWithMultipleGroups(baseCommand, groupIds);
        }

        /// <summary>
        /// Builds action command with multiple SMS commands (one per group).
        /// CRITICAL: Preserves ALL non-SMS commands (like WF:OnCamALARM, etc.)
        /// Only adds/updates SMS notification commands (Scada_sms.bat)
        /// </summary>
        private string BuildActionCommandWithMultipleGroups(string? action, List<int> groupIds)
        {
            if (!groupIds.Any())
            {
                return RemoveSmsCommandFromAction(action);
            }

            if (string.IsNullOrWhiteSpace(action))
            {
                // No commands exist, create SMS commands for all groups
                _logger.LogDebug("No existing commands, creating {Count} new SMS commands", groupIds.Count);
                return string.Join(" ; ", groupIds.Select(BuildSmsCommandForGroupId));
            }

            // Split commands and preserve non-SMS commands
            var commands = SplitCommands(action);
            var nonSmsCommands = commands.Where(cmd => !IsSmsCommand(cmd)).ToList();

            // Build new SMS commands for all groups
            var smsCommands = groupIds.Select(BuildSmsCommandForGroupId).ToList();

            // Combine: non-SMS commands first, then SMS commands
            var allCommands = new List<string>();
            allCommands.AddRange(nonSmsCommands);
            allCommands.AddRange(smsCommands);

            _logger.LogDebug("Built action with {SmsCount} SMS command(s) and {OtherCount} preserved non-SMS command(s)", 
                smsCommands.Count, nonSmsCommands.Count);

            if (nonSmsCommands.Any())
            {
                _logger.LogInformation("PRESERVED {Count} non-SMS commands: {Commands}", 
                    nonSmsCommands.Count, 
                    string.Join(" | ", nonSmsCommands.Select(c => c.Substring(0, Math.Min(30, c.Length)))));
            }

            // Join with semicolon and spaces (SCADA standard format)
            return string.Join(" ; ", allCommands);
        }

        /// <summary>
        /// Updates ONLY the SMS command in the action string, preserving all other commands.
        /// CRITICAL: Non-SMS commands are NEVER touched or modified.
        /// If no SMS command exists, adds it at the end.
        /// </summary>
        private string UpdateSmsCommandInAction(string? action, int groupId)
        {
            if (string.IsNullOrWhiteSpace(action))
            {
                // No commands exist, create SMS command only
                return BuildSmsCommandForGroupId(groupId);
            }

            // Split commands
            var commands = SplitCommands(action);
            var updatedCommands = new List<string>();
            bool smsCommandFound = false;

            foreach (var command in commands)
            {
                if (IsSmsCommand(command))
                {
                    // Replace SMS command with new group ID
                    updatedCommands.Add(BuildSmsCommandForGroupId(groupId));
                    smsCommandFound = true;
                    _logger.LogDebug("Replaced existing SMS command with group {GroupId}", groupId);
                }
                else
                {
                    // Preserve non-SMS command as-is (NEVER MODIFIED)
                    updatedCommands.Add(command.Trim());
                    _logger.LogDebug("PRESERVED non-SMS command: {Command}", command.Substring(0, Math.Min(50, command.Length)));
                }
            }

            // If no SMS command was found, add it
            if (!smsCommandFound)
            {
                updatedCommands.Add(BuildSmsCommandForGroupId(groupId));
                _logger.LogDebug("Added new SMS command with group {GroupId}", groupId);
            }

            // Reconstruct action string with semicolon and space separators
            return string.Join(" ; ", updatedCommands);
        }

        /// <summary>
        /// Removes ONLY the SMS command from the action string, preserving all other commands.
        /// CRITICAL: Non-SMS commands (like WF:OnCamALARM) are NEVER removed or modified.
        /// This is used when the user wants to disable SMS notifications but keep other alarm actions.
        /// </summary>
        private string RemoveSmsCommandFromAction(string? action)
        {
            if (string.IsNullOrWhiteSpace(action))
                return string.Empty;

            var commands = SplitCommands(action);
            var smsCommands = commands.Where(cmd => IsSmsCommand(cmd)).ToList();
            var preservedCommands = commands.Where(cmd => !IsSmsCommand(cmd)).ToList();

            _logger.LogDebug("Found {SmsCount} SMS command(s) to remove and {OtherCount} non-SMS command(s) to preserve", 
                smsCommands.Count, preservedCommands.Count);

            if (preservedCommands.Any())
            {
                _logger.LogInformation("PRESERVED {Count} non-SMS command(s): {Commands}", 
                    preservedCommands.Count,
                    string.Join(" | ", preservedCommands.Select(c => c.Substring(0, Math.Min(30, c.Length)))));
                return string.Join(" ; ", preservedCommands);
            }

            _logger.LogDebug("No non-SMS commands to preserve, action will be empty");
            return string.Empty;
        }

        /// <summary>
        /// Splits the action string into individual commands.
        /// Handles semicolon separators (SCADA standard format).
        /// </summary>
        private List<string> SplitCommands(string action)
        {
            if (string.IsNullOrWhiteSpace(action))
                return new List<string>();

            // Split by semicolon (SCADA standard separator)
            var commands = action.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(cmd => cmd.Trim())
                .Where(cmd => !string.IsNullOrWhiteSpace(cmd))
                .ToList();

            return commands;
        }

        /// <summary>
        /// Checks if a command is the SMS notification command (Scada_sms.bat).
        /// </summary>
        private bool IsSmsCommand(string command)
        {
            if (string.IsNullOrWhiteSpace(command))
                return false;

            // Check if command contains the SMS script name
            return command.Contains(SMS_SCRIPT_NAME, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Builds the SMS command for a specific group ID using the correct SCADA syntax.
        /// Syntax: 'Run '+getalias('PCIMUTIL')+'Scada_sms.bat'+' "+'"+GetValue(PFWALARMNG|PCIM!{PCIM_ID}:(ThisObject).DESCRIPTION)+'"'+{GROUP_ID}+' "+GetValue(PFWALARMNG|PCIM!{PCIM_ID}:(ThisObject).ALM_VALUE)+'"'
        /// </summary>
        private string BuildSmsCommandForGroupId(int groupId)
        {
            if (groupId <= 0)
                return string.Empty;

            var pcimId = _smsSettings.ScadaPcimObjectId;

            // Correct SCADA syntax with proper quote escaping
            // 'Run '+getalias('PCIMUTIL')+'Scada_sms.bat'+' "+'"+GetValue(PFWALARMNG|PCIM!198:(ThisObject).DESCRIPTION)+'"'+31+' "+GetValue(PFWALARMNG|PCIM!198:(ThisObject).ALM_VALUE)+'"'
            return $"'Run '+getalias('PCIMUTIL')+'Scada_sms.bat'+' \"+'\" +GetValue(PFWALARMNG|PCIM!{pcimId}:(ThisObject).DESCRIPTION)+'\"'+{groupId}+' \"+GetValue(PFWALARMNG|PCIM!{pcimId}:(ThisObject).ALM_VALUE)+'\"'";
        }

        public List<int> ParseGroupIdsFromAction(string? action)
        {
            var groupId = ParseGroupIdFromAction(action);
            return groupId > 0 ? new List<int> { groupId } : new List<int>();
        }

        /// <summary>
        /// Parses the group ID from ONLY the SMS command, ignoring other commands.
        /// </summary>
        private int ParseGroupIdFromAction(string? action)
        {
            if (string.IsNullOrWhiteSpace(action))
                return 0;

            // Find the SMS command
            var commands = SplitCommands(action);
            var smsCommand = commands.FirstOrDefault(cmd => IsSmsCommand(cmd));

            if (string.IsNullOrWhiteSpace(smsCommand))
            {
                _logger.LogDebug("No SMS command found in action");
                return 0;
            }

            // Pattern to match the group ID in the SMS command
            // Looking for: +" "+<NUMBER>+" "+
            var pattern = @"\+\s*""\s*""\s*\+\s*(\d+)\s*\+\s*""\s*""";
            var match = Regex.Match(smsCommand, pattern);
            
            if (match.Success && int.TryParse(match.Groups[1].Value, out int groupId))
            {
                _logger.LogDebug("Parsed group ID {GroupId} from SMS command", groupId);
                return groupId;
            }

            // Fallback: try simpler pattern
            var fallbackPattern = @"""\s*(\d+)\s*\+\s*""";
            var fallbackMatch = Regex.Match(smsCommand, fallbackPattern);
            
            if (fallbackMatch.Success && int.TryParse(fallbackMatch.Groups[1].Value, out int fallbackGroupId))
            {
                _logger.LogDebug("Parsed group ID {GroupId} from SMS command (fallback pattern)", fallbackGroupId);
                return fallbackGroupId;
            }

            _logger.LogWarning("Could not parse group ID from SMS command: {Command}", smsCommand.Substring(0, Math.Min(100, smsCommand.Length)));
            return 0;
        }

        private async Task CreateAuditRecordAsync(
            int blockId, 
            string actionType, 
            int? oldGroupId,
            int? newGroupId,
            string? oldAction, 
            string? newAction, 
            string modifiedBy, 
            string? ipAddress)
        {
            try
            {
                // Store audit in OUR database, not in WinCC OA database
                var audit = new AlarmActionAudit
                {
                    BlockId = blockId,
                    ActionType = actionType,
                    OldGroupId = oldGroupId,
                    NewGroupId = newGroupId,
                    OldAction = oldAction,
                    NewAction = newAction,
                    ModifiedBy = modifiedBy,
                    ModifiedAt = DateTime.Now,
                    IpAddress = ipAddress
                };

                _context.Set<AlarmActionAudit>().Add(audit);
                await _context.SaveChangesAsync();

                _logger.LogInformation("Created audit record for {ActionType} on block {BlockId}: {OldGroupId} -> {NewGroupId}", 
                    actionType, blockId, oldGroupId, newGroupId);
            }
            catch (Exception ex)
            {
                // Don't fail the main operation if audit fails
                _logger.LogError(ex, "Error creating audit record for block {BlockId}", blockId);
            }
        }
    }
}
