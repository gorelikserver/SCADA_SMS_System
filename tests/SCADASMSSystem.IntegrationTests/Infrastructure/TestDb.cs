using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using SCADASMSSystem.Web.Data;
using SCADASMSSystem.Web.Models;

namespace SCADASMSSystem.IntegrationTests.Infrastructure;

public sealed record SeededUser(int UserId, string Phone, string? FirstName, string? LastName, string? TZ);
public sealed record SeededGroup(int GroupId, string Name, IReadOnlyList<SeededUser> Members);

/// <summary>
/// The dedicated test database (scada_mock_it on the local SQL Server). Recreated once per test
/// process with the app's own EF migrations; never touches any other database.
/// </summary>
public static class TestDb
{
    private static readonly SemaphoreSlim InitLock = new(1, 1);
    private static bool _ready;
    private static int _phoneCounter;
    private static readonly int PhoneBase = (Environment.ProcessId % 90 + 10) * 100000;

    public static SCADADbContext NewContext() =>
        new(new DbContextOptionsBuilder<SCADADbContext>()
            .UseSqlServer(MockIt.SqlConnectionString, o => o.CommandTimeout(60)).Options);

    public static async Task EnsureReadyAsync()
    {
        if (_ready) return;
        await InitLock.WaitAsync();
        try
        {
            if (_ready) return;
            var builder = new SqlConnectionStringBuilder(MockIt.SqlConnectionString);
            if (!builder.InitialCatalog.Equals(MockIt.DatabaseName, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Refusing to touch database " + builder.InitialCatalog);
            builder.InitialCatalog = "master";
            await using (var conn = new SqlConnection(builder.ConnectionString))
            {
                await conn.OpenAsync();
                await using var cmd = conn.CreateCommand();
                cmd.CommandText =
                    $"IF DB_ID('{MockIt.DatabaseName}') IS NOT NULL BEGIN " +
                    $"ALTER DATABASE [{MockIt.DatabaseName}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [{MockIt.DatabaseName}]; END";
                cmd.CommandTimeout = 120;
                await cmd.ExecuteNonQueryAsync();
            }
            SqlConnection.ClearAllPools();
            await using (var ctx = NewContext())
                await ctx.Database.MigrateAsync();
            _ready = true;
        }
        finally
        {
            InitLock.Release();
        }
    }

    /// <summary>Unique, valid Israeli mobile number (stored as 05XXXXXXXX, 10 digits).</summary>
    public static string NextPhone() => "05" + (PhoneBase + Interlocked.Increment(ref _phoneCounter)).ToString("D8");

    /// <summary>
    /// A group with SMS-enabled members who also work on special days, so the holiday filter never
    /// changes the recipient list. Names alternate Hebrew/English; every third member has no TZ.
    /// </summary>
    public static async Task<SeededGroup> CreateGroupAsync(string name, int members, bool withPersonFields = true)
    {
        await EnsureReadyAsync();
        await using var ctx = NewContext();
        var group = new Group { GroupName = name };
        ctx.Groups.Add(group);
        var users = new List<User>();
        for (var i = 0; i < members; i++)
        {
            var hebrew = i % 2 == 0;
            users.Add(new User
            {
                UserName = $"{name}-u{i + 1}",
                PhoneNumber = NextPhone(),
                SmsEnabled = true,
                SpecialDaysEnabled = true,
                FirstName = withPersonFields ? (hebrew ? "דנה" : "Avi") : null,
                LastName = withPersonFields ? (hebrew ? "כהן" : "Levi") : null,
                TZ = withPersonFields && i % 3 != 2 ? (300000000 + i).ToString() : null,
            });
        }
        ctx.Users.AddRange(users);
        await ctx.SaveChangesAsync();
        foreach (var u in users)
            ctx.GroupMembers.Add(new GroupMember { GroupId = group.GroupId, UserId = u.UserId });
        await ctx.SaveChangesAsync();
        return new SeededGroup(group.GroupId, name,
            users.Select(u => new SeededUser(u.UserId, u.PhoneNumber, u.FirstName, u.LastName, u.TZ)).ToList());
    }

    public static async Task<List<CriticalSmsDelivery>> DeliveriesForGroupAsync(int groupId)
    {
        await using var ctx = NewContext();
        return await ctx.CriticalSmsDeliveries.AsNoTracking().Where(d => d.GroupId == groupId)
            .OrderBy(d => d.AlarmId).ThenBy(d => d.PhoneNumber).ToListAsync();
    }

    public static async Task<List<CriticalSmsDelivery>> DeliveriesForGroupsAsync(IEnumerable<int> groupIds)
    {
        var ids = groupIds.ToList();
        await using var ctx = NewContext();
        return await ctx.CriticalSmsDeliveries.AsNoTracking().Where(d => ids.Contains(d.GroupId)).ToListAsync();
    }

    public static async Task<List<SmsAudit>> AuditsForGroupAsync(int groupId)
    {
        await using var ctx = NewContext();
        return await ctx.SmsAudits.AsNoTracking().Where(a => a.GroupId == groupId).OrderBy(a => a.AuditId).ToListAsync();
    }
}
