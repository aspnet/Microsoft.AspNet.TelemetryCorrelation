// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Threading.Tasks;

namespace Microsoft.Owin.TelemetryCorrelation
{
    /// <summary>
    /// Owin middleware sets ambient state using Activity API from DiagnosticsSource package.
    /// </summary>
    public sealed class TelemetryCorrelationMiddleware : OwinMiddleware
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="TelemetryCorrelationMiddleware"/> class.
        /// </summary>
        /// <param name="next">Next middleware in the pipeline</param>
        public TelemetryCorrelationMiddleware(OwinMiddleware next)
            : base(next)
        {
        }

        /// <inheritdoc />
        public override async Task Invoke(IOwinContext context)
        {
            var activity = ActivityHelper.CreateRootActivity(context.Request);
            try
            {
                await Next.Invoke(context).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                throw;
            }
            finally
            {
                activity.Stop();
            }
        }
    }
}