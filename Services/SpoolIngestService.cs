using System.Text.Json;
using Microsoft.Extensions.Options;

namespace SCADASMSSystem.Web.Services
{
    /// <summary>
    /// Settings for the alarm spool written by Scada_sms.bat. Root must match the
    /// SPOOL_ROOT the batch file uses (default %ProgramData%\SCADA_SMS).
    /// </summary>
    public class SpoolOptions
    {
        public string Root { get; set; } = string.Empty;
        public int PollSeconds { get; set; } = 30;

        // Skip files younger than this: the batch may still be waiting on its own HTTP call.
        // Must be longer than the batch MAXTIME (default 120 s).
        public int MinAgeSeconds { get; set; } = 150;

        // After this long without full acceptance the alarm moves to spool\failed.
        public int MaxAgeMinutes { get; set; } = 60;

        public string ResolveRoot() => string.IsNullOrWhiteSpace(Root)
            ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "SCADA_SMS")
            : Root;
    }

    public record SpoolAlarm(string Message, int GroupId, string AlarmId, string? Priority, string? Value, DateTime? ReceivedAt);

    public record SpoolPassResult(int Delivered, int Pending, int Failed, int Bad);

    /// <summary>
    /// One pass over the spool folder. Every alarm left behind by the batch file is re-sent with
    /// its original AlarmId, so recipients already accepted are skipped and only failed ones retry.
    /// </summary>
    public class SpoolProcessor
    {
        private static readonly JsonSerializerOptions ReadOptions = new() { PropertyNameCaseInsensitive = true };

        private readonly SpoolOptions _options;
        private readonly Func<SpoolAlarm, CancellationToken, Task<CriticalSmsDeliveryResult>> _send;
        private readonly ILogger _logger;

        public SpoolProcessor(
            SpoolOptions options,
            Func<SpoolAlarm, CancellationToken, Task<CriticalSmsDeliveryResult>> send,
            ILogger logger)
        {
            _options = options;
            _send = send;
            _logger = logger;
        }

        public string SpoolDirectory => Path.Combine(_options.ResolveRoot(), "spool");
        public string FailedDirectory => Path.Combine(SpoolDirectory, "failed");
        public string BadDirectory => Path.Combine(SpoolDirectory, "bad");

        public async Task<SpoolPassResult> ProcessOnceAsync(DateTime nowUtc, CancellationToken ct = default)
        {
            int delivered = 0, pending = 0, failed = 0, bad = 0;
            if (!Directory.Exists(SpoolDirectory))
                return new SpoolPassResult(0, 0, 0, 0);

            foreach (var file in Directory.GetFiles(SpoolDirectory, "*.json").OrderBy(f => f, StringComparer.Ordinal))
            {
                ct.ThrowIfCancellationRequested();

                var writtenUtc = File.GetLastWriteTimeUtc(file);
                if (nowUtc - writtenUtc < TimeSpan.FromSeconds(_options.MinAgeSeconds))
                    continue;

                SpoolAlarm? alarm;
                try
                {
                    alarm = JsonSerializer.Deserialize<SpoolAlarm>(await File.ReadAllTextAsync(file, ct), ReadOptions);
                }
                catch (JsonException ex)
                {
                    _logger.LogError(ex, "Spool file {File} is not valid JSON; moved to bad", Path.GetFileName(file));
                    MoveTo(file, BadDirectory);
                    bad++;
                    continue;
                }

                if (alarm == null || string.IsNullOrWhiteSpace(alarm.Message) || alarm.GroupId <= 0 ||
                    string.IsNullOrWhiteSpace(alarm.AlarmId))
                {
                    _logger.LogError("Spool file {File} is missing Message, GroupId or AlarmId; moved to bad", Path.GetFileName(file));
                    MoveTo(file, BadDirectory);
                    bad++;
                    continue;
                }

                CriticalSmsDeliveryResult? result = null;
                try
                {
                    result = await _send(alarm, ct);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    _logger.LogError(ex, "Spooled alarm {AlarmId} could not be sent; will retry", alarm.AlarmId);
                }

                if (result?.Success == true)
                {
                    _logger.LogInformation("Spooled alarm {AlarmId} accepted for all {Count} recipients", alarm.AlarmId, result.RecipientCount);
                    File.Delete(file);
                    delivered++;
                    continue;
                }

                var receivedUtc = alarm.ReceivedAt?.ToUniversalTime() ?? writtenUtc;
                if (nowUtc - receivedUtc >= TimeSpan.FromMinutes(_options.MaxAgeMinutes))
                {
                    _logger.LogCritical(
                        "Spooled alarm {AlarmId} for group {GroupId} gave up after {Minutes} min: {Accepted}/{Count} recipients accepted",
                        alarm.AlarmId, alarm.GroupId, _options.MaxAgeMinutes, result?.AcceptedCount ?? 0, result?.RecipientCount ?? 0);
                    MoveTo(file, FailedDirectory);
                    failed++;
                }
                else
                {
                    _logger.LogWarning(
                        "Spooled alarm {AlarmId} not fully accepted ({Accepted}/{Count}); will retry",
                        alarm.AlarmId, result?.AcceptedCount ?? 0, result?.RecipientCount ?? 0);
                    pending++;
                }
            }

            return new SpoolPassResult(delivered, pending, failed, bad);
        }

        private static void MoveTo(string file, string directory)
        {
            Directory.CreateDirectory(directory);
            File.Move(file, Path.Combine(directory, Path.GetFileName(file)), overwrite: true);
        }
    }

    /// <summary>Hosted loop that runs <see cref="SpoolProcessor"/> on startup and every PollSeconds.</summary>
    public class SpoolIngestService : BackgroundService
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly IOptionsMonitor<SpoolOptions> _options;
        private readonly ILogger<SpoolIngestService> _logger;

        public SpoolIngestService(
            IServiceScopeFactory scopeFactory,
            IOptionsMonitor<SpoolOptions> options,
            ILogger<SpoolIngestService> logger)
        {
            _scopeFactory = scopeFactory;
            _options = options;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                var options = _options.CurrentValue;
                try
                {
                    var processor = new SpoolProcessor(options, SendAsync, _logger);
                    await processor.ProcessOnceAsync(DateTime.UtcNow, stoppingToken);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Spool pass failed");
                }

                try
                {
                    await Task.Delay(TimeSpan.FromSeconds(Math.Max(5, options.PollSeconds)), stoppingToken);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
            }
        }

        private async Task<CriticalSmsDeliveryResult> SendAsync(SpoolAlarm alarm, CancellationToken ct)
        {
            using var scope = _scopeFactory.CreateScope();
            var smsService = scope.ServiceProvider.GetRequiredService<ISmsService>();
            return await smsService.SendCriticalSmsToGroupAsync(
                alarm.Message, alarm.GroupId, alarm.AlarmId, alarm.Priority, alarm.Value, ct);
        }
    }
}
