using System.Collections.Generic;

namespace Microsoft.Framework.Runtime
{
    public class CompilationFailure : ICompilationFailure
    {
        public IEnumerable<ICompilationMessage> Messages { get; set; }

        public string SourceFilePath { get; set; }

        public string CompiledContent { get; set; }

        public string SourceFileContent { get; set; }
    }
}