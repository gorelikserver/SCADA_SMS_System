using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.RegularExpressions;

namespace SCADASMSSystem.Web.Models
{
    /// <summary>
    /// Represents a SCADA alarm action configuration from the database.
    /// Maps to AlarmConditions table with DBLIST join for block information.
    /// This model is used to manage SMS notification group assignments for alarm actions.
    /// </summary>
    [Table("AlarmConditions")]
    public class AlarmAction
    {
        /// <summary>
        /// Block Index - Primary key from SCADA database
        /// </summary>
        [Key]
        [Column("BLOCK_INDEX")]
        public int BlockId { get; set; }

        /// <summary>
        /// Block name from DBLIST table (via join) - Not mapped to AlarmConditions
        /// </summary>
        [NotMapped]
        public string BlockName { get; set; } = string.Empty;

        /// <summary>
        /// Alarm ID from AlarmConditions - May be INT or VARCHAR in database
        /// </summary>
        [Column("ALARM_ID")]
        public int? AlarmIdInt { get; set; }

        /// <summary>
        /// Alarm ID as string for display - with private setter for EF Core
        /// </summary>
        [NotMapped]
        public string? AlarmId 
        { 
            get => AlarmIdInt?.ToString();
            private set { } // EF Core requires a setter even if not used
        }

        /// <summary>
        /// Alarm description - TEXT or NVARCHAR(MAX) in database
        /// </summary>
        [Column("DESCRIPTION")]
        public string? AlarmDescription { get; set; }

        /// <summary>
        /// Alarm condition name (not displayed to user)
        /// </summary>
        [Column("ALARM_CONDITION_NAME")]
        public string? AlarmConditionName { get; set; }

        /// <summary>
        /// Action command string - SCADA format on alarm active
        /// May be TEXT or NVARCHAR(MAX) in database
        /// Example: Run '+getalias(PCIMUTIL)+"Scada_sms.bat"..." "+22+" "+...
        /// Can contain multiple group assignments (one SMS command per group)
        /// </summary>
        [Column("ACTIONS_ON_ACTIVE")]
        public string? Action { get; set; }

        /// <summary>
        /// Deletion flag - May be INT, SMALLINT, or TINYINT in database (0=active, 1=deleted)
        /// </summary>
        [Column("DELETED")]
        public int Deleted { get; set; }

        /// <summary>
        /// Whether the alarm is active (DELETED = 0)
        /// </summary>
        [NotMapped]
        public bool IsActive => Deleted == 0;

        /// <summary>
        /// Last modification timestamp - tracked in our audit table
        /// </summary>
        [NotMapped]
        public DateTime? LastModified { get; set; }

        /// <summary>
        /// Last modified by user - tracked in our audit table
        /// </summary>
        [NotMapped]
        public string? ModifiedBy { get; set; }

        /// <summary>
        /// Parse all group IDs from the action command string.
        /// Each SMS command contains one group ID.
        /// NEW FORMAT: 'Run '+getalias('PCIMUTIL')+'Scada_sms.bat'+' "+'"+GetValue(PFWALARMNG|PCIM!198:(ThisObject).DESCRIPTION)+'"'+31+' "+GetValue(PFWALARMNG|PCIM!198:(ThisObject).ALM_VALUE)+'"'
        /// OLD FORMAT: Run '+getalias(PCIMUTIL)+"Scada_sms.bat"+" "+GetValue(PFWALARMNGIPCIM!198:(ThisObject).DESCRIPTION)+" "+22+" "+GetValue(PFWALARMNGIPCIM!198:(ThisObject).ALM_VALUE)+""
        /// </summary>
        /// <returns>List of group IDs found in SMS commands</returns>
        [NotMapped]
        public List<int> AssignedGroupIds
        {
            get
            {
                if (string.IsNullOrWhiteSpace(Action))
                    return new List<int>();

                var groupIds = new List<int>();
                
                // NEW FORMAT: Pattern to match group ID in new syntax
                // Looking for: +'"'+<NUMBER>+' "
                // Example: +'"'+31+' "+GetValue
                var newFormatPattern = @"\+['""]\""\+\s*(\d+)\s*\+\s*['""]?\s*\""";
                var newMatches = Regex.Matches(Action, newFormatPattern);
                
                foreach (Match match in newMatches)
                {
                    if (match.Success && int.TryParse(match.Groups[1].Value, out int groupId))
                    {
                        if (!groupIds.Contains(groupId))
                        {
                            groupIds.Add(groupId);
                        }
                    }
                }

                // If new format found groups, return them
                if (groupIds.Any())
                    return groupIds;

                // OLD FORMAT: Fallback to old pattern
                // Looking for: +" "+<NUMBER>+" "
                var oldFormatPattern = @"\+\s*""\s*""\s*\+\s*(\d+)\s*\+\s*""\s*""";
                var oldMatches = Regex.Matches(Action, oldFormatPattern);
                
                foreach (Match match in oldMatches)
                {
                    if (match.Success && int.TryParse(match.Groups[1].Value, out int groupId))
                    {
                        if (!groupIds.Contains(groupId))
                        {
                            groupIds.Add(groupId);
                        }
                    }
                }

                // If still no matches, try the simplest pattern (works for both formats)
                if (!groupIds.Any())
                {
                    // Look for any number between quote patterns
                    var simpleFallbackPattern = @"['""]?\+\s*(\d+)\s*\+\s*['""]";
                    var fallbackMatches = Regex.Matches(Action, simpleFallbackPattern);
                    
                    foreach (Match match in fallbackMatches)
                    {
                        if (match.Success && int.TryParse(match.Groups[1].Value, out int fallbackGroupId))
                        {
                            // Only add if it's a reasonable group ID (between 1-1000)
                            if (fallbackGroupId > 0 && fallbackGroupId < 1000 && !groupIds.Contains(fallbackGroupId))
                            {
                                groupIds.Add(fallbackGroupId);
                            }
                        }
                    }
                }

                return groupIds;
            }
        }
    }

    /// <summary>
    /// DBLIST table - contains block metadata
    /// Used for joining with AlarmConditions to get block names
    /// </summary>
    [Table("DBLIST")]
    public class DBListBlock
    {
        [Key]
        [Column("BLOCK_INDEX")]
        public int BlockIndex { get; set; }

        [Column("BLOCK_NAME")]
        public string BlockName { get; set; } = string.Empty;
    }

    /// <summary>
    /// View model for managing alarm group assignments
    /// Combines data from AlarmConditions and DBLIST tables
    /// </summary>
    public class AlarmGroupAssignment
    {
        public int BlockId { get; set; }
        public string BlockName { get; set; } = string.Empty;
        public string? AlarmId { get; set; }
        public string? AlarmDescription { get; set; }
        public string? OriginalAction { get; set; }
        public List<int> AssignedGroupIds { get; set; } = new();
        public List<Group> AssignedGroups { get; set; } = new();
        public bool IsActive { get; set; }
        public DateTime? LastModified { get; set; }
        public string? ModifiedBy { get; set; }
    }

    /// <summary>
    /// Audit record for tracking alarm action modifications
    /// Stored in our own audit table
    /// </summary>
    [Table("alarm_action_audit")]
    public class AlarmActionAudit
    {
        [Key]
        [Column("audit_id")]
        public int AuditId { get; set; }

        [Column("block_index")]
        public int BlockId { get; set; }

        [Column("action_type")]
        [StringLength(50)]
        public string ActionType { get; set; } = string.Empty; // "ADD_GROUP", "REMOVE_GROUP", "CHANGE_GROUP"

        [Column("old_group_id")]
        public int? OldGroupId { get; set; }

        [Column("new_group_id")]
        public int? NewGroupId { get; set; }

        [Column("old_action")]
        public string? OldAction { get; set; }

        [Column("new_action")]
        public string? NewAction { get; set; }

        [Column("modified_by")]
        [StringLength(100)]
        public string ModifiedBy { get; set; } = string.Empty;

        [Column("modified_at")]
        public DateTime ModifiedAt { get; set; } = DateTime.Now;

        [Column("ip_address")]
        [StringLength(50)]
        public string? IpAddress { get; set; }
    }
}
