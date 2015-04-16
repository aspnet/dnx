// Copyright (c) Microsoft Open Technologies, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;

namespace Microsoft.Framework.Runtime.Infrastructure
{
    /// <summary>
    /// The <see cref="IServiceProviderLocator"/> provides access to the current <see cref="IServiceProvider"/>.
    /// </summary>
    public interface IServiceProviderLocator
    {
        /// <summary>
        /// Gets or sets the <see cref="IServiceProvider"/>.
        /// </summary>
        IServiceProvider ServiceProvider { get; set; }
    }
}
