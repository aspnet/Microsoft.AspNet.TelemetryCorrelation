// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Diagnostics;
using System.Reflection;
using System.Web;

namespace Microsoft.AspNet.TelemetryCorrelation
{
    /// <summary>
    /// Http Module sets ambient state using Activity API from DiagnosticsSource package.
    /// </summary>
    public class TelemetryCorrelationHttpModule : IHttpModule
    {
        private const string BeginCalledFlag = "Microsoft.AspNet.TelemetryCorrelation.BeginCalled";
        private static MethodInfo onStepMethodInfo = null;

        static TelemetryCorrelationHttpModule()
        {
            onStepMethodInfo = typeof(HttpApplication).GetMethod("OnExecuteRequestStep");
        }

        /// <inheritdoc />
        public void Dispose()
        {
        }

        /// <inheritdoc />
        public void Init(HttpApplication context)
        {
            context.BeginRequest += Application_BeginRequest;
            context.EndRequest += Application_EndRequest;

            // OnExecuteRequestStep is availabile starting with 4.7.1
            // If this is executed in 4.7.1 runtime (regardless of targeted .NET version),
            // we will use it to restore lost activity, otherwise keep PreRequestHandlerExecute
            if (onStepMethodInfo != null)
            {
                onStepMethodInfo.Invoke(context, new object[] { (Action<HttpContextBase, Action>)OnExecuteRequestStep });
            }
            else
            {
                context.PreRequestHandlerExecute += Application_PreRequestHandlerExecute;
            }
        }

        /// <summary>
        /// Restores Activity before each pipeline step if it was lost.
        /// </summary>
        /// <param name="context">HttpContext instance.</param>
        /// <param name="step">Step to be executed.</param>
        internal void OnExecuteRequestStep(HttpContextBase context, Action step)
        {
            // Once we have public Activity.Current setter (https://github.com/dotnet/corefx/issues/29207) this method will be
            // simplified to just assign Current if is was lost.
            // In the mean time, we are creating child Activity to restore the context. We have to send
            // event with this Activity to tracing system. It created a lot of issues for listeners as
            // we may potentially have a lot of them for different stages.
            // To reduce amount of events, we only care about ExecuteRequestHandler stage - restore activity here and
            // stop/report it to tracing system in EndRequest.
            if (context.CurrentNotification == RequestNotification.ExecuteRequestHandler && !context.IsPostNotification)
            {
                ActivityHelper.RestoreActivityIfNeeded(context.Items);
            }

            step();
        }

        private void Application_BeginRequest(object sender, EventArgs e)
        {
            var context = ((HttpApplication)sender).Context;
            AspNetTelemetryCorrelationEventSource.Log.TraceCallback("Application_BeginRequest");
            ActivityHelper.CreateRootActivity(context);
            context.Items[BeginCalledFlag] = true;
        }

        private void Application_PreRequestHandlerExecute(object sender, EventArgs e)
        {
            AspNetTelemetryCorrelationEventSource.Log.TraceCallback("Application_PreRequestHandlerExecute");
            ActivityHelper.RestoreActivityIfNeeded(((HttpApplication)sender).Context.Items);
        }

        private void Application_EndRequest(object sender, EventArgs e)
        {
            AspNetTelemetryCorrelationEventSource.Log.TraceCallback("Application_EndRequest");

            var context = ((HttpApplication)sender).Context;

            // EndRequest does it's best effort to notify that request has ended
            // BeginRequest has never been called
            if (!context.Items.Contains(BeginCalledFlag))
            {
                // Activity has never been started
                var activity = ActivityHelper.CreateRootActivity(context);
                ActivityHelper.StopAspNetActivity(activity, context.Items);
            }
            else
            {
                var activity = (Activity)context.Items[ActivityHelper.ActivityKey];

                // try to stop activity if it's in the Current stack
                // stop all running Activities on the way
                if (!ActivityHelper.StopAspNetActivity(activity, context.Items))
                {
                    // perhaps we attempted to restore the Activity before
                    var restoredActivity = (Activity)context.Items[ActivityHelper.RestoredActivityKey];
                    if (restoredActivity != null)
                    {
                        // if so, report it
                        ActivityHelper.StopRestoredActivity(restoredActivity, context);
                    }

                    // Activity we created was lost let's report it
                    if (activity != null)
                    {
                        ActivityHelper.StopLostActivity(activity, context);
                    }
                }
            }
        }
    }
}
