// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;

namespace Microsoft.Dnx.Runtime
{
    /// <summary>
    /// Provides a list of service types exposed by the runtime.
    /// </summary>
    public interface IRuntimeServices
    {
        /// <summary>
        /// Gets the list of exposed service types.
        /// </summary>
        IEnumerable<Type> Services { get; }
    }
}