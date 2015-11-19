using System.Collections.Generic;

namespace Microsoft.Extensions.CompilationAbstractions
{
    /// <summary>
    /// Represents the resolved collection of files used for compilation
    /// </summary>
    public class CompilationFiles
    {
        public IEnumerable<string> PreprocessSourceFiles { get; }
        public IEnumerable<string> SourceFiles { get; }

        public CompilationFiles(IEnumerable<string> preprocessSourceFiles, IEnumerable<string> sourceFiles)
        {
            PreprocessSourceFiles = preprocessSourceFiles;
            SourceFiles = sourceFiles;
        }
    }
}
