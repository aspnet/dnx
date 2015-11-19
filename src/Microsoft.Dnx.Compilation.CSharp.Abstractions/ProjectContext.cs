// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Runtime.Versioning;

namespace Microsoft.Dnx.Compilation.CSharp
{
    /// <summary>
    /// Stores information about the project
    /// </summary>
    public class ProjectContext
    {
        /// <summary>
        /// Path to the project directory
        /// </summary>
        public string ProjectDirectory { get; set; }

        /// <summary>
        /// Path to the project file
        /// </summary>
        public string ProjectFilePath { get; set; }

        /// <summary>
        /// The name of the project
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// The target framework of the project currently being compiled
        /// </summary>
        public FrameworkName TargetFramework { get; set; }

        /// <summary>
        /// The configuration of the project currently being compiled
        /// </summary>
        public string Configuration { get; set; }

        /// <summary>
        /// Project version
        /// </summary>
        public string Version { get; set; }
    }
}