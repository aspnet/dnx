// Copyright (c) Microsoft Open Technologies, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Security.Cryptography;
using Microsoft.Framework.Runtime;

namespace Microsoft.Framework.PackageManager.Packing
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

        public string Name { get { return _libraryDescription.Identity.Name; } }
        public string TargetPath { get; private set; }
        public string AppFolder { get; set; }

        public void EmitSource(PackRoot root)
        {
            Console.WriteLine("Packing project dependency {0}", _libraryDescription.Identity.Name);

            Runtime.Project project;
            if (!_projectResolver.TryResolveProject(_libraryDescription.Identity.Name, out project))
            {
                throw new Exception("TODO: unable to resolve project named " + _libraryDescription.Identity.Name);
            }

            var targetName = AppFolder ?? project.Name;
            TargetPath = Path.Combine(root.OutputPath, targetName);

            Console.WriteLine("  Source {0}", project.ProjectDirectory);
            Console.WriteLine("  Target {0}", TargetPath);

            root.Operations.Delete(TargetPath);

            // A set of excluded source files used as a filter when doing copy
            var sourceExcludeFiles = new HashSet<string>(project.SourceExcludeFiles, StringComparer.OrdinalIgnoreCase);

            root.Operations.Copy(project.ProjectDirectory, TargetPath, (isRoot, filePath) =>
            {
                if (IsImplicitlyExcludedFile(filePath))
                {
                    return false;
                }

                if (sourceExcludeFiles.Contains(filePath))
                {
                    return false;
                }

                return true;
            });
        }

        public void EmitNupkg(PackRoot root)
        {
            Console.WriteLine("Packing nupkg from project dependency {0}", _libraryDescription.Identity.Name);

            Runtime.Project project;
            if (!_projectResolver.TryResolveProject(_libraryDescription.Identity.Name, out project))
            {
                throw new Exception("TODO: unable to resolve project named " + _libraryDescription.Identity.Name);
            }

            var targetName = AppFolder ?? project.Name;
            var targetNupkgName = string.Format("{0}.{1}", targetName, project.Version);
            TargetPath = Path.Combine(root.OutputPath, "packages", targetNupkgName);

            Console.WriteLine("  Source {0}", project.ProjectDirectory);
            Console.WriteLine("  Target {0}", TargetPath);

            if (Directory.Exists(TargetPath))
            {
                if (root.Overwrite)
                {
                    root.Operations.Delete(TargetPath);
                }
                else
                {
                    Console.WriteLine("  {0} already exists.", TargetPath);
                    return;
                }
            }

            // Generate nupkg from this project dependency
            var buildOptions = new BuildOptions();
            buildOptions.ProjectDir = project.ProjectDirectory;
            buildOptions.OutputDir = Path.Combine(project.ProjectDirectory, "bin");
            buildOptions.Configurations.Add(root.Configuration);
            var buildManager = new BuildManager(buildOptions);
            if (!buildManager.Build())
            {
                return;
            }

            // Extract the generated nupkg to target path
            var srcNupkgPath = Path.Combine(buildOptions.OutputDir, root.Configuration, targetNupkgName + ".nupkg");
            var targetNupkgPath = Path.Combine(TargetPath, targetNupkgName + ".nupkg");
            using (var sourceStream = new FileStream(srcNupkgPath, FileMode.Open, FileAccess.Read))
            {
                using (var archive = new ZipArchive(sourceStream, ZipArchiveMode.Read))
                {
                    root.Operations.ExtractNupkg(archive, TargetPath);
                }
            }
            using (var sourceStream = new FileStream(srcNupkgPath, FileMode.Open, FileAccess.Read))
            {
                using (var targetStream = new FileStream(targetNupkgPath, FileMode.Create, FileAccess.Write, FileShare.None))
                {
                    sourceStream.CopyTo(targetStream);
                }

                sourceStream.Seek(0, SeekOrigin.Begin);
                var sha512Bytes = SHA512.Create().ComputeHash(sourceStream);
                File.WriteAllText(targetNupkgPath + ".sha512", Convert.ToBase64String(sha512Bytes));
            }
        }

    private static bool IsImplicitlyExcludedFile(string filePath)
        {
            var fileExtension = Path.GetExtension(filePath);
            var fileName = Path.GetFileName(filePath);

            if (string.Equals(fileExtension, ".csproj", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(fileExtension, ".kproj", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(fileExtension, ".user", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(fileExtension, ".vspscc", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(fileExtension, ".vssscc", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(fileExtension, ".pubxml", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(fileName, "bin", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(fileName, "obj", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
            return false;
        }

        public void PostProcess(PackRoot root)
        {
            var binFolderPath = Path.Combine(TargetPath, "bin");

            var defaultRuntime = root.Runtimes.FirstOrDefault();
            var iniFilePath = Path.Combine(TargetPath, "k.ini");
            if (defaultRuntime != null && !File.Exists(iniFilePath))
            {
                var parts = defaultRuntime.Name.Split(new []{'.'}, 2);
                if (parts.Length == 2)
                {
                    var versionNumber = parts[1];
                    parts = parts[0].Split(new []{'-'}, 3);
                    if (parts.Length == 3)
                    {
                        var flavor = parts[1];
                        File.WriteAllText(iniFilePath, string.Format(@"[Runtime]
KRE_VERSION={0}
KRE_FLAVOR={1}
CONFIGURATION={2}
", 
versionNumber, 
flavor == "svrc50" ? "CoreCLR" : "DesktopCLR",
root.Configuration));
                    }
                }
            }

            // Copy tools/*.dll into bin to support AspNet.Loader.dll
            foreach (var package in root.Packages)
            {
                var packageToolsPath = Path.Combine(package.TargetPath, "tools");
                if (Directory.Exists(packageToolsPath))
                {
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

            // Copy lib/**/*.dll into bin/$xxxxxxx.packages to support web deploy
            if (root.ZipPackages)
            {
                if (!Directory.Exists(binFolderPath))
                {
                    Directory.CreateDirectory(binFolderPath);
                }

                var chars = "abcdefghijklmnopqrstuvwxyz0123456789";
                var rnd = new Random();
                var sequence = new string(Enumerable.Range(0, 7).Select(_ => chars[rnd.Next(chars.Length)]).ToArray());
                var targetFile = Path.Combine(binFolderPath, "$" + sequence + ".packages");

                using (var targetStream = new FileStream(targetFile, FileMode.Create, FileAccess.Write, FileShare.None))
                {
                    using (var archive = new ZipArchive(targetStream, ZipArchiveMode.Create))
                    {
                        foreach (var package in root.Packages)
                        {
                            root.Operations.AddFiles(
                                archive,
                                package.TargetPath,
                                Path.Combine("packages", package.Library.Name + "." + package.Library.Version),
                                IncludePackageFileInBundle);
                        }
                        foreach (var runtime in root.Runtimes)
                        {
                            //root.Operations.AddFiles(
                            //    archive,
                            //    root.Runtime.TargetPath,
                            //    Path.Combine("packages", root.Runtime.Name + "." + root.Runtime.Version),
                            //    IncludeRuntimeFileInBundle);
                        }
                    }
                }
            }
        }

        private bool IncludePackageFileInBundle(string relativePath, string fileName)
        {
            var fileExtension = Path.GetExtension(fileName);
            var rootFolder = BasePath(relativePath);

            if (/*string.Equals(rootFolder, "lib", StringComparison.OrdinalIgnoreCase) && */
                string.Equals(fileExtension, ".dll", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
            if (string.IsNullOrEmpty(relativePath) &&
                (string.Equals(fileExtension, ".sha512", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(fileExtension, ".nuspec", StringComparison.OrdinalIgnoreCase)))
            {
                return true;
            }
            return false;
        }

        private bool IncludeRuntimeFileInBundle(string relativePath, string fileName)
        {
            return true;
        }

        private string BasePath(string relativePath)
        {
            var index1 = (relativePath + Path.DirectorySeparatorChar).IndexOf(Path.DirectorySeparatorChar);
            var index2 = (relativePath + Path.AltDirectorySeparatorChar).IndexOf(Path.AltDirectorySeparatorChar);
            return relativePath.Substring(0, Math.Min(index1, index2));
        }

    }
}
