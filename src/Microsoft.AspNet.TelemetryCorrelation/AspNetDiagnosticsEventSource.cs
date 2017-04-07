using System.Diagnostics.Tracing;

namespace Microsoft.AspNet.TelemetryCorrelation
{
    /// <summary>
    /// ETW EventSource tracing class.
    /// </summary>
    [EventSource(Name = "Microsoft-AspNet-Diagnostics", Guid = "e73033a3-24b2-4076-8f8a-2c0795b00746")]
    internal sealed class AspNetDiagnosticsEventSource : EventSource
    {
        /// <summary>
        /// Instance of the PlatformEventSource class.
        /// </summary>
        public static readonly AspNetDiagnosticsEventSource Log = new AspNetDiagnosticsEventSource();

        [Event(1, Message = "[TelemetryCorrelationHttpModule];Callback='{0}'", Level = EventLevel.Informational)]
        public void TelemetryCorrelationHttpModule(string callback)
        {
            if (IsEnabled())
            {
                WriteEvent(1, callback);
            }
        }

        [Event(3, Message = "[TelemetryCorrelationHttpModule];Activity started, Id='{0}'", Level = EventLevel.Informational)]
        public void ActivityStarted(string id)
        {
            if (IsEnabled())
            {
                WriteEvent(3, id);
            }
        }

        [Event(4, Message = "[TelemetryCorrelationHttpModule];Activity stopped, Id='{0}', lost {1}", Level = EventLevel.Informational)]
        public void ActivityStopped(string id, bool lost = false)
        {
            if (IsEnabled())
            {
                WriteEvent(4, id, lost);
            }
        }

        [Event(5, Message = "[TelemetryCorrelationHttpModule];Failed to parse header {0}, value: '{1}'", Level = EventLevel.Error)]
        public void HeaderParsingFailure(string headerName, string headerValue)
        {
            if (IsEnabled())
            {
                WriteEvent(5, headerName, headerValue);
            }
        }
    }
}
