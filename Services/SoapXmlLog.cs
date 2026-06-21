using System.Collections.Concurrent;

namespace SCADASMSSystem.Web.Services
{
    public class SoapXmlLog
    {
        public record Entry(DateTime Timestamp, string MaskedPhone, string Xml);

        private readonly ConcurrentQueue<Entry> _entries = new();
        private const int MaxEntries = 100;

        public void Push(string maskedPhone, string xml)
        {
            _entries.Enqueue(new Entry(DateTime.Now, maskedPhone, xml));
            while (_entries.Count > MaxEntries)
                _entries.TryDequeue(out _);
        }

        public IReadOnlyList<Entry> Entries => _entries.ToArray();

        public void Clear() => _entries.Clear();
    }
}
