using System;

namespace Microsoft.Framework.Runtime
{
    public class CompileRequest
    {
        public string ProjectPath { get; set; }

        public string Configuration { get; set; }

        public string TargetFramework { get; set; }
    }
}