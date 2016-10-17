using System;
using Microsoft.Extensions.PlatformAbstractions;

namespace Microsoft.Extensions.CompilationAbstractions
{
    /// <summary>
    /// Represents the information needed to compile a project.
    /// </summary>
    public class CompilationProjectContext
    {
        public CompilationTarget Target { get; }
        public string ProjectDirectory { get; }
        public string ProjectFilePath { get; }
        public string Title { get; }
        public string Description { get; }
        public string Copyright { get; }
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
            string title,
            string description,
            string copyright,
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
            Title = title;
            Description = description;
            Copyright = copyright;
            Version = version;
            AssemblyFileVersion = assemblyFileVersion;
            EmbedInteropTypes = embedInteropTypes;
            CompilerOptions = compilerOptions;
        }
    }
}