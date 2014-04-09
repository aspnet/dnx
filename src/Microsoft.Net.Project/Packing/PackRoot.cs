using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Net.Runtime;

namespace Microsoft.Net.Project.Packing
{
    public class PackRoot
    {
        private readonly Runtime.Project _project;

        public PackRoot(Runtime.Project project, string outputPath)
        {
            _project = project;
            Projects = new List<PackProject>();
            Packages = new List<PackPackage>();
            OutputPath = outputPath;
            PackagesPath = Path.Combine(outputPath, "packages");
            Operations = new PackOperations();
        }

        public string OutputPath { get; private set; }
        public string PackagesPath { get; private set; }
        public bool Overwrite { get; set; }
        public bool ZipPackages { get; set; }

        public PackRuntime Runtime { get; set; }
        public IList<PackProject> Projects { get; private set; }
        public IList<PackPackage> Packages { get; private set; }

        public PackOperations Operations { get; private set; }

        public void Emit()
        {
            Console.WriteLine("Copying to output path {0}", OutputPath);

            var mainProject = Projects.Single(project => project.Name == _project.Name);

            foreach (var deploymentPackage in Packages)
            {
                deploymentPackage.Emit(this);
            }

            foreach (var deploymentProject in Projects)
            {
                deploymentProject.Emit(this);
            }

            Runtime.Emit(this);

            mainProject.PostProcess(this);


            foreach (var commandName in _project.Commands.Keys)
            {
                const string template = @"
SETLOCAL
SET K_APPBASE=%~dp0{0}
CALL ""%~dp0packages\{1}.{2}\tools\k"" {3} %*
ENDLOCAL";

                File.WriteAllText(
                    Path.Combine(OutputPath, commandName + ".cmd"),
                    string.Format(template, _project.Name, Runtime.Name, Runtime.Version, commandName));
            }
        }
    }
}

