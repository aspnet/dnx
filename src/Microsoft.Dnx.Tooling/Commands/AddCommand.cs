// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.IO;
using Newtonsoft.Json.Linq;
using NuGet;

namespace Microsoft.Dnx.Tooling
{
    public class AddCommand
    {
        public string Name { get; set; }
        public string Version { get; set; }
        public string ProjectDir { get; set; }
        public Reports Reports { get; set; }

        public bool ExecuteCommand()
        {
            if (string.IsNullOrEmpty(Name))
            {
                Reports.Error.WriteLine("Name of dependency to add is required.".Red());
                return false;
            }

            if (string.IsNullOrEmpty(Version))
            {
                Reports.Error.WriteLine("Version of dependency to add is required.".Red());
                return false;
            }

            // Only for version validation
            SemanticVersion.Parse(Version);

            ProjectDir = ProjectDir ?? Directory.GetCurrentDirectory();

            Runtime.Project project;
            if (!Runtime.Project.TryGetProject(ProjectDir, out project))
            {
                Reports.Error.WriteLine("Unable to locate {0}.".Red(), Runtime.Project.ProjectFileName);
                return false;
            }

            var root = JObject.Parse(File.ReadAllText(project.ProjectFilePath));
            if (root["dependencies"] == null)
            {
                root["dependencies"] = new JObject();
            }

            root["dependencies"][Name] = Version;

            File.WriteAllText(project.ProjectFilePath, root.ToString());

            Reports.Information.WriteLine("{0}.{1} was added to {2}.", Name, Version, Runtime.Project.ProjectFileName);

            return true;
        }
    }
}
