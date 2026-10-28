using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using SCADASMSSystem.Web.Data;
using SCADASMSSystem.Web.Models;

namespace SCADASMSSystem.Web.Services
{
    public enum AlarmRetryState
    {
        Retrying,   // spool file still pending: the service keeps retrying failed recipients
        GaveUp,     // spool file moved to spool\failed after MaxAgeMinutes
        NotSpooled  // no spool file (e.g. sent from the Test page): nothing will retry it
    }

    public record AlarmRecipientStatus(string MaskedPhone, string Status, int ProviderStatusCode, int Attempts, string? ProviderMessage);

    public record UnresolvedAlarm(
        string AlarmId,
        int GroupId,
        string? GroupName,
        string? Message,
        DateTime FirstSeen,
        AlarmRetryState State,
        IReadOnlyList<AlarmRecipientStatus> Recipients)
    {
        public int Accepted => Recipients.Count(r => r.Status == CriticalDeliveryStatus.Success);
    }

    public record EmptyGroup(int GroupId, string GroupName);

    public record InvalidPhoneUser(int UserId, string UserName, string PhoneNumber);

    public record DeliveryAlertSummary(
        IReadOnlyList<UnresolvedAlarm> Alarms,
        IReadOnlyList<string> BadSpoolFiles,
        IReadOnlyList<EmptyGroup> EmptyGroups,
        IReadOnlyList<InvalidPhoneUser> InvalidPhones,
        IReadOnlyList<string> ProfileErrors,
        bool TestMode,
        ScadaLinkStatus? ScadaLink = null)
    {
        public bool NeedsAttention =>
            Alarms.Count > 0 || BadSpoolFiles.Count > 0 || EmptyGroups.Count > 0 || InvalidPhones.Count > 0 || ProfileErrors.Count > 0 || ScadaLink?.IsProblem == true;
    }

    public interface IDeliveryAlertService
    {
        Task<DeliveryAlertSummary> GetSummaryAsync(CancellationToken ct = default);
        Task AcknowledgeAsync(string alarmId, string? note, string? acknowledgedBy, CancellationToken ct = default);
    }

    /// <summary>
    /// Collects every critical alarm that did not reach all recipients and is not yet acknowledged,
    /// from the delivery table and the spool folders, plus groups that would receive nobody.
    /// </summary>
    public class DeliveryAlertService : IDeliveryAlertService
    {
        // A SENDING row younger than this is a request still in flight, not a problem yet.
        private static readonly TimeSpan InFlightGrace = TimeSpan.FromMinutes(2);
        private static readonly JsonSerializerOptions ReadOptions = new() { PropertyNameCaseInsensitive = true };

        private readonly SCADADbContext _db;
        private readonly IUserService _userService;
        private readonly IGroupService _groupService;
        private readonly IOptionsMonitor<SpoolOptions> _spoolOptions;
        private readonly IOptionsMonitor<SmsSettings> _smsSettings;
        private readonly IConfiguration _configuration;
        private readonly IWebHostEnvironment _env;

        public DeliveryAlertService(
            SCADADbContext db,
            IUserService userService,
            IGroupService groupService,
            IOptionsMonitor<SpoolOptions> spoolOptions,
            IOptionsMonitor<SmsSettings> smsSettings,
            IConfiguration configuration,
            IWebHostEnvironment env)
        {
            _db = db;
            _userService = userService;
            _groupService = groupService;
            _spoolOptions = spoolOptions;
            _smsSettings = smsSettings;
            _configuration = configuration;
            _env = env;
        }

        public async Task<DeliveryAlertSummary> GetSummaryAsync(CancellationToken ct = default)
        {
            var spoolDir = Path.Combine(_spoolOptions.CurrentValue.ResolveRoot(), "spool");
            var pending = SpoolIds(spoolDir);
            var failedDir = Path.Combine(spoolDir, "failed");
            var gaveUp = SpoolIds(failedDir);
            var acknowledged = (await _db.AlarmAcknowledgements.AsNoTracking().Select(a => a.AlarmId).ToListAsync(ct))
                .ToHashSet(StringComparer.Ordinal);

            var groups = (await _groupService.GetAllGroupsAsync()).ToList();
            var groupNames = groups.ToDictionary(g => g.GroupId, g => g.GroupName);

            var inFlightBefore = DateTime.Now - InFlightGrace;
            var rows = await _db.CriticalSmsDeliveries.AsNoTracking()
                .Where(d => d.Status != CriticalDeliveryStatus.Success)
                .Where(d => !_db.AlarmAcknowledgements.Any(a => a.AlarmId == d.AlarmId))
                .Select(d => d.AlarmId)
                .Distinct()
                .ToListAsync(ct);

            var alarms = new List<UnresolvedAlarm>();
            foreach (var alarmId in rows)
            {
                var recipients = await _db.CriticalSmsDeliveries.AsNoTracking()
                    .Where(d => d.AlarmId == alarmId)
                    .OrderBy(d => d.DeliveryId)
                    .ToListAsync(ct);

                // Only fresh SENDING rows left: a request is still working on it.
                if (recipients.Where(r => r.Status != CriticalDeliveryStatus.Success)
                    .All(r => r.Status == CriticalDeliveryStatus.Sending && r.UpdatedAt >= inFlightBefore))
                    continue;

                var safeId = SafeFileName(alarmId);
                var groupId = recipients[0].GroupId;
                alarms.Add(new UnresolvedAlarm(
                    alarmId,
                    groupId,
                    groupNames.GetValueOrDefault(groupId),
                    ReadSpoolMessage(spoolDir, failedDir, safeId),
                    recipients.Min(r => r.CreatedAt),
                    StateFor(safeId, pending, gaveUp),
                    recipients.Select(r => new AlarmRecipientStatus(
                        MaskPhone(r.PhoneNumber), r.Status, r.ProviderStatusCode, r.Attempts, r.ProviderMessage)).ToList()));
            }

            // Spool files with no delivery rows: the group had no eligible recipients, or the
            // database was unreachable. They are just as undelivered.
            var known = alarms.Select(a => SafeFileName(a.AlarmId)).ToHashSet(StringComparer.Ordinal);
            foreach (var (safeId, dir, state) in pending.Select(id => (id, spoolDir, AlarmRetryState.Retrying))
                         .Concat(gaveUp.Select(id => (id, failedDir, AlarmRetryState.GaveUp))))
            {
                if (known.Contains(safeId) || acknowledged.Contains(safeId))
                    continue;
                if (state == AlarmRetryState.Retrying && File.GetLastWriteTime(Path.Combine(dir, safeId + ".json")) >= inFlightBefore)
                    continue; // the batch call may still be running
                var spooled = ReadSpool(Path.Combine(dir, safeId + ".json"));
                if (spooled == null || acknowledged.Contains(spooled.AlarmId))
                    continue;
                if (state == AlarmRetryState.Retrying && await _db.CriticalSmsDeliveries.AnyAsync(d => d.AlarmId == spooled.AlarmId, ct))
                    continue; // all rows SUCCESS or acknowledged: nothing to show
                known.Add(safeId);
                alarms.Add(new UnresolvedAlarm(spooled.AlarmId, spooled.GroupId, groupNames.GetValueOrDefault(spooled.GroupId),
                    spooled.Message, spooled.ReceivedAt?.ToLocalTime() ?? File.GetLastWriteTime(Path.Combine(dir, safeId + ".json")),
                    state, []));
            }

            var emptyGroups = new List<EmptyGroup>();
            foreach (var group in groups)
            {
                if (!(await _userService.GetSmsRecipientsForGroupAsync(group.GroupId)).Any())
                    emptyGroups.Add(new EmptyGroup(group.GroupId, group.GroupName));
            }

            // SMS-enabled users whose number cannot be dialled: alarms to them fail at the provider.
            var invalidPhones = (await _userService.GetAllUsersAsync())
                .Where(u => u.SmsEnabled && !PhoneNumberRules.IsValid(u.PhoneNumber))
                .Select(u => new InvalidPhoneUser(u.UserId, u.UserName, u.PhoneNumber))
                .ToList();

            var badDir = Path.Combine(spoolDir, "bad");
            var bad = Directory.Exists(badDir)
                ? Directory.GetFiles(badDir, "*.json").Select(Path.GetFileName).OfType<string>().OrderBy(f => f).ToList()
                : new List<string>();

            return new DeliveryAlertSummary(
                alarms.OrderByDescending(a => a.FirstSeen).ToList(), bad, emptyGroups, invalidPhones,
                Providers.ProfileValidator.Validate(_smsSettings.CurrentValue.Profile), _smsSettings.CurrentValue.TestMode,
                CheckScadaLink(_configuration, _env));
        }

        /// <summary>State of the Scada_sms.bat copy in Akara's PCIMUTIL folder (Scada:PcimUtilDir, set by the installer).</summary>
        public static ScadaLinkStatus CheckScadaLink(IConfiguration configuration, IWebHostEnvironment env)
        {
            try
            {
                return ScadaLinkChecker.Check(
                    configuration["Scada:PcimUtilDir"],
                    Path.Combine(env.ContentRootPath, "Scripts"),
                    ScadaLinkChecker.ExpectedUrl(configuration["Urls"]));
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                return new ScadaLinkStatus(ScadaLinkState.Missing, configuration["Scada:PcimUtilDir"],
                    "Cannot read the Scada_sms.bat copy in the PCIMUTIL folder: " + ex.Message);
            }
        }

        public async Task AcknowledgeAsync(string alarmId, string? note, string? acknowledgedBy, CancellationToken ct = default)
        {
            if (await _db.AlarmAcknowledgements.AnyAsync(a => a.AlarmId == alarmId, ct))
                return;
            _db.AlarmAcknowledgements.Add(new AlarmAcknowledgement { AlarmId = alarmId, Note = note, AcknowledgedBy = acknowledgedBy });
            await _db.SaveChangesAsync(ct);
        }

        private static AlarmRetryState StateFor(string safeId, HashSet<string> pending, HashSet<string> gaveUp) =>
            pending.Contains(safeId) ? AlarmRetryState.Retrying
            : gaveUp.Contains(safeId) ? AlarmRetryState.GaveUp
            : AlarmRetryState.NotSpooled;

        private static HashSet<string> SpoolIds(string dir) =>
            Directory.Exists(dir)
                ? Directory.GetFiles(dir, "*.json").Select(Path.GetFileNameWithoutExtension).OfType<string>().ToHashSet(StringComparer.Ordinal)
                : new HashSet<string>(StringComparer.Ordinal);

        private static string? ReadSpoolMessage(string spoolDir, string failedDir, string safeId) =>
            (ReadSpool(Path.Combine(spoolDir, safeId + ".json")) ?? ReadSpool(Path.Combine(failedDir, safeId + ".json")))?.Message;

        private static SpoolAlarm? ReadSpool(string path)
        {
            try
            {
                return File.Exists(path) ? JsonSerializer.Deserialize<SpoolAlarm>(File.ReadAllText(path), ReadOptions) : null;
            }
            catch (Exception ex) when (ex is JsonException or IOException)
            {
                return null;
            }
        }

        // Same rule as Scada_sms.bat when it names the spool file.
        public static string SafeFileName(string alarmId) =>
            string.Concat(alarmId.Select(c => char.IsAsciiLetterOrDigit(c) || c is '_' or '.' or '-' ? c : '_'));

        private static string MaskPhone(string phone) =>
            phone.Length <= 4 ? "****" : new string('*', phone.Length - 4) + phone[^4..];
    }
}
