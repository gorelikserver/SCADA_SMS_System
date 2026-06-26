using System.Collections.Concurrent;

namespace SCADASMSSystem.Web.Services
{
    public class SmsCallEntry
    {
        public DateTime Timestamp { get; set; }
        public string Provider { get; set; } = string.Empty;   // REST or SOAP
        public string Endpoint { get; set; } = string.Empty;
        public string HttpMethod { get; set; } = string.Empty;
        public string PhoneNumber { get; set; } = string.Empty; // masked
        public string AlarmId { get; set; } = string.Empty;
        public string RequestSummary { get; set; } = string.Empty; // params with sensitive data masked
        public string FullResponse { get; set; } = string.Empty;
        public int StatusCode { get; set; }
        public bool Success { get; set; }
        public long DurationMs { get; set; }
    }

    public class SmsCallLog
    {
        private const int MaxEntries = 50;
        private readonly ConcurrentQueue<SmsCallEntry> _entries = new();

        public void Add(SmsCallEntry entry)
        {
            _entries.Enqueue(entry);
            while (_entries.Count > MaxEntries)
                _entries.TryDequeue(out _);
        }

        public IReadOnlyList<SmsCallEntry> GetRecent() =>
            _entries.OrderByDescending(e => e.Timestamp).ToList();

        public void Clear()
        {
            while (_entries.TryDequeue(out _)) { }
        }
    }
}
