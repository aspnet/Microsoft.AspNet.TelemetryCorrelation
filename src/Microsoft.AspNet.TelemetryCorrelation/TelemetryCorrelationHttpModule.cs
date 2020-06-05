// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.ComponentModel;
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

        /// <summary>
        /// Gets or sets a value indicating whether TelemetryCorrelationHttpModule should parse headers to get correlation ids.
        /// </summary>
        [EditorBrowsable(EditorBrowsableState.Never)]
        public bool ParseHeaders { get; set; } = true;

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
            if (onStepMethodInfo != null && HttpRuntime.UsingIntegratedPipeline)
            {
                try
                {
                    onStepMethodInfo.Invoke(context, new object[] { (Action<HttpContextBase, Action>)OnExecuteRequestStep });
                }
                catch (Exception e)
                {
                    AspNetTelemetryCorrelationEventSource.Log.OnExecuteRequestStepInvokationError(e.Message);
                }
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
            ActivityHelper.CreateRootActivity(context, ParseHeaders);
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
            bool trackActivity = true;

            var context = ((HttpApplication)sender).Context;

            // EndRequest does it's best effort to notify that request has ended
            // BeginRequest has never been called
            if (!context.Items.Contains(BeginCalledFlag))
            {
                // Exception happened before BeginRequest
                if (context.Error != null)
                {
                    // Activity has never been started
                    ActivityHelper.CreateRootActivity(context, ParseHeaders);
                }
                else
                {
                    // Rewrite: In case of rewrite, a new request context is created, called the child request, and it goes through the entire IIS/ASP.NET integrated pipeline.
                    // The child request can be mapped to any of the handlers configured in IIS, and it's execution is no different than it would be if it was received via the HTTP stack.
                    // The parent request jumps ahead in the pipeline to the end request notification, and waits for the child request to complete.
                    // When the child request completes, the parent request executes the end request notifications and completes itself.
                    // Ignore creating root activity for parent request as control got transferred from rewrite module to EndRequest with no request flow.
                    trackActivity = false;
                }
            }

            if (trackActivity)
            {
                ActivityHelper.StopAspNetActivity(context.Items);
            }
        }
    }
}
