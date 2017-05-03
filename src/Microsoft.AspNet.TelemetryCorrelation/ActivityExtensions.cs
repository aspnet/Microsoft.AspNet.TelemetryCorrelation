using System;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Diagnostics;
using System.Net.Http.Headers;

namespace Microsoft.AspNet.TelemetryCorrelation
{
    /// <summary>
    /// Extensions of Activity class
    /// </summary>
    [EditorBrowsable(EditorBrowsableState.Never)]
    public static class ActivityExtensions
    {
        internal const string RequestIDHeaderName = "Request-Id";
        internal const string CorrelationContextHeaderName = "Correlation-Context";

        /// <summary>
        /// Reads Request-Id and Correlation-Context headers and sets ParentId and Baggage on Activity.
        /// </summary>
        /// <param name="activity">Instance of activity that has not been started yet.</param>
        /// <param name="requestHeaders">Request headers collection.</param>
        public static bool Extract(this Activity activity, NameValueCollection requestHeaders)
        {
            if (activity == null)
            {
                AspNetTelemetryCorrelationEventSource.Log.ActvityExtractionError("activity is null");
                return false;
            }

            if (activity.ParentId != null)
            {
                AspNetTelemetryCorrelationEventSource.Log.ActvityExtractionError("ParentId is already set on activity");
                return false;
            }

            if (activity.Id != null)
            {
                AspNetTelemetryCorrelationEventSource.Log.ActvityExtractionError("Activity is already started");
                return false;
            }

            var requestIDs = requestHeaders.GetValues(RequestIDHeaderName);
            if (!string.IsNullOrEmpty(requestIDs?[0]))
            {
                // there may be several Request-Id header, but we only read the first one
                activity.SetParentId(requestIDs[0]);

                // Header format - Correlation-Context: key1=value1, key2=value2 
                var baggages = requestHeaders.GetValues(CorrelationContextHeaderName);
                if (baggages != null)
                {
                    // there may be several Correlation-Context header 
                    foreach (var item in baggages)
                    {
                        foreach (var pair in item.Split(','))
                        {
                            NameValueHeaderValue baggageItem;
                            if (NameValueHeaderValue.TryParse(pair, out baggageItem))
                            {
                                activity.AddBaggage(baggageItem.Name, baggageItem.Value);
                            }
                            else
                            {
                                AspNetTelemetryCorrelationEventSource.Log.HeaderParsingError(CorrelationContextHeaderName, pair);
                            }
                        }
                    }
                }

                return true;
            }

            return false;
        }


        /// <summary>
        /// 
        /// </summary>
        [Obsolete("Method is obsolete, use Extract method instead", true)]
        [EditorBrowsable(EditorBrowsableState.Never)]
        public static bool TryParse(this Activity activity, NameValueCollection requestHeaders)
        {
            return Extract(activity, requestHeaders);
        }
    }
}
