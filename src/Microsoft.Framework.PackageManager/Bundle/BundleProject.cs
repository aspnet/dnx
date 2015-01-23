// Copyright (c) Microsoft Open Technologies, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Security.Cryptography;
using System.Xml;
using System.Xml.Linq;
using Microsoft.Framework.Runtime;
using Newtonsoft.Json.Linq;
using NuGet;

namespace Microsoft.Framework.PackageManager.Bundle
{
    public class BundleProject
    {
        private readonly ProjectReferenceDependencyProvider _projectReferenceDependencyProvider;
        private readonly IProjectResolver _projectResolver;
        private readonly LibraryDescription _libraryDescription;
        private string _applicationBase;

        public BundleProject(
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
        public string WwwRoot { get; set; }
        public string WwwRootOut { get; set; }

        public void Emit(BundleRoot root)
        {
            root.Reports.Quiet.WriteLine("Using {0} dependency {1} for {2}", _libraryDescription.Type,
                _libraryDescription.Identity, _libraryDescription.Framework.ToString().Yellow().Bold());

            if (root.NoSource)
            {
                EmitNupkg(root);
            }
            else
            {
                EmitSource(root);
            }
            root.Reports.Quiet.WriteLine();
        }

        private void EmitSource(BundleRoot root)
        {
            root.Reports.Quiet.WriteLine("  Copying source code from {0} dependency {1}",
                _libraryDescription.Type, _libraryDescription.Identity.Name);

            Runtime.Project project;
            if (!_projectResolver.TryResolveProject(_libraryDescription.Identity.Name, out project))
            {
                throw new Exception("TODO: unable to resolve project named " + _libraryDescription.Identity.Name);
            }

            var targetName = project.Name;
            TargetPath = Path.Combine(root.OutputPath, BundleRoot.AppRootName, "src", targetName);

            // If root.OutputPath is specified by --out option, it might not be a full path
            TargetPath = Path.GetFullPath(TargetPath);

            root.Reports.Quiet.WriteLine("    Source {0}", _libraryDescription.Path.Bold());
            root.Reports.Quiet.WriteLine("    Target {0}", TargetPath);

            root.Operations.Delete(TargetPath);

            CopyProject(root, project, TargetPath, includeSource: true);

            CopyRelativeSources(project);

            UpdateWebRoot(root, TargetPath);

            _applicationBase = Path.Combine("..", BundleRoot.AppRootName, "src", project.Name);
        }

        private void EmitNupkg(BundleRoot root)
        {
            root.Reports.Quiet.WriteLine("  Packing nupkg from {0} dependency {1}",
                _libraryDescription.Type, _libraryDescription.Identity.Name);

            Runtime.Project project;
            if (!_projectResolver.TryResolveProject(_libraryDescription.Identity.Name, out project))
            {
                throw new Exception("TODO: unable to resolve project named " + _libraryDescription.Identity.Name);
            }

            var resolver = new DefaultPackagePathResolver(root.TargetPackagesPath);

            var targetNupkg = resolver.GetPackageFileName(project.Name, project.Version);
            TargetPath = resolver.GetInstallPath(project.Name, project.Version);

            root.Reports.Quiet.WriteLine("    Source {0}", _libraryDescription.Path.Bold());
            root.Reports.Quiet.WriteLine("    Target {0}", TargetPath);

            if (Directory.Exists(TargetPath))
            {
                if (root.Overwrite)
                {
                    root.Operations.Delete(TargetPath);
                }
                else
                {
                    root.Reports.Quiet.WriteLine("  {0} already exists.", TargetPath);
                    return;
                }
            }

            // Generate nupkg from this project dependency
            var buildOptions = new BuildOptions();
            buildOptions.ProjectDir = project.ProjectDirectory;
            buildOptions.OutputDir = Path.Combine(project.ProjectDirectory, "bin");
            buildOptions.Configurations.Add(root.Configuration);
            buildOptions.Reports = root.Reports;
            buildOptions.GeneratePackages = true;
            var buildManager = new BuildManager(root.HostServices, buildOptions);
            if (!buildManager.Build())
            {
                return;
            }

            // Extract the generated nupkg to target path
            var srcNupkgPath = Path.Combine(buildOptions.OutputDir, root.Configuration, targetNupkg);
            var targetNupkgPath = resolver.GetPackageFilePath(project.Name, project.Version);
            var hashFile = resolver.GetHashPath(project.Name, project.Version);

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
                File.WriteAllText(hashFile, Convert.ToBase64String(sha512Bytes));
            }

            // Copy content files (e.g. html, js and images) of main project into "root" folder of the exported package
            var rootFolderPath = Path.Combine(TargetPath, "root");
            var rootProjectJson = Path.Combine(rootFolderPath, Runtime.Project.ProjectFileName);

            CopyProject(root, project, rootFolderPath, includeSource: false);

            UpdateWebRoot(root, rootFolderPath);

            UpdateJson(rootProjectJson, jsonObj =>
            {
                // Update the project entrypoint
                jsonObj["entryPoint"] = _libraryDescription.Identity.Name;

                // Set mark this as non loadable
                jsonObj["loadable"] = false;

                // Update the dependencies node to reference the main project
                var deps = new JObject();
                jsonObj["dependencies"] = deps;

                deps[_libraryDescription.Identity.Name] = _libraryDescription.Identity.Version.ToString();
            });

            _applicationBase = Path.Combine("..", BundleRoot.AppRootName, "packages", resolver.GetPackageDirectory(_libraryDescription.Identity.Name, _libraryDescription.Identity.Version), "root");
        }

        private void CopyRelativeSources(Runtime.Project project)
        {
            // We can reference source files outside of project root with "code" property in project.json,
            // e.g. { "code" : "..\\ExternalProject\\**.cs" }
            // So we find out external source files and copy them separately
            var rootDirectory = ProjectResolver.ResolveRootDirectory(project.ProjectDirectory);
            foreach (var sourceFile in project.SourceFiles)
            {
                // This source file is in project root directory. So it was already copied.
                if (PathUtility.IsChildOfDirectory(dir: project.ProjectDirectory, candidate: sourceFile))
                {
                    continue;
                }

                // This source file is in solution root but out of project root,
                // it is an external source file that we should copy here
                if (PathUtility.IsChildOfDirectory(dir: rootDirectory, candidate: sourceFile))
                {
                    // Keep the relativeness between external source files and project root,
                    var relativeSourcePath = PathUtility.GetRelativePath(project.ProjectFilePath, sourceFile);
                    var relativeParentDir = Path.GetDirectoryName(relativeSourcePath);
                    Directory.CreateDirectory(Path.Combine(TargetPath, relativeParentDir));
                    var targetFile = Path.Combine(TargetPath, relativeSourcePath);
                    if (!File.Exists(targetFile))
                    {
                        File.Copy(sourceFile, targetFile);
                    }
                }
                else
                {
                    Console.WriteLine(
                        string.Format("TODO: Warning: the referenced source file '{0}' is not in solution root and it is not bundled to output.", sourceFile));
                }
            }
        }

        private void UpdateWebRoot(BundleRoot root, string targetPath)
        {
            // Update the 'webroot' property, which was specified with '--wwwroot-out' option
            if (!string.IsNullOrEmpty(WwwRootOut))
            {
                var targetProjectJson = Path.Combine(targetPath, Runtime.Project.ProjectFileName);

                UpdateJson(targetProjectJson, jsonObj =>
                {
                    var targetWebRootPath = Path.Combine(root.OutputPath, WwwRootOut);
                    jsonObj["webroot"] = PathUtility.GetRelativePath(targetProjectJson, targetWebRootPath, separator: '/');
                });
            }
        }

        private static void UpdateJson(string jsonFile, Action<JObject> modifier)
        {
            var jsonObj = JObject.Parse(File.ReadAllText(jsonFile));
            modifier(jsonObj);
            File.WriteAllText(jsonFile, jsonObj.ToString());
        }

        private void CopyProject(BundleRoot root, Runtime.Project project, string targetPath, bool includeSource)
        {
            // A set of excluded files/directories used as a filter when doing copy
            var excludeSet = new HashSet<string>(project.BundleExcludeFiles, StringComparer.OrdinalIgnoreCase);
            var contentFiles = new HashSet<string>(project.ContentFiles, StringComparer.OrdinalIgnoreCase);

            // If a public folder is specified with 'webroot' or '--wwwroot', we ignore it when copying project files
            var wwwRootPath = string.Empty;
            if (!string.IsNullOrEmpty(WwwRoot))
            {
                wwwRootPath = Path.Combine(project.ProjectDirectory, WwwRoot);
                wwwRootPath = PathUtility.EnsureTrailingSlash(wwwRootPath);
            }

            // If project root is used as value of '--wwwroot', we shouldn't exclude it when copying
            if (string.Equals(wwwRootPath, PathUtility.EnsureTrailingSlash(project.ProjectDirectory)))
            {
                wwwRootPath = string.Empty;
            }

            root.Operations.Copy(project.ProjectDirectory, targetPath, itemPath =>
            {
                // If current file/folder is in the exclusion list, we don't copy it
                if (excludeSet.Contains(itemPath))
                {
                    return false;
                }

                // If current file is in the public folder, we don't copy it to destination project
                if (!string.IsNullOrEmpty(wwwRootPath) && itemPath.StartsWith(wwwRootPath))
                {
                    return false;
                }

                // The full path of target generated by copy operation should also be excluded
                var targetFullPath = itemPath.Replace(project.ProjectDirectory, TargetPath);
                excludeSet.Add(targetFullPath);

                if (includeSource)
                {
                    return true;
                }

                if (Directory.Exists(itemPath))
                {
                    return true;
                }

                return contentFiles.Contains(itemPath);
            });
        }

        public void PostProcess(BundleRoot root)
        {
            // If --wwwroot-out doesn't have a non-empty value, we don't need a public app folder in output
            if (string.IsNullOrEmpty(WwwRootOut))
            {
                return;
            }

            Runtime.Project project;
            if (!_projectResolver.TryResolveProject(_libraryDescription.Identity.Name, out project))
            {
                throw new Exception("TODO: unable to resolve project named " + _libraryDescription.Identity.Name);
            }

            // Construct path to public app folder, which contains content files and tool dlls
            // The name of public app folder is specified with "--appfolder" option
            // Default name of public app folder is the same as main project
            var wwwRootOutPath = Path.Combine(root.OutputPath, WwwRootOut);

            // Delete old public app folder because we don't want leftovers from previous operations
            root.Operations.Delete(wwwRootOutPath);
            Directory.CreateDirectory(wwwRootOutPath);

            // Copy content files (e.g. html, js and images) of main project into public app folder
            CopyContentFiles(root, project, wwwRootOutPath);

            GenerateWebConfigFileForWwwRootOut(root, project, wwwRootOutPath);

            CopyAspNetLoaderDll(root, wwwRootOutPath);
        }

        private void GenerateWebConfigFileForWwwRootOut(BundleRoot root, Runtime.Project project, string wwwRootOutPath)
        {
            // Generate web.config for public app folder
            var wwwRootOutWebConfigFilePath = Path.Combine(wwwRootOutPath, "web.config");
            var wwwRootSourcePath = GetWwwRootSourcePath(project.ProjectDirectory, WwwRoot);
            var webConfigFilePath = Path.Combine(wwwRootSourcePath, "web.config");

            XDocument xDoc;
            if (File.Exists(webConfigFilePath))
            {
                xDoc = XDocument.Parse(File.ReadAllText(webConfigFilePath));
            }
            else
            {
                xDoc = new XDocument();
            }

            if (xDoc.Root == null)
            {
                xDoc.Add(new XElement("configuration"));
            }

            if (xDoc.Root.Name != "configuration")
            {
                throw new InvalidDataException("'configuration' is the only valid name for root element of web.config file");
            }

            var appSettingsElement = GetOrAddElement(parent: xDoc.Root, name: "appSettings");

            var relativePackagesPath = PathUtility.GetRelativePath(wwwRootOutWebConfigFilePath, root.TargetPackagesPath);
            var defaultRuntime = root.Runtimes.FirstOrDefault();

            var keyValuePairs = new Dictionary<string, string>()
            {
                { "kpm-package-path", relativePackagesPath},
                { "bootstrapper-version", GetBootstrapperVersion(root)},
                { "packages-path", relativePackagesPath},
                { "dotnet-version", GetRuntimeVersion(defaultRuntime)},
                { "dotnet-clr", GetRuntimeFlavor(defaultRuntime)},
                { "dotnet-app-base", _applicationBase},
            };

            foreach (var pair in keyValuePairs)
            {
                var addElement = appSettingsElement.Elements()
                    .Where(x => x.Name == "add" && x.Attribute("key").Value == pair.Key)
                    .SingleOrDefault();
                if (addElement == null)
                {
                    addElement = new XElement("add");
                    addElement.SetAttributeValue("key", pair.Key);
                    appSettingsElement.Add(addElement);
                }

                addElement.SetAttributeValue("value", pair.Value);
            }

            var xmlWriterSettings = new XmlWriterSettings
            {
                Indent = true,
                ConformanceLevel = ConformanceLevel.Auto
            };

            using (var xmlWriter = XmlWriter.Create(File.Create(wwwRootOutWebConfigFilePath), xmlWriterSettings))
            {
                xDoc.WriteTo(xmlWriter);
            }
        }

        private static XElement GetOrAddElement(XElement parent, string name)
        {
            var child = parent.Elements().Where(x => x.Name == name).FirstOrDefault();
            if (child == null)
            {
                child = new XElement(name);
                parent.Add(child);
            }
            return child;
        }

        private static string GetBootstrapperVersion(BundleRoot root)
        {
            // Use version of Microsoft.AspNet.Loader.IIS.Interop as version of bootstrapper
            var package = root.Packages.SingleOrDefault(
                x => string.Equals(x.Library.Name, "Microsoft.AspNet.Loader.IIS.Interop"));
            return package == null ? string.Empty : package.Library.Version.ToString();
        }

        // Expected runtime name format: dotnet-{FLAVOR}-{OS}-{ARCHITECTURE}.{VERSION}
        // Sample input: dotnet-coreclr-win-x86.1.0.0.0
        // Sample output: coreclr
        private static string GetRuntimeFlavor(BundleRuntime runtime)
        {
            if (runtime == null)
            {
                return string.Empty;
            }

            var segments = runtime.Name.Split(new[] { '.' }, 2);
            segments = segments[0].Split(new[] { '-' }, 4);
            return segments[1];
        }

        // Expected runtime name format: dotnet-{FLAVOR}-{OS}-{ARCHITECTURE}.{VERSION}
        // Sample input: dotnet-coreclr-win-x86.1.0.0.0
        // Sample output: 1.0.0.0
        private static string GetRuntimeVersion(BundleRuntime runtime)
        {
            if (runtime == null)
            {
                return string.Empty;
            }

            var segments = runtime.Name.Split(new[] { '.' }, 2);
            return segments[1];
        }

        private static void CopyAspNetLoaderDll(BundleRoot root, string wwwRootOutPath)
        {
            // Tool dlls including AspNet.Loader.dll go to bin folder under public app folder
            var wwwRootOutBinPath = Path.Combine(wwwRootOutPath, "bin");

            // Copy Microsoft.AspNet.Loader.IIS.Interop/tools/*.dll into bin to support AspNet.Loader.dll
            var package = root.Packages.SingleOrDefault(
                x => string.Equals(x.Library.Name, "Microsoft.AspNet.Loader.IIS.Interop"));
            if (package == null)
            {
                return;
            }

            var resolver = new DefaultPackagePathResolver(root.SourcePackagesPath);
            var packagePath = resolver.GetInstallPath(package.Library.Name, package.Library.Version);
            var packageToolsPath = Path.Combine(packagePath, "tools");
            if (Directory.Exists(packageToolsPath))
            {
                foreach (var packageToolFile in Directory.EnumerateFiles(packageToolsPath, "*.dll").Select(Path.GetFileName))
                {
                    // Create the bin folder only when we need to put something inside it
                    if (!Directory.Exists(wwwRootOutBinPath))
                    {
                        Directory.CreateDirectory(wwwRootOutBinPath);
                    }

                    // Copy to bin folder under public app folder
                    File.Copy(
                        Path.Combine(packageToolsPath, packageToolFile),
                        Path.Combine(wwwRootOutBinPath, packageToolFile),
                        overwrite: true);
                }
            }
        }

        private void CopyContentFiles(BundleRoot root, Runtime.Project project, string targetFolderPath)
        {
            root.Reports.Quiet.WriteLine("Copying contents of {0} dependency {1} to {2}",
                _libraryDescription.Type, _libraryDescription.Identity.Name, targetFolderPath);

            var contentSourcePath = GetWwwRootSourcePath(project.ProjectDirectory, WwwRoot);

            root.Reports.Quiet.WriteLine("  Source {0}", contentSourcePath);
            root.Reports.Quiet.WriteLine("  Target {0}", targetFolderPath);

            root.Operations.Copy(contentSourcePath, targetFolderPath);
        }

        private static string GetWwwRootSourcePath(string projectDirectory, string wwwRoot)
        {
            wwwRoot = wwwRoot ?? string.Empty;
            var wwwRootSourcePath = Path.Combine(projectDirectory, wwwRoot);

            // If the value of '--wwwroot' is ".", we need to bundle the project root dir
            // Use Path.GetFullPath() to get rid of the trailing "."
            return Path.GetFullPath(wwwRootSourcePath);
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
