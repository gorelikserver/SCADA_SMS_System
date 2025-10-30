using Microsoft.EntityFrameworkCore;
using SCADASMSSystem.Web.Data;
using SCADASMSSystem.Web.Models;

namespace SCADASMSSystem.Web.Services
{
    public class GroupService : IGroupService
    {
        private readonly SCADADbContext _context;
        private readonly ILogger<GroupService> _logger;

        public GroupService(SCADADbContext context, ILogger<GroupService> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task<IEnumerable<Group>> GetAllGroupsAsync()
        {
            try
            {
                return await _context.Groups
                    .Include(g => g.GroupMembers)
                        .ThenInclude(gm => gm.User)
                    .OrderBy(g => g.GroupName)
                    .ToListAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting all groups");
                return Enumerable.Empty<Group>();
            }
        }

        public async Task<Group?> GetGroupByIdAsync(int groupId)
        {
            try
            {
                return await _context.Groups
                    .Include(g => g.GroupMembers)
                        .ThenInclude(gm => gm.User)
                    .FirstOrDefaultAsync(g => g.GroupId == groupId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting group by ID {GroupId}", groupId);
                return null;
            }
        }

        public async Task<bool> CreateGroupAsync(Group group)
        {
            try
            {
                group.CreatedAt = DateTime.Now;
                _context.Groups.Add(group);
                await _context.SaveChangesAsync();
                _logger.LogInformation("Created group {GroupName} with ID {GroupId}", group.GroupName, group.GroupId);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating group {GroupName}", group.GroupName);
                return false;
            }
        }

        public async Task<bool> UpdateGroupAsync(Group group)
        {
            try
            {
                _context.Groups.Update(group);
                await _context.SaveChangesAsync();
                _logger.LogInformation("Updated group {GroupName} with ID {GroupId}", group.GroupName, group.GroupId);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating group {GroupId}", group.GroupId);
                return false;
            }
        }

        public async Task<bool> DeleteGroupAsync(int groupId)
        {
            try
            {
                // Use execution strategy to handle retries properly
                var strategy = _context.Database.CreateExecutionStrategy();
                
                return await strategy.ExecuteAsync(async () =>
                {
                    using var transaction = await _context.Database.BeginTransactionAsync();

                    // Remove all group members first
                    var groupMembers = await _context.GroupMembers
                        .Where(gm => gm.GroupId == groupId)
                        .ToListAsync();

                    if (groupMembers.Any())
                    {
                        _context.GroupMembers.RemoveRange(groupMembers);
                        await _context.SaveChangesAsync();
                    }

                    // Remove the group
                    var group = await _context.Groups.FindAsync(groupId);
                    if (group != null)
                    {
                        _context.Groups.Remove(group);
                        await _context.SaveChangesAsync();
                    }

                    await transaction.CommitAsync();

                    _logger.LogInformation("Deleted group {GroupId} and {MemberCount} members", groupId, groupMembers.Count);
                    return true;
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting group {GroupId}", groupId);
                return false;
            }
        }

        public async Task<IEnumerable<User>> GetGroupMembersAsync(int groupId)
        {
            try
            {
                return await _context.Users
                    .Join(_context.GroupMembers, u => u.UserId, gm => gm.UserId, (u, gm) => new { User = u, GroupMember = gm })
                    .Where(x => x.GroupMember.GroupId == groupId)
                    .Select(x => x.User)
                    .OrderBy(u => u.UserName)
                    .ToListAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting members for group {GroupId}", groupId);
                return Enumerable.Empty<User>();
            }
        }

        public async Task<bool> AddUserToGroupAsync(int groupId, int userId)
        {
            try
            {
                // Check if user is already in group
                var existing = await _context.GroupMembers
                    .FirstOrDefaultAsync(gm => gm.GroupId == groupId && gm.UserId == userId);

                if (existing != null)
                {
                    _logger.LogWarning("User {UserId} is already a member of group {GroupId}", userId, groupId);
                    return false;
                }

                var groupMember = new GroupMember
                {
                    GroupId = groupId,
                    UserId = userId,
                    CreatedAt = DateTime.Now
                };

                _context.GroupMembers.Add(groupMember);
                await _context.SaveChangesAsync();

                _logger.LogInformation("Added user {UserId} to group {GroupId}", userId, groupId);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error adding user {UserId} to group {GroupId}", userId, groupId);
                return false;
            }
        }

        public async Task<bool> RemoveUserFromGroupAsync(int groupId, int userId)
        {
            try
            {
                var groupMember = await _context.GroupMembers
                    .FirstOrDefaultAsync(gm => gm.GroupId == groupId && gm.UserId == userId);

                if (groupMember == null)
                {
                    _logger.LogWarning("User {UserId} is not a member of group {GroupId}", userId, groupId);
                    return false;
                }

                _context.GroupMembers.Remove(groupMember);
                await _context.SaveChangesAsync();

                _logger.LogInformation("Removed user {UserId} from group {GroupId}", userId, groupId);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error removing user {UserId} from group {GroupId}", userId, groupId);
                return false;
            }
        }

        public async Task<IEnumerable<User>> GetAvailableUsersForGroupAsync(int groupId)
        {
            try
            {
                _logger.LogInformation("?? DEBUGGING GetAvailableUsersForGroupAsync for group {GroupId}", groupId);
                
                // Step 1: Get current group members
                _logger.LogDebug("Step 1: Getting current group members for group {GroupId}", groupId);
                var groupMemberUserIds = await _context.GroupMembers
                    .Where(gm => gm.GroupId == groupId)
                    .Select(gm => gm.UserId)
                    .ToListAsync();

                _logger.LogInformation("?? Group {GroupId} currently has member user IDs: [{UserIds}]", 
                    groupId, string.Join(", ", groupMemberUserIds));

                // Step 2: Get all users
                _logger.LogDebug("Step 2: Getting all users from database");
                var allUsers = await _context.Users.ToListAsync();
                _logger.LogInformation("?? Total users in database: {Count} [{AllUsers}]",
                    allUsers.Count,
                    string.Join(", ", allUsers.Select(u => $"{u.UserName}(ID:{u.UserId})")));

                // Step 3: Manual filter to debug
                _logger.LogDebug("Step 3: Manually filtering available users");
                var manualAvailable = new List<User>();
                foreach (var user in allUsers)
                {
                    if (!groupMemberUserIds.Contains(user.UserId))
                    {
                        manualAvailable.Add(user);
                        _logger.LogDebug("? User {UserName}(ID:{UserId}) is available", user.UserName, user.UserId);
                    }
                    else
                    {
                        _logger.LogDebug("? User {UserName}(ID:{UserId}) is already a member", user.UserName, user.UserId);
                    }
                }
                
                _logger.LogInformation("?? Manual filter result: {Count} users available", manualAvailable.Count);

                // Step 4: Try EF Core query
                _logger.LogDebug("Step 4: Attempting EF Core LINQ query");
                List<User> availableUsers;
                
                try
                {
                    availableUsers = await _context.Users
                        .Where(u => !groupMemberUserIds.Contains(u.UserId))
                        .OrderBy(u => u.UserName)
                        .ToListAsync();
                    
                    _logger.LogInformation("? EF Core query successful: {Count} users returned", availableUsers.Count);
                }
                catch (Exception efEx)
                {
                    _logger.LogError(efEx, "? EF Core query failed, using manual results instead");
                    availableUsers = manualAvailable.OrderBy(u => u.UserName).ToList();
                }

                _logger.LogInformation("?? Final result: {Count} available users: [{AvailableUsers}]",
                    availableUsers.Count,
                    string.Join(", ", availableUsers.Select(u => $"{u.UserName}(ID:{u.UserId})")));

                // Final comparison
                if (availableUsers.Count != manualAvailable.Count)
                {
                    _logger.LogWarning("?? MISMATCH DETECTED!");
                    _logger.LogWarning("   EF Core query returned: {EfCount}", availableUsers.Count);
                    _logger.LogWarning("   Manual calculation: {ManualCount}", manualAvailable.Count);
                    _logger.LogWarning("   Using manual results as fallback");
                    return manualAvailable.OrderBy(u => u.UserName);
                }

                return availableUsers;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "? CRITICAL ERROR in GetAvailableUsersForGroupAsync for group {GroupId}", groupId);
                _logger.LogError("Exception Type: {ExceptionType}", ex.GetType().Name);
                _logger.LogError("Exception Message: {ExceptionMessage}", ex.Message);
                _logger.LogError("Stack Trace: {StackTrace}", ex.StackTrace);
                
                // Return empty collection to prevent crashes, but log the error
                return Enumerable.Empty<User>();
            }
        }
    }
}