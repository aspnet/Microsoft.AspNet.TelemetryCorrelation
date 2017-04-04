using System;
using System.Diagnostics;
using System.Web;

namespace Microsoft.AspNet.Diagnostics
{
    class DiagnosticsHttpModule : IHttpModule
    {   
        private Activity _activity;
        private Activity _rootActivityInHandlerExecution;
        private bool _shouldCreateRootActivity = true;

        public void Dispose()
        {
        }

        public void Init(HttpApplication context)
        {
            context.BeginRequest += Application_BeginRequest;
            context.EndRequest += Application_EndRequest;
            context.PreRequestHandlerExecute += Application_PreRequestHandlerExecute;
            context.PostRequestHandlerExecute += Application_PostRequestHandlerExecute;
            context.Error += Application_Error;
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
            AspNetDiagnosticsEventSource.Log.RequestTrackingModule("Application_BeginRequest");

            // if some other module creates the activity, we do nothing.
            if (Activity.Current != null)
            {
                if (HttpContext.Current.Items[ActivityHelper.ActivityKey] == null)
                {
                    // TODO: comment
                    HttpContext.Current.Items.Add(ActivityHelper.ActivityKey, Activity.Current);
                }

                _shouldCreateRootActivity = false;
                return;
            }
            _activity = ActivityHelper.CreateRootActivity(CurrentHttpContext);
        }

        private void Application_PreRequestHandlerExecute(object sender, EventArgs e)
        {
            AspNetDiagnosticsEventSource.Log.RequestTrackingModule("Application_PreRequestHandlerExecute");

            if (_shouldCreateRootActivity)
            {
                _rootActivityInHandlerExecution = ActivityHelper.RestoreCurrentActivity(CurrentHttpContext);
            }
        }

        private void Application_PostRequestHandlerExecute(object sender, EventArgs e)
        {
            AspNetDiagnosticsEventSource.Log.RequestTrackingModule("Application_PostRequestHandlerExecute");

            if (_shouldCreateRootActivity && _rootActivityInHandlerExecution != null)
            {
                _rootActivityInHandlerExecution.Stop();
                AspNetDiagnosticsEventSource.Log.ActivityStopped(_rootActivityInHandlerExecution.Id);
            }
        }

        private void Application_Error(object sender, EventArgs e)
        {
            AspNetDiagnosticsEventSource.Log.RequestTrackingModule("Application_Error");

            if (Activity.Current == null && HttpContext.Current.Items[ActivityHelper.ActivityKey] == null)
            {
                // Exception happened before BeginRequest
                _activity = ActivityHelper.CreateRootActivity(CurrentHttpContext);
            }

            if (_shouldCreateRootActivity)
            {
                // In case unhandled exception is thrown before PreRequestHandlerExecute
                var currentActivity = ActivityHelper.RestoreCurrentActivity(CurrentHttpContext);

                ActivityHelper.WriteExceptionToDiagnosticSource(CurrentHttpContext);
                ActivityHelper.StopAspNetActivity(currentActivity);

                // In case unhandled exception is thrown during handler executing, which won't
                // trigger PostRequestHandlerExecute event.
                if (_rootActivityInHandlerExecution != null)
                {
                    _rootActivityInHandlerExecution.Stop();
                }
            }
        }

        private void Application_EndRequest(object sender, EventArgs e)
        {
            AspNetDiagnosticsEventSource.Log.RequestTrackingModule("Application_EndRequest");
            if (_shouldCreateRootActivity)
            {
                ActivityHelper.StopAspNetActivity(_activity);
            }
        }        
    }
}
