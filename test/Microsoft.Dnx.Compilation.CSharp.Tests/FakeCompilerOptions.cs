using System.Collections.Generic;
using Microsoft.Extensions.CompilationAbstractions;

namespace Microsoft.Dnx.Compilation.CSharp.Tests
{
    internal class FakeCompilerOptions : ICompilerOptions
    {
        public bool? AllowUnsafe { get; set; }

        public IEnumerable<string> Defines { get; set; }

        public bool? DelaySign { get; set; }

        public bool? EmitEntryPoint { get; set; }

        public string KeyFile { get; set; }

        public string LanguageVersion { get; set; }

        public bool? Optimize { get; set; }

        public string Platform { get; set; }

        public bool? UseOssSigning { get; set; }

        public bool? WarningsAsErrors { get; set; }
    }
}