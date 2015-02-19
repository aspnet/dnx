using System;
using System.Runtime.Versioning;

namespace Microsoft.Framework.Runtime.Roslyn
{
    /// <summary>
    /// Stores information about the project
    /// </summary>
    public interface IProjectContext
    {
        /// <summary>
        /// Path to the project directory
        /// </summary>
        string ProjectDirectory { get; }

        /// <summary>
        /// Path to the project file
        /// </summary>
        string ProjectFilePath { get; }

        /// <summary>
        /// The name of the project
        /// </summary>
        string Name { get; }

        /// <summary>
        /// The target framework of the project currently being compiled
        /// </summary>
        FrameworkName TargetFramework { get; }

        /// <summary>
        /// Project version
        /// </summary>
        string Version { get; }
    }
}