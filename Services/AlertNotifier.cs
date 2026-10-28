using System.Diagnostics;

namespace SCADASMSSystem.Web.Services
{
    /// <summary>
    /// Raises delivery problems outside the app, so site IT sees them even when nobody opens the UI.
    /// Akara ignores the batch exit code, so this is the only push signal for a missed alarm.
    /// </summary>
    public interface IAlertNotifier
    {
        void DeliveryIncomplete(string alarmId, int groupId, int accepted, int recipients);
        void SpoolGaveUp(string alarmId, int groupId, int maxAgeMinutes);
        void SpoolFileRejected(string fileName, string reason);
    }

    /// <summary>
    /// Writes to the Windows Application event log. Serilog replaces the MEL providers
    /// (UseSerilog), so ILogger calls never reach the event log; this writes directly.
    /// </summary>
    public class EventLogAlertNotifier : IAlertNotifier
    {
        public const string SourceName = "SCADA SMS System";
        public const int DeliveryIncompleteEventId = 1001;
        public const int SpoolGaveUpEventId = 1002;
        public const int SpoolFileRejectedEventId = 1003;

        private readonly ILogger<EventLogAlertNotifier> _logger;
        private bool _sourceReady;

        public EventLogAlertNotifier(ILogger<EventLogAlertNotifier> logger)
        {
            _logger = logger;
        }

        public void DeliveryIncomplete(string alarmId, int groupId, int accepted, int recipients) =>
            Write(EventLogEntryType.Warning, DeliveryIncompleteEventId,
                recipients == 0
                    ? $"Critical alarm {alarmId} for group {groupId} has no eligible recipients. It stays in the spool and is retried."
                    : $"Critical alarm {alarmId} for group {groupId}: only {accepted} of {recipients} recipients accepted. Failed recipients are retried from the spool.");

        public void SpoolGaveUp(string alarmId, int groupId, int maxAgeMinutes) =>
            Write(EventLogEntryType.Error, SpoolGaveUpEventId,
                $"Critical alarm {alarmId} for group {groupId} was NOT delivered to every recipient after {maxAgeMinutes} minutes. Retries stopped; see the Alerts page.");

        public void SpoolFileRejected(string fileName, string reason) =>
            Write(EventLogEntryType.Error, SpoolFileRejectedEventId,
                $"Spool file {fileName} could not be read ({reason}). The alarm was NOT sent; see the Alerts page.");

        private void Write(EventLogEntryType type, int eventId, string message)
        {
            if (!OperatingSystem.IsWindows())
                return;

            try
            {
                if (!_sourceReady)
                {
                    // Creating a source needs admin rights; the service account (LocalSystem) has them.
                    if (!EventLog.SourceExists(SourceName))
                        EventLog.CreateEventSource(SourceName, "Application");
                    _sourceReady = true;
                }
                EventLog.WriteEntry(SourceName, message, type, eventId);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Could not write to the Windows event log: {Message}", message);
            }
        }
    }
}
