using System;
using System.Runtime.Versioning;

namespace Microsoft.Framework.Runtime.Compilation
{
    /// <summary>
    /// DO NOT USE. Provides a (temporary) interface to abstract between old runtime Project and new runtime Project.
    /// Eventually the Project class itself will move into this assembly!
    /// </summary>
    public interface ICompilationProject
    {
        string Name { get; }
        string ProjectDirectory { get; }
        string ProjectFilePath { get; }
        IProjectFilesCollection Files { get; }
        string Version { get; }
        string AssemblyFileVersion { get; }

        // Unfortunately we have to do this for now... we'll need a more general compilation options system :(
        bool EmbedInteropTypes { get; set; }

        ICompilerOptions GetCompilerOptions(FrameworkName targetFramework, string configuration);
    }
}