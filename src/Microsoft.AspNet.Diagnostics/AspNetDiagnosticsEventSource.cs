using System.Diagnostics.Tracing;
using System.Web.UI.WebControls;

namespace Microsoft.AspNet.Diagnostics
{
    /// <summary>
    /// ETW EventSource tracing class.
    /// </summary>
    [EventSource(Name = "Microsoft-AspNet-Diagnostics", Guid = "B72742AA-ED2D-407A-ABA3-9DF2522B3B1F")]
    internal sealed class AspNetDiagnosticsEventSource : EventSource
    {
        /// <summary>
        /// Instance of the PlatformEventSource class.
        /// </summary>
        public static readonly AspNetDiagnosticsEventSource Log = new AspNetDiagnosticsEventSource();

        [Event(1, Message = "DiagnosticsHttpModule.{0}", Level = EventLevel.Informational)]
        public void DiagnosticsHttpModule(string callback)
        {
            if(IsEnabled())
            {
                WriteEvent(1, callback);
            }
        }

        [Event(3, Message = "Activity started, Id='{0}'", Level = EventLevel.Informational)]
        public void ActivityStarted(string id)
        {
            if (IsEnabled())
            {
                WriteEvent(3, id);
            }
        }

        [Event(4, Message = "Activity stopped, Id='{0}', lost {1}", Level = EventLevel.Informational)]
        public void ActivityStopped(string id, bool lost = false)
        {
            if (IsEnabled())
            {
                WriteEvent(4, id, lost);
            }
        }

        [Event(5, Message = "Failed to parse header {0}, value: '{1}'", Level = EventLevel.Error)]
        public void HeaderParsingFailure(string headerName, string headerValue)
        {
            if (IsEnabled())
            {
                WriteEvent(5, headerName, headerValue);
            }
        }
    }
}
