// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections;
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
        private static bool initialized = false;
        private static MethodInfo onStepMethodInfo = null;
        private static object sync = new object();

        /// <inheritdoc />
        public void Dispose()
        {
        }

        /// <inheritdoc />
        public void Init(HttpApplication context)
        {
            context.BeginRequest += Application_BeginRequest;
            context.EndRequest += Application_EndRequest;

            if (!initialized)
            {
                lock (sync)
                {
                    if (!initialized)
                    {
                        onStepMethodInfo = typeof(HttpApplication).GetMethod("OnExecuteRequestStep");
                        initialized = true;
                    }
                }
            }

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
            RestoreActivityIfNeeded(context.Items);
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
            RestoreActivityIfNeeded(((HttpApplication)sender).Context.Items);
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
                ActivityHelper.StopAspNetActivity(activity, context);
            }
            else
            {
                var activity = (Activity)context.Items[ActivityHelper.ActivityKey];

                // try to stop activity if it's in the Current stack
                if (!ActivityHelper.StopAspNetActivity(activity, context))
                {
                    // Activity we created was lost, let's report it
                    if (activity != null)
                    {
                        ActivityHelper.StopLostActivity(activity, context);
                    }
                }
            }
        }

        private void RestoreActivityIfNeeded(IDictionary contextItems)
        {
            if (Activity.Current == null)
            {
                var rootActivity = (Activity)contextItems[ActivityHelper.ActivityKey];
                if (rootActivity != null)
                {
                    ActivityHelper.RestoreCurrentActivity(rootActivity);
                }
            }
        }
    }
}
