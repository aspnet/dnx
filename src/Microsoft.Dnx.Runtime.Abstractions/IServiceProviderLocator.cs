// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;

namespace Microsoft.Dnx.Runtime.Infrastructure
{
    /// <summary>
    /// Provides access to the current <see cref="IServiceProvider"/>.
    /// </summary>
    public interface IServiceProviderLocator
    {
        /// <summary>
        /// Gets or sets the current <see cref="IServiceProvider"/>.
        /// </summary>
        IServiceProvider ServiceProvider { get; set; }
    }
}
