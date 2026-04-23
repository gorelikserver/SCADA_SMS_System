using System.Collections.Concurrent;
using SCADASMSSystem.Web.Models;

namespace SCADASMSSystem.Web.Services
{
    public class MockSmsService : IMockSmsService
    {
        private static readonly ConcurrentQueue<MockSmsLogEntry> _log = new();
        private static int _totalSent;
        private static int _totalFailed;
        private const int MaxLogEntries = 100;

        public int TotalSent => _totalSent;
        public int TotalFailed => _totalFailed;

        public Task<SmsApiResponse> SimulateSmsAsync(string message, SmsRecipient recipient, string? providerType)
        {
            var phone = recipient.PhoneNumber;
            var masked = MaskPhone(phone);
            var provider = providerType ?? "REST";

            // 3% random failure rate
            var fail = Random.Shared.NextDouble() < 0.03;
            var msgId = $"MOCK-{DateTime.UtcNow:yyyyMMddHHmmssff}-{Random.Shared.Next(1000, 9999)}";

            var entry = new MockSmsLogEntry(
                Timestamp: DateTime.Now,
                MaskedPhone: masked,
                Message: message.Length > 120 ? message[..120] + "…" : message,
                ProviderType: provider,
                Success: !fail,
                MockMessageId: msgId
            );

            Enqueue(entry);

            if (fail)
            {
                Interlocked.Increment(ref _totalFailed);
                return Task.FromResult(new SmsApiResponse
                {
                    Success = false,
                    Message = "[TEST MODE] Simulated failure (3% rate)",
                    Response = $"{{\"mock\":true,\"success\":false,\"messageId\":\"{msgId}\"}}",
                    StatusCode = 500
                });
            }

            Interlocked.Increment(ref _totalSent);
            return Task.FromResult(new SmsApiResponse
            {
                Success = true,
                Message = "[TEST MODE] Simulated success",
                Response = $"{{\"mock\":true,\"success\":true,\"messageId\":\"{msgId}\"}}",
                StatusCode = 200
            });
        }

        public IReadOnlyList<MockSmsLogEntry> GetRecentLog()
        {
            return _log.Reverse().Take(20).ToList();
        }

        public void Reset()
        {
            while (_log.TryDequeue(out _)) { }
            Interlocked.Exchange(ref _totalSent, 0);
            Interlocked.Exchange(ref _totalFailed, 0);
        }

        private static void Enqueue(MockSmsLogEntry entry)
        {
            _log.Enqueue(entry);
            while (_log.Count > MaxLogEntries)
                _log.TryDequeue(out _);
        }

        private static string MaskPhone(string phone)
        {
            if (string.IsNullOrEmpty(phone) || phone.Length < 4) return phone;
            if (phone.Length <= 6) return $"{phone[..2]}***{phone[^2..]}";
            return $"{phone[..3]}***{phone[^3..]}";
        }
    }
}
