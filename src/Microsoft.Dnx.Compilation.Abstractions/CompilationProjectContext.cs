using System;
using Microsoft.Dnx.Runtime;

namespace Microsoft.Dnx.Compilation
{
    /// <summary>
    /// Represents the information needed to compile a project.
    /// </summary>
    public class CompilationProjectContext
    {
        public CompilationTarget Target { get; }
        public string ProjectDirectory { get; }
        public string ProjectFilePath { get; }
        public string Version { get; }
        public Version AssemblyFileVersion { get; }
        public CompilationFiles Files { get; }

        // Unfortunately we have to do this for now... we'll need a more general compilation options system :(
        public bool EmbedInteropTypes { get; }

        public ICompilerOptions CompilerOptions { get; }

        public CompilationProjectContext(
            CompilationTarget target,
            string projectDirectory,
            string projectFilePath,
            string version,
            Version assemblyFileVersion,
            bool embedInteropTypes,
            CompilationFiles files,
            ICompilerOptions compilerOptions)
        {
            Target = target;
            ProjectDirectory = projectDirectory;
            ProjectFilePath = projectFilePath;
            Files = files;
            Version = version;
            AssemblyFileVersion = assemblyFileVersion;
            EmbedInteropTypes = embedInteropTypes;
            CompilerOptions = compilerOptions;
        }
    }
}