// Copyright (c) Microsoft Open Technologies, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;

namespace Microsoft.Framework.Runtime
{
    /// <summary>
    /// Provides a list of service types exposed by the runtime.
    /// </summary>
    public interface IServiceManifest
    {
        /// <summary>
        /// Gets the list of exposed service types.
        /// </summary>
        IEnumerable<Type> Services { get; }
    }
}