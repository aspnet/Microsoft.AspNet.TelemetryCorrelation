// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Diagnostics;
using Microsoft.AspNet.TelemetryCorrelation;

namespace Microsoft.Owin.TelemetryCorrelation
{
    /// <summary>
    /// Activity helper class
    /// </summary>
    internal static class ActivityHelper
    {
        /// <summary>
        /// Listener name.
        /// </summary>
        public const string OwinListenerName = "Microsoft.Owin.TelemetryCorrelation";

        /// <summary>
        /// Activity name for http request.
        /// </summary>
        public const string OwinActivityName = "Microsoft.Owin.HttpReqIn";

        /// <summary>
        /// Event name for the activity start event.
        /// </summary>
        public const string OwinActivityStartName = "Microsoft.Owin.HttpReqIn.Start";

        private static readonly DiagnosticListener OwinListener = new DiagnosticListener(OwinListenerName);

        private static readonly object EmptyPayload = new object();

        /// <summary>
        /// Creates root (first level) activity that describes incoming request.
        /// </summary>
        /// <param name="request">Inbound HTTP request.</param>
        public static void CreateRootActivity(IOwinRequest request)
        {
            if (OwinListener.IsEnabled() && OwinListener.IsEnabled(OwinActivityName))
            {
                var rootActivity = new Activity(OwinActivityName);

                rootActivity.Extract(new HeaderDictionaryStore(request.Headers));

                OwinListener.OnActivityImport(rootActivity, null);

                if (StartAspNetActivity(rootActivity))
                {
                    AspNetTelemetryCorrelationEventSource.Log.ActivityStarted(rootActivity.Id);
                }
            }
        }

        /// <summary>
        /// Stops the activity and notifies listeners about it.
        /// </summary>
        public static void StopOwinActivity()
        {
            var currentActivity = Activity.Current;
            if (currentActivity != null)
            {
                // stop Activity with Stop event
                OwinListener.StopActivity(currentActivity, EmptyPayload);
            }

            AspNetTelemetryCorrelationEventSource.Log.ActivityStopped(currentActivity?.Id, currentActivity?.OperationName);
        }

        private static bool StartAspNetActivity(Activity activity)
        {
            if (OwinListener.IsEnabled(OwinActivityName, activity, EmptyPayload))
            {
                if (OwinListener.IsEnabled(OwinActivityStartName))
                {
                    OwinListener.StartActivity(activity, EmptyPayload);
                }
                else
                {
                    activity.Start();
                }

                return true;
            }

            return false;
        }
    }
}