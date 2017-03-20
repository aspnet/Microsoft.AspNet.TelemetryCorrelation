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
                HttpContextWrapper context = null;
                if (HttpContext.Current != null)
                {
                    context = new HttpContextWrapper(HttpContext.Current);
                }
                return context;
            }
        }

        private void Application_BeginRequest(object sender, EventArgs e)
        {
            _activity = ActivityHelper.CreateRootActivity(CurrentHttpContext);
        }

        private void Application_PreRequestHandlerExecute(object sender, EventArgs e)
        {
            _rootActivityInHandlerExecution = ActivityHelper.RestoreCurrentActivity(CurrentHttpContext);
        }

        private void Application_PostRequestHandlerExecute(object sender, EventArgs e)
        {
            ActivityHelper.StopAspNetActivity(_rootActivityInHandlerExecution, new { Context = CurrentHttpContext });
        }

        private void Application_Error(object sender, EventArgs e)
        {
            // In case unhandled exception is thrown before PreRequestHandlerExecute
            var currentActivity = ActivityHelper.RestoreCurrentActivity(CurrentHttpContext);
            var app = (HttpApplication)sender;
            ActivityHelper.TriggerAspNetExceptionActivity(app.Server.GetLastError());
            ActivityHelper.StopAspNetActivity(currentActivity, new { Context = CurrentHttpContext });

            // In case unhandled exception is thrown during handler executing, which won't
            // trigger PostRequestHandlerExecut event.
            ActivityHelper.StopAspNetActivity(_rootActivityInHandlerExecution, new { Context = CurrentHttpContext });
        }

        private void Application_EndRequest(object sender, EventArgs e)
        {
            ActivityHelper.StopAspNetActivity(_activity, new { Context = CurrentHttpContext });
        }        
    }
}
