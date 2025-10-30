using Microsoft.EntityFrameworkCore;
using SCADASMSSystem.Web.Data;
using SCADASMSSystem.Web.Models;

namespace SCADASMSSystem.Web.Services
{
    public class UserService : IUserService
    {
        private readonly SCADADbContext _context;
        private readonly ILogger<UserService> _logger;
        private readonly IServiceProvider _serviceProvider;

        public UserService(SCADADbContext context, ILogger<UserService> logger, IServiceProvider serviceProvider)
        {
            _context = context;
            _logger = logger;
            _serviceProvider = serviceProvider;
        }

        public async Task<IEnumerable<User>> GetAllUsersAsync()
        {
            try
            {
                return await _context.Users
                    .Include(u => u.GroupMembers)  // CRITICAL: Include GroupMembers for UI display
                        .ThenInclude(gm => gm.Group)  // Also include Group details
                    .OrderBy(u => u.UserName)
                    .ToListAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting all users");
                return Enumerable.Empty<User>();
            }
        }

        public async Task<User?> GetUserByIdAsync(int userId)
        {
            try
            {
                return await _context.Users
                    .FirstOrDefaultAsync(u => u.UserId == userId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting user by ID {UserId}", userId);
                return null;
            }
        }

        public async Task<bool> CreateUserAsync(User user)
        {
            try
            {
                user.CreatedAt = DateTime.Now;
                _context.Users.Add(user);
                await _context.SaveChangesAsync();
                _logger.LogInformation("Created user {UserName} with ID {UserId}", user.UserName, user.UserId);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating user {UserName}", user.UserName);
                return false;
            }
        }

        public async Task<bool> UpdateUserAsync(User user)
        {
            try
            {
                _context.Users.Update(user);
                await _context.SaveChangesAsync();
                _logger.LogInformation("Updated user {UserName} with ID {UserId}", user.UserName, user.UserId);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating user {UserId}", user.UserId);
                return false;
            }
        }

        public async Task<bool> DeleteUserAsync(int userId)
        {
            try
            {
                using var transaction = await _context.Database.BeginTransactionAsync();
                
                // First remove from groups
                var groupMembers = await _context.GroupMembers
                    .Where(gm => gm.UserId == userId)
                    .ToListAsync();
                
                _context.GroupMembers.RemoveRange(groupMembers);

                // Set user_id to null in audit records to preserve history
                var auditRecords = await _context.SmsAudits
                    .Where(sa => sa.UserId == userId)
                    .ToListAsync();

                foreach (var audit in auditRecords)
                {
                    audit.UserId = 0; // Use 0 to indicate deleted user
                }

                // Remove the user
                var user = await _context.Users.FindAsync(userId);
                if (user != null)
                {
                    _context.Users.Remove(user);
                }

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                _logger.LogInformation("Deleted user {UserId}", userId);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting user {UserId}", userId);
                return false;
            }
        }

        public async Task<bool> ToggleUserSettingAsync(int userId, string setting, bool value)
        {
            try
            {
                var user = await _context.Users.FindAsync(userId);
                if (user == null)
                {
                    return false;
                }

                switch (setting.ToLower())
                {
                    case "sms_enabled":
                        user.SmsEnabled = value;
                        break;
                    case "special_days_enabled":
                        user.SpecialDaysEnabled = value;
                        break;
                    default:
                        return false;
                }

                await _context.SaveChangesAsync();
                _logger.LogInformation("Toggled {Setting} to {Value} for user {UserId}", setting, value, userId);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error toggling setting {Setting} for user {UserId}", setting, userId);
                return false;
            }
        }

        public async Task<IEnumerable<User>> GetSmsRecipientsForGroupAsync(int groupId, bool? isSpecialDay = null)
        {
            try
            {
                // Check if today is a sabbatical holiday unless overridden
                bool isHoliday = isSpecialDay ?? await IsSabbaticalHolidayAsync();
                
                var query = _context.Users
                    .Join(_context.GroupMembers, u => u.UserId, gm => gm.UserId, (u, gm) => new { User = u, GroupMember = gm })
                    .Where(x => x.GroupMember.GroupId == groupId && x.User.SmsEnabled);

                // Add holiday filter if it's a holiday
                if (isHoliday)
                {
                    query = query.Where(x => x.User.SpecialDaysEnabled);
                    _logger.LogInformation("Filtering SMS recipients to only those who work on holidays for group {GroupId}", groupId);
                }

                var recipients = await query.Select(x => x.User).ToListAsync();
                
                _logger.LogInformation("Found {Count} eligible SMS recipients for group {GroupId}", recipients.Count, groupId);
                return recipients;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting SMS recipients for group {GroupId}", groupId);
                return Enumerable.Empty<User>();
            }
        }

        private async Task<bool> IsSabbaticalHolidayAsync()
        {
            try
            {
                // Get holiday service from service provider to avoid circular dependency
                var holidayService = _serviceProvider.GetService<IHolidayService>();
                if (holidayService != null)
                {
                    return await holidayService.IsSabbaticalHolidayAsync();
                }
                
                // Fallback to checking if today is Saturday
                return DateTime.Now.DayOfWeek == DayOfWeek.Saturday;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking sabbatical holiday status");
                // Default to false to avoid blocking SMS notifications
                return false;
            }
        }
    }
}