using System.Collections.Specialized;
using System.Diagnostics;

namespace Microsoft.AspNet.TelemetryCorrelation
{
    /// <summary>
    /// Extensions of Activity class
    /// </summary>
    public static partial class ActivityExtensions
    {
        /// <summary>
        /// Reads Request-Id and Correlation-Context headers and sets ParentId and Baggage on Activity.
        /// </summary>
        /// <param name="activity">Instance of activity that has not been started yet.</param>
        /// <param name="requestHeaders">Request headers collection.</param>
        /// <returns>true if request was parsed successfully, false - otherwise.</returns>
        public static bool Extract(this Activity activity, NameValueCollection requestHeaders)
            => Extract(activity, new NameValueCollectionHeaderStore(requestHeaders));
    }
}