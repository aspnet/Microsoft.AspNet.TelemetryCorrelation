using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using System.Web;

namespace Microsoft.AspNet.CorrelationActivity
{
    class ActivityTrackingModule : IHttpModule
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
            // if some other module creates the activity, we do nothing.
            if(Activity.Current != null || HttpContext.Current.Items[ActivityHelper.ActivityKey] != null)
            {
                _shouldCreateRootActivity = false;
                return;
            }
            _activity = ActivityHelper.CreateRootActivity(CurrentHttpContext);
        }

        private void Application_PreRequestHandlerExecute(object sender, EventArgs e)
        {
            if (_shouldCreateRootActivity)
            {
                _rootActivityInHandlerExecution = ActivityHelper.RestoreCurrentActivity(CurrentHttpContext);
            }
        }

        private void Application_PostRequestHandlerExecute(object sender, EventArgs e)
        {
            if(_shouldCreateRootActivity && _rootActivityInHandlerExecution != null)
            {
                _rootActivityInHandlerExecution.Stop();
            }
        }

        private void Application_Error(object sender, EventArgs e)
        {
            if (_shouldCreateRootActivity)
            {
                // In case unhandled exception is thrown before PreRequestHandlerExecute
                var currentActivity = ActivityHelper.RestoreCurrentActivity(CurrentHttpContext);
                ActivityHelper.WriteExceptionToDiagnosticSource(CurrentHttpContext);
                ActivityHelper.StopAspNetActivity(currentActivity);

                // In case unhandled exception is thrown during handler executing, which won't
                // trigger PostRequestHandlerExecut event.
                if (_rootActivityInHandlerExecution != null)
                {
                    _rootActivityInHandlerExecution.Stop();
                }
            }
        }

        private void Application_EndRequest(object sender, EventArgs e)
        {
            if(_shouldCreateRootActivity)
            {
                ActivityHelper.StopAspNetActivity(_activity);
            }
        }        
    }
}
