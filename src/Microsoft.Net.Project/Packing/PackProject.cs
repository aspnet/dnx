using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Microsoft.Net.Runtime;

namespace Microsoft.Net.Project.Packing
{
    public class PackProject
    {
        private readonly ProjectReferenceDependencyProvider _projectReferenceDependencyProvider;
        private readonly IProjectResolver _projectResolver;
        private readonly LibraryDescription _libraryDescription;

        public PackProject(
            ProjectReferenceDependencyProvider projectReferenceDependencyProvider,
            IProjectResolver projectResolver,
            LibraryDescription libraryDescription)
        {
            _projectReferenceDependencyProvider = projectReferenceDependencyProvider;
            _projectResolver = projectResolver;
            _libraryDescription = libraryDescription;
        }

        public void Emit(PackRoot root)
        {
            Runtime.Project project;
            if (!_projectResolver.TryResolveProject(_libraryDescription.Identity.Name, out project))
            {
                throw new Exception("TODO: unable to resolve project named " + _libraryDescription.Identity.Name);
            }

            var targetName = project.Name;
            var targetPath = Path.Combine(root.OutputPath, targetName);

            root.Delete(targetPath);
            CopyRecursive(project.ProjectDirectory, targetPath, isProjectRootFolder: true);

            foreach (var package in root.Packages)
            {
                var packageToolsPath = Path.Combine(package.TargetPath, "tools");
                if (Directory.Exists(packageToolsPath))
                {
                    var binFolderPath = Path.Combine(targetPath, "bin");

                    foreach (var packageToolFile in Directory.EnumerateFiles(packageToolsPath, "*.dll").Select(Path.GetFileName))
                    {
                        if (!Directory.Exists(binFolderPath))
                        {
                            Directory.CreateDirectory(binFolderPath);
                        }

                        File.Copy(
                            Path.Combine(packageToolsPath, packageToolFile),
                            Path.Combine(binFolderPath, packageToolFile),
                            true);
                    }
                }
            }
        }

        private void CopyRecursive(string sourcePath, string targetPath, bool isProjectRootFolder)
        {
            if (!Directory.Exists(targetPath))
            {
                Directory.CreateDirectory(targetPath);
            }

            foreach (var sourceFilePath in Directory.EnumerateFiles(sourcePath))
            {
                var fileName = Path.GetFileName(sourceFilePath);
                Debug.Assert(fileName != null, "fileName != null");

                if (isProjectRootFolder && IsFileExcludedFromProjectRoot(fileName))
                {
                    continue;
                }
                File.Copy(
                    Path.Combine(sourcePath, fileName),
                    Path.Combine(targetPath, fileName),
                    overwrite: true);
            }

            foreach (var sourceFolderPath in Directory.EnumerateDirectories(sourcePath))
            {
                var folderName = Path.GetFileName(sourceFolderPath);
                Debug.Assert(folderName != null, "folderName != null");

                if (isProjectRootFolder && IsFolderExcludedFromProjectRoot(folderName))
                {
                    continue;
                }

                CopyRecursive(
                    Path.Combine(sourcePath, folderName),
                    Path.Combine(targetPath, folderName),
                    isProjectRootFolder: false);
            }
        }

        private static bool IsFileExcludedFromProjectRoot(string fileName)
        {
            var fileExtension = Path.GetExtension(fileName);
            return string.Equals(fileExtension, ".csproj", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(fileExtension, ".kproj", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsFolderExcludedFromProjectRoot(string folderName)
        {
            return string.Equals(folderName, "bin", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(folderName, "obj", StringComparison.OrdinalIgnoreCase);
        }
    }
}