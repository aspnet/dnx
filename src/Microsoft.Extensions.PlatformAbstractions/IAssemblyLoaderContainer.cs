// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.


using System;

namespace Microsoft.Extensions.PlatformAbstractions
{
    /// <summary>
    /// Provides access to assembly loaders used for runtime assembly resolution.
    /// </summary>
    public interface IAssemblyLoaderContainer
    {
        /// <summary>
        /// Adds an <see cref="IAssemblyLoader"/> to the runtime.
        /// </summary>
        /// <param name="loader">The loader to add.</param>
        /// <returns>A disposable object representing the registration of the loader. Disposing it removes the loader from the runtime.</returns>
        IDisposable AddLoader(IAssemblyLoader loader);
    }
}
