// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Runtime.Versioning;

namespace Microsoft.Extensions.PlatformAbstractions
{
    /// <summary>
    /// Provides access to the runtime environment.
    /// </summary>
    public interface IRuntimeEnvironment
    {
        /// <summary>
        /// Gets the current operating system name.
        /// </summary>
        string OperatingSystem { get; }

        /// <summary>
        /// Gets the current operating system version.
        /// </summary>
        string OperatingSystemVersion { get; }

        /// <summary>
        /// Gets the runtime type. Common values include CLR, CoreCLR and Mono.
        /// </summary>
        string RuntimeType { get; }

        /// <summary>
        /// Gets the runtime architecture. Common values include x86 and x64.
        /// </summary>
        string RuntimeArchitecture { get; }

        /// <summary>
        /// Gets the runtime version.
        /// </summary>
        string RuntimeVersion { get; }

        /// <summary>
        /// Gets the path to the runtime foler.
        /// </summary>
        string RuntimePath { get; }
    }
}
