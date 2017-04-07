using System;
using System.Diagnostics;
using System.Web;

namespace Microsoft.AspNet.TelemetryCorrelation
{
    class TelemetryCorrelationHttpModule : IHttpModule
    {
        private Activity _activity;
        private bool _beginRequestWasCalled;

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
            AspNetDiagnosticsEventSource.Log.TelemetryCorrelationHttpModule("Application_BeginRequest");
            _activity = ActivityHelper.CreateRootActivity(CurrentHttpContext);
            _beginRequestWasCalled = true;
        }

        private void Application_PreRequestHandlerExecute(object sender, EventArgs e)
        {
            AspNetDiagnosticsEventSource.Log.TelemetryCorrelationHttpModule("Application_PreRequestHandlerExecute");
            var context = CurrentHttpContext;
            if (Activity.Current == null && context.Items[ActivityHelper.ActivityKey] is Activity)
            {
                ActivityHelper.RestoreCurrentActivity(context);
            }
        }

        private void Application_EndRequest(object sender, EventArgs e)
        {
            AspNetDiagnosticsEventSource.Log.TelemetryCorrelationHttpModule("Application_EndRequest");

            // EndRequest does it's best effort to notify that request has ended
            var context = CurrentHttpContext;
            // try to stop activity if it's in the Current stack
            if (!ActivityHelper.StopAspNetActivity(_activity, context))
            {
                // Activity started by this module is not in the stack or BeginRequest has never been called
                if (!_beginRequestWasCalled)
                {
                    // Activity has never been started
                    _activity = ActivityHelper.CreateRootActivity(CurrentHttpContext);
                    ActivityHelper.StopAspNetActivity(_activity, context);
                }
                else
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
