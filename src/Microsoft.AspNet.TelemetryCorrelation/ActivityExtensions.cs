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
        public static bool TryParse(this Activity activity, NameValueCollection requestHeaders)
        {
            if (activity == null)
            {
                throw new ArgumentNullException(nameof(activity));
            }

            if (activity.ParentId != null)
            {
                throw new InvalidOperationException("ParentId is already set on activity");
            }

            if (activity.Id != null)
            {
                throw new InvalidOperationException("Activity is already started");
            }

            var requestIDs = requestHeaders.GetValues(RequestIDHeaderName);
            if (requestIDs != null && !string.IsNullOrEmpty(requestIDs[0]))
            {
                try
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
                                    try
                                    {
                                        activity.AddBaggage(baggageItem.Name, baggageItem.Value);
                                    }
                                    catch (ArgumentException)
                                    {
                                        AspNetDiagnosticsEventSource.Log.HeaderParsingFailure(
                                            $"{CorrelationContextHeaderName}: {baggageItem.Name}=", baggageItem.Value);
                                    }
                                }
                                else
                                {
                                    AspNetDiagnosticsEventSource.Log.HeaderParsingFailure(
                                        $"{CorrelationContextHeaderName}: ", pair);
                                }
                            }
                        }
                    }
                }
                catch (ArgumentException)
                {
                    AspNetDiagnosticsEventSource.Log.HeaderParsingFailure($"{RequestIDHeaderName}: ", requestIDs[0]);
                }

                return true;
            }

            return false;
        }
    }
}
