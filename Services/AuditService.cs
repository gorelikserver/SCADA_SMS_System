using Microsoft.EntityFrameworkCore;
using SCADASMSSystem.Web.Data;
using SCADASMSSystem.Web.Models;

namespace SCADASMSSystem.Web.Services
{
    public class AuditService : IAuditService
    {
        private readonly SCADADbContext _context;
        private readonly ILogger<AuditService> _logger;

        public AuditService(SCADADbContext context, ILogger<AuditService> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task<IEnumerable<SmsAudit>> GetAuditHistoryAsync(int page = 1, int pageSize = 50)
        {
            try
            {
                var skip = (page - 1) * pageSize;
                
                // Try with Group include first, if it fails, fall back to basic query
                try
                {
                    return await _context.SmsAudits
                        .Include(sa => sa.User)
                        .Include(sa => sa.Group)
                        .OrderByDescending(sa => sa.CreatedAt)
                        .Skip(skip)
                        .Take(pageSize)
                        .ToListAsync();
                }
                catch (Microsoft.Data.SqlClient.SqlException ex) when (ex.Message.Contains("Invalid column name 'group_id'"))
                {
                    _logger.LogWarning("Group column not available in database, falling back to basic audit query");
                    
                    // Fallback query without Group navigation
                    return await _context.SmsAudits
                        .Include(sa => sa.User)
                        .OrderByDescending(sa => sa.CreatedAt)
                        .Skip(skip)
                        .Take(pageSize)
                        .ToListAsync();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting audit history for page {Page}", page);
                return Enumerable.Empty<SmsAudit>();
            }
        }

        public async Task<bool> LogSmsAuditAsync(string alarmId, int userId, string phoneNumber, 
            string alarmDescription, string status, string? messageStatus = null, string? response = null, int? groupId = null)
        {
            try
            {
                _logger.LogDebug("=== AUDIT LOGGING DEBUG ===");
                _logger.LogDebug("Attempting to log audit for AlarmId: {AlarmId}", alarmId);
                _logger.LogDebug("UserId: {UserId}, Phone: {PhoneNumber}, GroupId: {GroupId}", userId, phoneNumber, groupId);
                _logger.LogDebug("Status: {Status}", status);

                // Validate required fields
                if (string.IsNullOrEmpty(alarmId))
                {
                    _logger.LogError("Cannot log audit: AlarmId is null or empty");
                    return false;
                }

                if (string.IsNullOrEmpty(phoneNumber))
                {
                    _logger.LogError("Cannot log audit: PhoneNumber is null or empty");
                    return false;
                }

                if (string.IsNullOrEmpty(alarmDescription))
                {
                    _logger.LogError("Cannot log audit: AlarmDescription is null or empty");
                    return false;
                }

                // Truncate long fields to prevent database errors
                var truncatedMessageStatus = messageStatus?.Length > 200 ? messageStatus[..197] + "..." : messageStatus;
                var truncatedResponse = response?.Length > 1000 ? response[..997] + "..." : response;
                var truncatedDescription = alarmDescription.Length > 500 ? alarmDescription[..497] + "..." : alarmDescription;

                var auditEntry = new SmsAudit
                {
                    AlarmId = alarmId,
                    UserId = userId,
                    GroupId = groupId,
                    PhoneNumber = phoneNumber,
                    AlarmDescription = truncatedDescription,
                    Status = status,
                    MessageStatus = truncatedMessageStatus,
                    ApiResponse = truncatedResponse,
                    CreatedAt = DateTime.Now
                };

                _logger.LogDebug("Creating audit entry object successful");
                _logger.LogDebug("Database context state: {ContextState}", _context.ChangeTracker.DebugView.ShortView);

                _context.SmsAudits.Add(auditEntry);
                _logger.LogDebug("Added audit entry to context");

                var saveResult = await _context.SaveChangesAsync();
                _logger.LogDebug("SaveChangesAsync returned: {SaveResult}", saveResult);

                if (saveResult > 0)
                {
                    _logger.LogInformation("Successfully logged SMS audit for alarm {AlarmId} to {PhoneNumber} (Group: {GroupId}) with status {Status} - Saved {Count} records", 
                        alarmId, phoneNumber, groupId, status, saveResult);
                    return true;
                }
                else
                {
                    _logger.LogError("SaveChangesAsync returned 0 - no records were saved for alarm {AlarmId}", alarmId);
                    return false;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error logging SMS audit for alarm {AlarmId} to phone {PhoneNumber}. Exception details: {ExceptionType}: {ExceptionMessage}", 
                    alarmId, phoneNumber, ex.GetType().Name, ex.Message);
                
                // Log inner exception if present
                if (ex.InnerException != null)
                {
                    _logger.LogError("Inner exception: {InnerExceptionType}: {InnerExceptionMessage}", 
                        ex.InnerException.GetType().Name, ex.InnerException.Message);
                }

                // Log SQL exception details if present
                if (ex is Microsoft.Data.SqlClient.SqlException sqlEx)
                {
                    _logger.LogError("SQL Exception Details - Number: {SqlNumber}, Severity: {SqlSeverity}, State: {SqlState}, Procedure: {SqlProcedure}, Line: {SqlLine}", 
                        sqlEx.Number, sqlEx.Class, sqlEx.State, sqlEx.Procedure, sqlEx.LineNumber);
                }

                return false;
            }
        }

        public async Task<IEnumerable<SmsAudit>> SearchAuditAsync(string? searchTerm, DateTime? startDate, DateTime? endDate)
        {
            try
            {
                var query = _context.SmsAudits
                    .Include(sa => sa.User)
                    .AsQueryable();

                // Try to include Group, but handle missing column gracefully
                try
                {
                    query = _context.SmsAudits
                        .Include(sa => sa.User)
                        .Include(sa => sa.Group)
                        .AsQueryable();
                }
                catch (Microsoft.Data.SqlClient.SqlException ex) when (ex.Message.Contains("Invalid column name 'group_id'"))
                {
                    _logger.LogWarning("Group column not available in database, using basic search");
                    query = _context.SmsAudits
                        .Include(sa => sa.User)
                        .AsQueryable();
                }

                // Apply search term filter
                if (!string.IsNullOrWhiteSpace(searchTerm))
                {
                    query = query.Where(sa => 
                        sa.AlarmId.Contains(searchTerm) ||
                        sa.AlarmDescription.Contains(searchTerm) ||
                        sa.PhoneNumber.Contains(searchTerm) ||
                        sa.Status.Contains(searchTerm) ||
                        (sa.User != null && sa.User.UserName.Contains(searchTerm)));
                }

                // Apply date range filters
                if (startDate.HasValue)
                {
                    query = query.Where(sa => sa.CreatedAt >= startDate.Value.Date);
                }

                if (endDate.HasValue)
                {
                    query = query.Where(sa => sa.CreatedAt <= endDate.Value.Date.AddDays(1).AddTicks(-1));
                }

                return await query
                    .OrderByDescending(sa => sa.CreatedAt)
                    .Take(1000) // Limit results to prevent performance issues
                    .ToListAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error searching audit with term '{SearchTerm}' between {StartDate} and {EndDate}", 
                    searchTerm, startDate, endDate);
                return Enumerable.Empty<SmsAudit>();
            }
        }

        public async Task<int> GetTotalAuditCountAsync()
        {
            try
            {
                return await _context.SmsAudits.CountAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting total audit count");
                return 0;
            }
        }

        public async Task<IEnumerable<SmsAudit>> GetAuditsByAlarmIdAsync(string alarmId)
        {
            try
            {
                // Try with Group include first, if it fails, fall back to basic query
                try
                {
                    return await _context.SmsAudits
                        .Include(sa => sa.User)
                        .Include(sa => sa.Group)
                        .Where(sa => sa.AlarmId == alarmId)
                        .OrderByDescending(sa => sa.CreatedAt)
                        .ToListAsync();
                }
                catch (Microsoft.Data.SqlClient.SqlException ex) when (ex.Message.Contains("Invalid column name 'group_id'"))
                {
                    _logger.LogWarning("Group column not available in database, using basic query");
                    
                    return await _context.SmsAudits
                        .Include(sa => sa.User)
                        .Where(sa => sa.AlarmId == alarmId)
                        .OrderByDescending(sa => sa.CreatedAt)
                        .ToListAsync();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting audits for alarm {AlarmId}", alarmId);
                return Enumerable.Empty<SmsAudit>();
            }
        }

        public async Task<IEnumerable<SmsAudit>> GetAuditsByUserIdAsync(int userId, int days = 30)
        {
            try
            {
                var cutoffDate = DateTime.Now.AddDays(-days);
                
                // Try with Group include first, if it fails, fall back to basic query
                try
                {
                    return await _context.SmsAudits
                        .Include(sa => sa.User)
                        .Include(sa => sa.Group)
                        .Where(sa => sa.UserId == userId && sa.CreatedAt >= cutoffDate)
                        .OrderByDescending(sa => sa.CreatedAt)
                        .ToListAsync();
                }
                catch (Microsoft.Data.SqlClient.SqlException ex) when (ex.Message.Contains("Invalid column name 'group_id'"))
                {
                    _logger.LogWarning("Group column not available in database, using basic query");
                    
                    return await _context.SmsAudits
                        .Include(sa => sa.User)
                        .Where(sa => sa.UserId == userId && sa.CreatedAt >= cutoffDate)
                        .OrderByDescending(sa => sa.CreatedAt)
                        .ToListAsync();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting audits for user {UserId}", userId);
                return Enumerable.Empty<SmsAudit>();
            }
        }

        public async Task<Dictionary<string, int>> GetAuditStatisticsAsync(int days = 30)
        {
            try
            {
                var cutoffDate = DateTime.Now.AddDays(-days);
                
                var stats = await _context.SmsAudits
                    .Where(sa => sa.CreatedAt >= cutoffDate)
                    .GroupBy(sa => sa.Status)
                    .Select(g => new { Status = g.Key, Count = g.Count() })
                    .ToListAsync();

                var result = new Dictionary<string, int>();
                foreach (var stat in stats)
                {
                    result[stat.Status] = stat.Count;
                }

                // Add total count
                result["Total"] = stats.Sum(s => s.Count);

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting audit statistics");
                return new Dictionary<string, int>();
            }
        }

        public async Task<bool> CleanupOldAuditsAsync(int daysToKeep = 90)
        {
            try
            {
                var cutoffDate = DateTime.Now.AddDays(-daysToKeep);
                
                var oldAudits = await _context.SmsAudits
                    .Where(sa => sa.CreatedAt < cutoffDate)
                    .ToListAsync();

                if (oldAudits.Any())
                {
                    _context.SmsAudits.RemoveRange(oldAudits);
                    await _context.SaveChangesAsync();
                    
                    _logger.LogInformation("Cleaned up {Count} old audit records older than {Days} days", 
                        oldAudits.Count, daysToKeep);
                }

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error cleaning up old audit records");
                return false;
            }
        }
    }
}