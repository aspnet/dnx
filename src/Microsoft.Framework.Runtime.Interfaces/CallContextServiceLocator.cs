// Copyright (c) Microsoft Open Technologies, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace Microsoft.Framework.Runtime.Infrastructure
{
    /// <summary>
    /// A class that exposes the ambient service provider.
    /// </summary>
    public static class CallContextServiceLocator
    {
        /// <summary>
        /// Provides access to the <see cref="IServiceProviderLocator"/>.
        /// </summary>
        public static IServiceProviderLocator Locator;
    }
}
