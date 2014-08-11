using System.Collections.Generic;

namespace Microsoft.Framework.Runtime
{
    public class CompileResponse
    {
        public string ProjectPath { get; set; }
        public IList<string> Sources { get; set; }

        public IList<string> Errors { get; set; }
        public IList<string> Warnings { get; set; }

        public IDictionary<string, byte[]> EmbeddedReferences { get; set; }

        public string Name { get; set; }
        public string TargetFramework { get; set; }

        public string Configuration { get; set; }

        public byte[] AssemblyBytes { get; set; }
        public byte[] PdbBytes { get; set; }

        public string AssemblyPath { get; set; }
    }

}