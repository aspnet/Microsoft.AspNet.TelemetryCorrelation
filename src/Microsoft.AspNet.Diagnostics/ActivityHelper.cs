using System.Diagnostics;
using System.Web;

namespace Microsoft.AspNet.Diagnostics
{
    /// <summary>
    /// Activity helper class
    /// </summary>
    internal static class ActivityHelper
    {
        public const string AspNetListenerName = "Microsoft.AspNet.Diagnostics";
        public const string AspNetActivityName = "Microsoft.AspNet.HttpReqIn";
        public const string AspNetActivityStartName = "Microsoft.AspNet.HttpReqIn.Start";
        public const string AspNetActivityLostStopName = "Microsoft.AspNet.HttpReqIn.Lost.Stop";
        public const string AspNetExceptionName = "Microsoft.AspNet.HttpReqIn.Exception";

        public const string ActivityKey = "__AspnetActivity__";
        private static DiagnosticListener s_aspNetListener = new DiagnosticListener(AspNetListenerName);

        /// <summary>
        /// It's possible that a request is executed in both native threads and managed threads,
        /// in such case Activity.Current will be lost during native thread and managed thread swtich.
        /// This method is intended to restore the current activity in order to correlate the child
        /// activities with the root activity of the request.
        /// </summary>
        /// <returns>If it returns an activity, it will be silently stopped with the parent activity</returns>
        public static Activity RestoreCurrentActivity(HttpContextBase context)
        {
            Debug.Assert(Activity.Current == null && context.Items[ActivityKey] is Activity);

            // workaround to restore the root activity, because we don't
            // have a way to change the Activity.Current
            var root = (Activity)context.Items[ActivityKey];
            var childActivity = new Activity(root.OperationName);
            childActivity.SetParentId(root.Id);
            childActivity.SetStartTime(root.StartTimeUtc);
            foreach(var item in root.Baggage)
            {
                childActivity.AddBaggage(item.Key, item.Value);
            }
            childActivity.Start();

            AspNetDiagnosticsEventSource.Log.ActivityStarted(childActivity.Id);
            return childActivity;
        }

        public static bool StopAspNetActivity(Activity activity, HttpContextBase context)
        {
            if (activity != null && Activity.Current != null)
            {
                // silently stop all child activities before activity
                while (Activity.Current != activity && Activity.Current != null)
                {
                    Activity.Current.Stop();
                }

                // if activity is in the stack, stop it with Stop event
                if (Activity.Current != null)
                {
                    s_aspNetListener.StopActivity(Activity.Current, new { });
                    RemoveCurrentActivity(context);
                    AspNetDiagnosticsEventSource.Log.ActivityStopped(activity.Id);
                    return true;
                }
            }

            return false;
        }

        public static void StopLostActivity(Activity activity, HttpContextBase context)
        {
            if (activity != null)
            {
                s_aspNetListener.Write(AspNetActivityLostStopName, new { activity });
                RemoveCurrentActivity(context);
                AspNetDiagnosticsEventSource.Log.ActivityStopped(activity.Id, true);
            }
        }

        public static Activity CreateRootActivity(HttpContextBase context)
        {
            if (s_aspNetListener.IsEnabled() && s_aspNetListener.IsEnabled(AspNetActivityName))
            {
                var rootActivity = new Activity(ActivityHelper.AspNetActivityName);

                rootActivity.TryParse(context.Request.Headers);
                if (StartAspNetActivity(rootActivity))
                {
                    SaveCurrentActivity(context, rootActivity);
                    AspNetDiagnosticsEventSource.Log.ActivityStarted(rootActivity.Id);
                    return rootActivity;
                }
            }
            return null;
        }

        private static bool StartAspNetActivity(Activity activity)
        {
            if (s_aspNetListener.IsEnabled(AspNetActivityName, activity, new { }))
            {
                if (s_aspNetListener.IsEnabled(AspNetActivityStartName))
                {
                    s_aspNetListener.StartActivity(activity, new { });
                }
                else
                {
                    activity.Start();
                }
                return true;
            }

            return false;
        }

        /// <summary>
        /// This should be called after the Activity starts
        /// and only for root activity of a request
        /// </summary>
        private static void SaveCurrentActivity(HttpContextBase context, Activity activity)
        {
            Debug.Assert(context != null);
            Debug.Assert(activity != null);

            context.Items[ActivityKey] = activity;
        }

        private static void RemoveCurrentActivity(HttpContextBase context)
        {
            Debug.Assert(context != null);
            context.Items[ActivityKey] = null;
        }
    }
}
