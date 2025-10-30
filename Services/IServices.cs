using SCADASMSSystem.Web.Models;

namespace SCADASMSSystem.Web.Services
{
    public interface IUserService
    {
        Task<IEnumerable<User>> GetAllUsersAsync();
        Task<User?> GetUserByIdAsync(int userId);
        Task<bool> CreateUserAsync(User user);
        Task<bool> UpdateUserAsync(User user);
        Task<bool> DeleteUserAsync(int userId);
        Task<bool> ToggleUserSettingAsync(int userId, string setting, bool value);
        Task<IEnumerable<User>> GetSmsRecipientsForGroupAsync(int groupId, bool? isSpecialDay = null);
    }

    public interface IGroupService
    {
        Task<IEnumerable<Group>> GetAllGroupsAsync();
        Task<Group?> GetGroupByIdAsync(int groupId);
        Task<bool> CreateGroupAsync(Group group);
        Task<bool> UpdateGroupAsync(Group group);
        Task<bool> DeleteGroupAsync(int groupId);
        Task<IEnumerable<User>> GetGroupMembersAsync(int groupId);
        Task<bool> AddUserToGroupAsync(int groupId, int userId);
        Task<bool> RemoveUserFromGroupAsync(int groupId, int userId);
        Task<IEnumerable<User>> GetAvailableUsersForGroupAsync(int groupId);
    }

    public interface ISmsService
    {
        Task<bool> SendSmsAsync(string message, IEnumerable<string> phoneNumbers, string alarmId, int? groupId = null);
        Task<bool> SendSmsToGroupAsync(string message, int groupId, string alarmId);
        Task<SmsApiResponse> SendSmsApiCallAsync(string message, string phoneNumber);
    }

    public interface ISmsBackgroundService
    {
        Task<bool> QueueSmsMessageAsync(string message, int groupId, string alarmId, string priority = "normal");
        SmsServiceStatus GetServiceStatus();
    }

    public interface IHolidayService
    {
        Task<bool> IsSabbaticalHolidayAsync(DateTime? date = null);
        Task<DateDimension?> GetDateInfoAsync(DateTime date);
        Task InitializeDateDimensionAsync(int yearsAhead = 10);
    }

    public interface IAuditService
    {
        Task<IEnumerable<SmsAudit>> GetAuditHistoryAsync(int page = 1, int pageSize = 50);
        Task<bool> LogSmsAuditAsync(string alarmId, int userId, string phoneNumber, 
            string alarmDescription, string status, string? messageStatus = null, string? response = null, int? groupId = null);
        Task<IEnumerable<SmsAudit>> SearchAuditAsync(string? searchTerm, DateTime? startDate, DateTime? endDate);
        Task<int> GetTotalAuditCountAsync();
        Task<IEnumerable<SmsAudit>> GetAuditsByAlarmIdAsync(string alarmId);
        Task<IEnumerable<SmsAudit>> GetAuditsByUserIdAsync(int userId, int days = 30);
        Task<Dictionary<string, int>> GetAuditStatisticsAsync(int days = 30);
        Task<bool> CleanupOldAuditsAsync(int daysToKeep = 90);
    }

    public interface IAlarmActionService
    {
        Task<IEnumerable<AlarmAction>> GetAllAlarmActionsAsync();
        Task<AlarmAction?> GetAlarmActionByIdAsync(int blockId);
        Task<AlarmGroupAssignment?> GetAlarmGroupAssignmentAsync(int blockId);
        Task<bool> AddGroupToAlarmAsync(int blockId, int groupId, string modifiedBy, string? ipAddress = null);
        Task<bool> RemoveGroupFromAlarmAsync(int blockId, int groupId, string modifiedBy, string? ipAddress = null);
        Task<bool> RemoveAllGroupsFromAlarmAsync(int blockId, string modifiedBy, string? ipAddress = null);
        Task<bool> UpdateAlarmGroupsAsync(int blockId, List<int> groupIds, string modifiedBy, string? ipAddress = null);
        string BuildActionCommand(string baseCommand, List<int> groupIds);
        List<int> ParseGroupIdsFromAction(string? action);
    }

    public class SmsApiResponse
    {
        public bool Success { get; set; }
        public string? Message { get; set; }
        public string? Response { get; set; }
        public int StatusCode { get; set; }
    }
}