// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace Microsoft.Dnx.Runtime.Infrastructure
{
    /// <summary>
    /// Exposes the ambient service provider.
    /// </summary>
    public static class CallContextServiceLocator
    {
        /// <summary>
        /// Provides access to the <see cref="IServiceProviderLocator"/>.
        /// </summary>
        public static IServiceProviderLocator Locator;
    }
}
