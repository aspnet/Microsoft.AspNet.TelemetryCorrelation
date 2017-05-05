﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Diagnostics.Tracing;
#pragma warning disable SA1600 // Elements must be documented

namespace Microsoft.AspNet.TelemetryCorrelation
{
    /// <summary>
    /// ETW EventSource tracing class.
    /// </summary>
    [EventSource(Name = "Microsoft-AspNet-Telemetry-Correlation", Guid = "ace2021e-e82c-5502-d81d-657f27612673")]
    internal sealed class AspNetTelemetryCorrelationEventSource : EventSource
    {
        /// <summary>
        /// Instance of the PlatformEventSource class.
        /// </summary>
        public static readonly AspNetTelemetryCorrelationEventSource Log = new AspNetTelemetryCorrelationEventSource();

        [Event(1, Message = "Callback='{0}'", Level = EventLevel.Verbose)]
        public void TraceCallback(string callback)
        {
            WriteEvent(1, callback);
        }

        [Event(2, Message = "Activity started, Id='{0}'", Level = EventLevel.Verbose)]
        public void ActivityStarted(string id)
        {
            WriteEvent(2, id);
        }

        [Event(3, Message = "Activity stopped, Id='{0}', lost {1}", Level = EventLevel.Verbose)]
        public void ActivityStopped(string id, bool lost = false)
        {
            WriteEvent(3, id, lost);
        }

        [Event(4, Message = "Failed to parse header '{0}', value: '{1}'", Level = EventLevel.Error)]
        public void HeaderParsingError(string headerName, string headerValue)
        {
            WriteEvent(4, headerName, headerValue);
        }

        [Event(5, Message = "Failed to extract activity, reason '{0}'", Level = EventLevel.Error)]
        public void ActvityExtractionError(string reason)
        {
            WriteEvent(5, reason);
        }
    }
}
#pragma warning restore SA1600 // Elements must be documented