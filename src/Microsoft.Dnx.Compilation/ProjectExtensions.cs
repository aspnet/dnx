using System.Runtime.Versioning;
using Microsoft.Dnx.Runtime;
using Microsoft.Extensions.PlatformAbstractions;
using Microsoft.Extensions.CompilationAbstractions;

namespace Microsoft.Dnx.Compilation
{
    internal static class ProjectExtensions
    {
        public static CompilationProjectContext ToCompilationContext(this Project self, FrameworkName frameworkName, string configuration, string aspect)
        {
            return new CompilationProjectContext(
                new CompilationTarget(self.Name, frameworkName, configuration, aspect),
                self.ProjectDirectory,
                self.ProjectFilePath,
                self.Title,
                self.Description,
                self.Copyright,
                self.Version.ToString(),
                self.AssemblyFileVersion,
                self.EmbedInteropTypes,
                new CompilationFiles(
                    self.Files.PreprocessSourceFiles,
                    self.Files.SourceFiles),
                self.GetCompilerOptions(frameworkName, configuration));
        }
    }
}
