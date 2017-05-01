using System;
using System.Diagnostics;
using System.Web;

namespace Microsoft.AspNet.TelemetryCorrelation
{
    class TelemetryCorrelationHttpModule : IHttpModule
    {
        private const string BeginCalledFlag = "Microsoft.AspNet.TelemetryCorrelation.BeginCalled";
        public void Dispose()
        {
        }

        public void Init(HttpApplication context)
        {
            context.BeginRequest += Application_BeginRequest;
            context.EndRequest += Application_EndRequest;
            context.PreRequestHandlerExecute += Application_PreRequestHandlerExecute;
        }

        private HttpContextBase CurrentHttpContext
        {
            get
            {
                Debug.Assert(HttpContext.Current != null);

                return new HttpContextWrapper(HttpContext.Current);
            }
        }

        private void Application_BeginRequest(object sender, EventArgs e)
        {
            var context = CurrentHttpContext;
            AspNetTelemetryCorrelaitonEventSource.Log.TelemetryCorrelationHttpModule("Application_BeginRequest");
            ActivityHelper.CreateRootActivity(CurrentHttpContext);
            context.Items[BeginCalledFlag] = true;
        }

        private void Application_PreRequestHandlerExecute(object sender, EventArgs e)
        {
            AspNetTelemetryCorrelaitonEventSource.Log.TelemetryCorrelationHttpModule("Application_PreRequestHandlerExecute");
            var context = CurrentHttpContext;
            if (Activity.Current == null && context.Items[ActivityHelper.ActivityKey] is Activity)
            {
                ActivityHelper.RestoreCurrentActivity(context);
            }
        }

        private void Application_EndRequest(object sender, EventArgs e)
        {
            AspNetTelemetryCorrelaitonEventSource.Log.TelemetryCorrelationHttpModule("Application_EndRequest");

            var context = CurrentHttpContext;

            // EndRequest does it's best effort to notify that request has ended
            // BeginRequest has never been called
            if (!context.Items.Contains(BeginCalledFlag))
            {
                // Activity has never been started
                var activity = ActivityHelper.CreateRootActivity(CurrentHttpContext);
                ActivityHelper.StopAspNetActivity(activity, context);
            }
            else
            {
                var activity = (Activity)context.Items[ActivityHelper.ActivityKey];
                // try to stop activity if it's in the Current stack
                if (!ActivityHelper.StopAspNetActivity(activity, context))
                {
                    // Activity we created was lost, let's report it
                    var lostActivity = CurrentHttpContext.Items[ActivityHelper.ActivityKey] as Activity;
                    if (lostActivity != null)
                    {
                        ActivityHelper.StopLostActivity(lostActivity, context);
                    }
                }
            }
        }        
    }
}
