using System;
using System.Runtime.Versioning;

namespace Microsoft.Framework.Runtime.Compilation
{
    public interface ICompilationProject
    {
        string Name { get; }
        string ProjectDirectory { get; }
        string ProjectFilePath { get; }
        IProjectFilesCollection Files { get; }
        string Version { get; }

        // Unfortunately we have to do this for now... we'll need a more general compilation options system :(
        bool EmbedInteropTypes { get; set; }

        ICompilerOptions GetCompilerOptions(FrameworkName targetFramework, string configuration);
    }
}