// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

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
        public override Task Invoke(IOwinContext context)
        {
            return Task.FromResult(1);
        }
    }
}