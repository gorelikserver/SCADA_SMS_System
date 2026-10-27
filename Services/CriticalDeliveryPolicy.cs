using System.Linq.Expressions;
using SCADASMSSystem.Web.Models;

namespace SCADASMSSystem.Web.Services
{
    /// <summary>
    /// Retry rules for critical alarms. A duplicate SMS is preferred over a missed alarm, so any
    /// recipient not confirmed as accepted may be sent again with the same AlarmId.
    /// </summary>
    public static class CriticalDeliveryPolicy
    {
        // A SENDING row older than this belongs to a request that crashed or was killed.
        // Must exceed the longest legitimate single-recipient send (timeout x attempts + backoff).
        public static readonly TimeSpan StaleSendingAfter = TimeSpan.FromMinutes(5);

        /// <summary>
        /// Rows a request may claim for sending: failed, indeterminate (timeout or transport loss,
        /// the provider may or may not have sent it), or stuck in SENDING longer than
        /// <see cref="StaleSendingAfter"/>. Used directly in the EF claim query.
        /// </summary>
        public static Expression<Func<CriticalSmsDelivery, bool>> Claimable(DateTime staleBefore) =>
            d => d.Status == CriticalDeliveryStatus.Failed ||
                 d.Status == CriticalDeliveryStatus.Indeterminate ||
                 (d.Status == CriticalDeliveryStatus.Sending && d.UpdatedAt < staleBefore);

        public static DateTime StaleBefore(DateTime now) => now - StaleSendingAfter;

        /// <summary>
        /// Retry inside one request for timeouts and transport errors (status 0), throttling (429)
        /// and server errors (5xx). Other 4xx and failure patterns on 2xx are permanent.
        /// </summary>
        public static bool IsRetryable(SmsApiResponse response) =>
            !response.Success &&
            (response.StatusCode == 0 || response.StatusCode == 429 || response.StatusCode >= 500);
    }
}
