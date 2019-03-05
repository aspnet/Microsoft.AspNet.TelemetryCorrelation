// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections;
using System.Diagnostics;
using System.Web;

namespace Microsoft.AspNet.TelemetryCorrelation
{
    /// <summary>
    /// Activity helper class
    /// </summary>
    internal static class ActivityHelper
    {
        /// <summary>
        /// Listener name.
        /// </summary>
        public const string AspNetListenerName = "Microsoft.AspNet.TelemetryCorrelation";

        /// <summary>
        /// Activity name for http request.
        /// </summary>
        public const string AspNetActivityName = "Microsoft.AspNet.HttpReqIn";

        /// <summary>
        /// Event name for the activity start event.
        /// </summary>
        public const string AspNetActivityStartName = "Microsoft.AspNet.HttpReqIn.Start";

        /// <summary>
        /// Event name for the lost activity stop event.
        /// </summary>
        public const string AspNetActivityLostStopName = "Microsoft.AspNet.HttpReqIn.ActivityLost.Stop";

        /// <summary>
        /// Event name for the restored activity stop event.
        /// </summary>
        public const string AspNetActivityRestoredStopName = "Microsoft.AspNet.HttpReqIn.ActivityRestored.Stop";

        /// <summary>
        /// Key to store the activity in HttpContext.
        /// </summary>
        public const string ActivityKey = "__AspnetActivity__";

        /// <summary>
        /// Key to store the restored activity in HttpContext.
        /// </summary>
        public const string RestoredActivityKey = "__AspnetActivityRestored__";

        private const int MaxActivityStackSize = 128;
        private static readonly DiagnosticListener AspNetListener = new DiagnosticListener(AspNetListenerName);

        /// <summary>
        /// Stops the activity and notifies listeners about it.
        /// </summary>
        /// <param name="activity">Activity to stop.</param>
        /// <param name="contextItems">HttpContext.Items.</param>
        /// <returns>True if activity was found in the stack, false otherwise.</returns>
        public static bool StopAspNetActivity(Activity activity, IDictionary contextItems)
        {
            var currentActivity = Activity.Current;
            if (activity != null && currentActivity != null)
            {
                // silently stop all child activities before activity
                int iteration = 0;
                while (currentActivity != activity)
                {
                    currentActivity.Stop();
                    var newCurrentActivity = Activity.Current;

                    if (newCurrentActivity == null)
                    {
                        return false;
                    }

                    // there could be a case when request or any child activity is stopped
                    // from the child execution context. In this case, Activity is present in the Current Stack,
                    // but is finished, i.e. stopping it has no effect on the Current.
                    if (newCurrentActivity == currentActivity)
                    {
                        // We could not reach our 'activity' in the stack and have to report 'lost activity'
                        // if child activity is broken, we can still stop the root one that we own to clean up
                        // all resources
                        AspNetTelemetryCorrelationEventSource.Log.FinishedActivityIsDetected(currentActivity.Id, currentActivity.OperationName);
                        activity.Stop();
                        return false;
                    }

                    // We also protect from endless loop with the MaxActivityStackSize
                    // in case it would ever be possible to have cycles in the Activity stack.
                    if (iteration++ == MaxActivityStackSize)
                    {
                        // this is for internal error reporting
                        AspNetTelemetryCorrelationEventSource.Log.ActivityStackIsTooDeepError();

                        // this is for debugging
                        AspNetTelemetryCorrelationEventSource.Log.ActivityStackIsTooDeepDetails(currentActivity.Id, currentActivity.OperationName);
                        activity.Stop();
                        return false;
                    }

                    currentActivity = newCurrentActivity;
                }

                // if activity is in the stack, stop it with Stop event
                AspNetListener.StopActivity(currentActivity, new { });
                contextItems[ActivityKey] = null;

                AspNetTelemetryCorrelationEventSource.Log.ActivityStopped(currentActivity.Id, currentActivity.OperationName);
                return true;
            }

            return false;
        }

        /// <summary>
        /// Notifies listeners that activity was ended and lost during execution.
        /// </summary>
        /// <param name="activity">Activity to notify about.</param>
        /// <param name="context">Current HttpContext.</param>
        public static void StopLostActivity(Activity activity, HttpContext context)
        {
            context.Items[ActivityKey] = null;
            AspNetListener.Write(AspNetActivityLostStopName, new { activity });
            AspNetTelemetryCorrelationEventSource.Log.ActivityStopped(activity.Id, AspNetActivityLostStopName);
        }

        /// <summary>
        /// Notifies listeners that there the lost activity was lost during execution and there was an intermediate activity.
        /// </summary>
        /// <param name="activity">Activity to notify about.</param>
        /// <param name="context">Current HttpContext.</param>
        public static void StopRestoredActivity(Activity activity, HttpContext context)
        {
            context.Items[RestoredActivityKey] = null;
            AspNetListener.Write(AspNetActivityRestoredStopName, new { Activity = activity });
            AspNetTelemetryCorrelationEventSource.Log.ActivityStopped(activity.Id, AspNetActivityRestoredStopName);
        }

        /// <summary>
        /// Creates root (first level) activity that describes incoming request.
        /// </summary>
        /// <param name="context">Current HttpContext.</param>
        /// <param name="parseHeaders">Determines if headers should be parsed get correlation ids.</param>
        /// <returns>New root activity.</returns>
        public static Activity CreateRootActivity(HttpContext context, bool parseHeaders)
        {
            if (AspNetListener.IsEnabled() && AspNetListener.IsEnabled(AspNetActivityName))
            {
                var rootActivity = new Activity(AspNetActivityName);

                if (parseHeaders)
                {
                    rootActivity.Extract(context.Request.Unvalidated.Headers);
                }

                if (StartAspNetActivity(rootActivity))
                {
                    context.Items[ActivityKey] = rootActivity;
                    AspNetTelemetryCorrelationEventSource.Log.ActivityStarted(rootActivity.Id);
                    return rootActivity;
                }
            }

            return null;
        }

        /// <summary>
        /// Saves activity in the HttpContext.Items.
        /// </summary>
        /// <param name="contextItems">Context to save context to.</param>
        /// <param name="key">Slot name.</param>
        /// <param name="activity">Activity to save.</param>
        internal static void SaveCurrentActivity(IDictionary contextItems, string key, Activity activity)
        {
            Debug.Assert(contextItems != null);
            Debug.Assert(activity != null);

            contextItems[key] = activity;
        }

        /// <summary>
        /// It's possible that a request is executed in both native threads and managed threads,
        /// in such case Activity.Current will be lost during native thread and managed thread switch.
        /// This method is intended to restore the current activity in order to correlate the child
        /// activities with the root activity of the request.
        /// </summary>
        /// <param name="contextItems">HttpContext.Items dictionary.</param>
        internal static void RestoreActivityIfNeeded(IDictionary contextItems)
        {
            if (Activity.Current == null)
            {
                var rootActivity = (Activity)contextItems[ActivityKey];
                if (rootActivity != null && !contextItems.Contains(RestoredActivityKey))
                {
                    contextItems[RestoredActivityKey] = RestoreActivity(rootActivity);
                }
            }
        }

        private static Activity RestoreActivity(Activity root)
        {
            Debug.Assert(root != null);
            Debug.Assert(Activity.Current == null);

            Activity.Current = root;
            AspNetTelemetryCorrelationEventSource.Log.ActivityRestored(root.Id);
            return root;
        }

        private static bool StartAspNetActivity(Activity activity)
        {
            if (AspNetListener.IsEnabled(AspNetActivityName, activity, new { }))
            {
                if (AspNetListener.IsEnabled(AspNetActivityStartName))
                {
                    AspNetListener.StartActivity(activity, new { });
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
