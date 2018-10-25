// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace Microsoft.AspNet.TelemetryCorrelation
{
    internal enum HttpParseResult
    {
        /// <summary>
        /// Parsed succesfully.
        /// </summary>
        Parsed,

        /// <summary>
        /// Was not parsed.
        /// </summary>
        NotParsed,

        /// <summary>
        /// Invalid format.
        /// </summary>
        InvalidFormat,
    }
}