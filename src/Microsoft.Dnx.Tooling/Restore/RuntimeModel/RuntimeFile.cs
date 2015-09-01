using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Dnx.Runtime;

namespace Microsoft.Dnx.Tooling.Restore.RuntimeModel
{
    public class RuntimeFile
    {
        public Dictionary<string, RuntimeSpec> Runtimes { get; set; } = new Dictionary<string, RuntimeSpec>();

        public RuntimeFile() { }
        public RuntimeFile(params RuntimeSpec[] runtimes)
        {
            Runtimes = runtimes.ToDictionary(r => r.Name);
        }

        internal static RuntimeFile ParseFromProject(Runtime.Project project)
        {
            var runtimeFormatter = new RuntimeFileFormatter();
            return runtimeFormatter.ReadRuntimeFile(project.ProjectFilePath);
        }
    }

    public class RuntimeSpec
    {
        public string Name { get; set; }
        public List<string> Import { get; set; } = new List<string>();
        public Dictionary<string, DependencySpec> Dependencies { get; set; } = new Dictionary<string, DependencySpec>();

        public RuntimeSpec() { }
        public RuntimeSpec(string name, params string[] imports)
        {
            Name = name;
            Import = imports.ToList();
        }
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