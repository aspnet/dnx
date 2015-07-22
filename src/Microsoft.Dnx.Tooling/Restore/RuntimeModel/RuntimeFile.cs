using System;
using System.Collections.Generic;

namespace Microsoft.Dnx.Tooling.Restore.RuntimeModel
{
    public class RuntimeFile
    {
        public Dictionary<string, RuntimeSpec> Runtimes { get; set; } = new Dictionary<string, RuntimeSpec>();
    }

    public class RuntimeSpec
    {
        public string Name { get; set; }
        public List<string> Import { get; set; } = new List<string>();
        public Dictionary<string, DependencySpec> Dependencies { get; set; } = new Dictionary<string, DependencySpec>();
    }

    public class DependencySpec
    {
        public string Name { get; set; }
        public Dictionary<string, ImplementationSpec> Implementations { get; set; } = new Dictionary<string, ImplementationSpec>();
    }

    public class ImplementationSpec
    {
        public string Name { get; set; }
        public string Version { get; set; }
    }
}