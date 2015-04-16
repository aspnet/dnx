// Copyright (c) Microsoft Open Technologies, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Runtime.Versioning;

namespace Microsoft.Framework.Runtime
{
    /// <summary>
    /// Provides access to common application information.
    /// </summary>
    public interface IApplicationEnvironment
    {
        /// <summary>
        /// Gets the application name.
        /// </summary>
        string ApplicationName { get; }

        /// <summary>
        /// Gets the version of the application. The version described in project.json.
        /// </summary>
        string Version { get; }

        /// <summary>
        /// Gets the base directory of the application. The path where the project.json file exists.
        /// </summary>
        string ApplicationBasePath { get; }

        /// <summary>
        /// Gets the configuration. This should only be used for runtime compilation.
        /// </summary>
        string Configuration { get; }

        /// <summary>
        /// Gets the target version and profile of the .NET Framework for the application domain.
        /// </summary>
        FrameworkName RuntimeFramework { get; }
    }
}
