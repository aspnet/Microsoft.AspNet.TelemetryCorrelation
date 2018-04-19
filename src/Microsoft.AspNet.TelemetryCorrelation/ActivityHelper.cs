// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

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
        /// Event name for the activity stop event.
        /// </summary>
        public const string AspNetActivityLostStopName = "Microsoft.AspNet.HttpReqIn.ActivityLost.Stop";

        /// <summary>
        /// Key to store the activity in HttpContext.
        /// </summary>
        public const string ActivityKey = "__AspnetActivity__";

        private const int MaxActivityStackSize = 128;
        private static readonly DiagnosticListener AspNetListener = new DiagnosticListener(AspNetListenerName);

        /// <summary>
        /// It's possible that a request is executed in both native threads and managed threads,
        /// in such case Activity.Current will be lost during native thread and managed thread switch.
        /// This method is intended to restore the current activity in order to correlate the child
        /// activities with the root activity of the request.
        /// </summary>
        /// <param name="root">Root activity id for the current request.</param>
        /// <returns>If it returns an activity, it will be silently stopped with the parent activity</returns>
        public static Activity RestoreCurrentActivity(Activity root)
        {
            Debug.Assert(root != null);

            // workaround to restore the root activity, because we don't
            // have a way to change the Activity.Current
            var childActivity = new Activity(root.OperationName);
            childActivity.SetParentId(root.Id);
            childActivity.SetStartTime(root.StartTimeUtc);
            foreach (var item in root.Baggage)
            {
                childActivity.AddBaggage(item.Key, item.Value);
            }

            childActivity.Start();

            AspNetTelemetryCorrelationEventSource.Log.ActivityRestored(childActivity.Id);
            return childActivity;
        }

        /// <summary>
        /// Stops the activity and notifies listeners about it.
        /// </summary>
        /// <param name="activity">Activity to stop.</param>
        /// <param name="context">Current HttpContext.</param>
        /// <returns>True if activity was found in the stack, false otherwise.</returns>
        public static bool StopAspNetActivity(Activity activity, HttpContext context)
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
                        break;
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
                        AspNetTelemetryCorrelationEventSource.Log.ActivityStackIsTooDeep(currentActivity.Id, currentActivity.OperationName);
                        activity.Stop();
                        return false;
                    }

                    currentActivity = newCurrentActivity;
                }

                // if activity is in the stack, stop it with Stop event
                if (Activity.Current != null)
                {
                    AspNetListener.StopActivity(Activity.Current, new { });
                    RemoveCurrentActivity(context);
                    AspNetTelemetryCorrelationEventSource.Log.ActivityStopped(activity.Id);
                    return true;
                }
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
            if (activity != null)
            {
                AspNetListener.Write(AspNetActivityLostStopName, new { activity });
                RemoveCurrentActivity(context);
                AspNetTelemetryCorrelationEventSource.Log.ActivityStopped(activity.Id, true);
            }
        }

        /// <summary>
        /// Creates root (first level) activity that describes incoming request.
        /// </summary>
        /// <param name="context">Current HttpContext.</param>
        /// <returns>New root activity.</returns>
        public static Activity CreateRootActivity(HttpContext context)
        {
            if (AspNetListener.IsEnabled() && AspNetListener.IsEnabled(AspNetActivityName))
            {
                var rootActivity = new Activity(ActivityHelper.AspNetActivityName);

                rootActivity.Extract(context.Request.Unvalidated.Headers);
                if (StartAspNetActivity(rootActivity))
                {
                    SaveCurrentActivity(context, rootActivity);
                    AspNetTelemetryCorrelationEventSource.Log.ActivityStarted(rootActivity.Id);
                    return rootActivity;
                }
            }

            return null;
        }

        /// <summary>
        /// This should be called after the Activity starts and only for root activity of a request.
        /// </summary>
        /// <param name="context">Context to save context to.</param>
        /// <param name="activity">Activity to save.</param>
        internal static void SaveCurrentActivity(HttpContext context, Activity activity)
        {
            Debug.Assert(context != null);
            Debug.Assert(activity != null);

            context.Items[ActivityKey] = activity;
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

        private static void RemoveCurrentActivity(HttpContext context)
        {
            Debug.Assert(context != null);
            context.Items[ActivityKey] = null;
        }
    }
}
