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
        }

        public string OutputPath { get; private set; }
        public string PackagesPath { get; private set; }

        public PackRuntime Runtime { get; set; }
        public IList<PackProject> Projects { get; private set; }
        public IList<PackPackage> Packages { get; private set; }

        public void Emit()
        {
            Console.WriteLine("Copying to output path {0}", OutputPath);

            foreach (var deploymentPackage in Packages)
            {
                deploymentPackage.Emit(this);
            }

            foreach (var deploymentProject in Projects)
            {
                deploymentProject.Emit(this);
            }

            Runtime.Emit(this);

            if (_project != null && _project.Commands != null)
            {
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

        public void Delete(string folderPath)
        {
            DeleteRecursive(folderPath);
        }

        private void DeleteRecursive(string deletePath)
        {
            if (!Directory.Exists(deletePath))
            {
                return;
            }

            foreach (var deleteFilePath in Directory.EnumerateFiles(deletePath).Select(Path.GetFileName))
            {
                File.Delete(Path.Combine(deletePath, deleteFilePath));
            }

            foreach (var deleteFolderPath in Directory.EnumerateDirectories(deletePath).Select(Path.GetFileName))
            {
                DeleteRecursive(Path.Combine(deletePath, deleteFolderPath));
                Directory.Delete(Path.Combine(deletePath, deleteFolderPath), true);
            }
        }
    }
}

