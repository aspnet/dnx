using System.Collections.Generic;

namespace Microsoft.Framework.Runtime
{
    public class CompilerOptions
    {
        public IEnumerable<string> Defines { get; set; }

        public string LanguageVersion { get; set; }

        public bool AllowUnsafe { get; set; }

        public string Platform { get; set; }

        public bool WarningsAsErrors { get; set; }

        public string CommandLine { get; set; }
    }
}