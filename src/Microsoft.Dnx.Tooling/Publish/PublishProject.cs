// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Versioning;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Linq;
using Microsoft.Dnx.Runtime;
using Microsoft.Extensions.PlatformAbstractions;
using Newtonsoft.Json.Linq;
using NuGet;

namespace Microsoft.Dnx.Tooling.Publish
{
    public class PublishProject
    {
        private readonly ProjectDescription _projectDescription;
        private string _relativeAppBase;

        public PublishProject(ProjectDescription projectDescription)
        {
            _projectDescription = projectDescription;
        }

        public string ApplicationBasePath { get; set; }
        public LibraryIdentity Library { get { return _projectDescription.Identity; } }
        public string TargetPath { get; private set; }
        public string WwwRoot { get; set; }
        public string WwwRootOut { get; set; }
        public bool IsPackage { get; private set; }

        public bool Emit(PublishRoot root)
        {
            root.Reports.Quiet.WriteLine("Using {0} dependency {1} for {2}", _projectDescription.Type,
                _projectDescription.Identity, _projectDescription.Framework.ToString().Yellow().Bold());

            var success = true;

            if (root.NoSource || IsWrappingAssembly())
            {
                success = EmitNupkg(root);
            }
            else
            {
                success = EmitSource(root);
            }

            root.Reports.Quiet.WriteLine();

            return success;
        }

        private bool EmitSource(PublishRoot root)
        {
            root.Reports.Quiet.WriteLine("  Copying source code from {0} dependency {1}",
                _projectDescription.Type, _projectDescription.Identity.Name);

            var project = GetCurrentProject();
            if (project == null)
            {
                return false;
            }

            var targetName = project.Name;
            TargetPath = Path.Combine(
                root.OutputPath,
                PublishRoot.AppRootName,
                PublishRoot.SourceFolderName,
                targetName);

            // If root.OutputPath is specified by --out option, it might not be a full path
            TargetPath = Path.GetFullPath(TargetPath);

            root.Reports.Quiet.WriteLine("    Source {0}", _projectDescription.Path.Bold());
            root.Reports.Quiet.WriteLine("    Target {0}", TargetPath);

            root.Operations.Delete(TargetPath);

            CopyProject(root, project, TargetPath, includeSource: true);

            CopyRelativeSources(project);

            UpdateWebRoot(root, TargetPath);

            var appBase = Path.Combine(PublishRoot.AppRootName, "src", project.Name);

            _relativeAppBase = Path.Combine("..", appBase);
            ApplicationBasePath = Path.Combine(root.OutputPath, appBase);

            return true;
        }

        private bool EmitNupkg(PublishRoot root)
        {
            root.Reports.Quiet.WriteLine("  Packing nupkg from {0} dependency {1}",
                _projectDescription.Type, _projectDescription.Identity.Name);

            IsPackage = true;

            var project = GetCurrentProject();
            if (project == null)
            {
                return false;
            }

            var resolver = new DefaultPackagePathResolver(root.TargetPackagesPath);
            var targetNupkg = resolver.GetPackageFileName(project.Name, project.Version);
            TargetPath = resolver.GetInstallPath(project.Name, project.Version);

            root.Reports.Quiet.WriteLine("    Source {0}", _projectDescription.Path.Bold());
            root.Reports.Quiet.WriteLine("    Target {0}", TargetPath);

            if (Directory.Exists(TargetPath))
            {
                root.Operations.Delete(TargetPath);
            }

            // Make sure we only emit the nupkgs for the specified frameworks
            // We need to pick actual project frameworks relevant to the publish
            // but the project may not target exactly the right framework so we need
            // to use a compatibility check.
            var selectedFrameworks = SelectCompatibleFrameworks(root, project, root.Frameworks.Keys);
            if(selectedFrameworks == null)
            {
                return false;
            }

            // If this is a wrapper project, we need to generate a lock file before building it
            if (IsWrappingAssembly())
            {
                var success = Restore(root, publishProject: this, restoreDirectory: project.ProjectDirectory, targetFrameworks: selectedFrameworks)
                    .GetAwaiter().GetResult();
                if (!success)
                {
                    return false;
                }
            }

            // Generate nupkg from this project dependency
            var buildOptions = new BuildOptions();
            buildOptions.ProjectPatterns.Add(project.ProjectDirectory);
            buildOptions.OutputDir = Path.Combine(project.ProjectDirectory, "bin");
            buildOptions.Configurations.Add(root.Configuration);
            buildOptions.GeneratePackages = true;
            buildOptions.Reports = root.Reports.ShallowCopy();

            foreach (var selectedFramework in selectedFrameworks)
            {
                buildOptions.TargetFrameworks.Add(selectedFramework, VersionUtility.GetShortFrameworkName(selectedFramework));
            }

            // Mute "dnu pack" completely if it is invoked by "dnu publish --quiet"
            buildOptions.Reports.Information = root.Reports.Quiet;

            var buildManager = new BuildManager(buildOptions);
            if (!buildManager.Build())
            {
                return false;
            }

            // Extract the generated nupkg to target path
            var srcNupkgPath = Path.Combine(buildOptions.OutputDir, root.Configuration, targetNupkg);
            var srcSymbolsNupkgPath = Path.ChangeExtension(srcNupkgPath, "symbols.nupkg");

            var options = new Packages.AddOptions
            {
                NuGetPackage = root.IncludeSymbols ? srcSymbolsNupkgPath : srcNupkgPath,
                SourcePackages = root.TargetPackagesPath,
                Reports = root.Reports
            };

            var packagesAddCommand = new Packages.AddCommand(options);
            packagesAddCommand.Execute().GetAwaiter().GetResult();

            // Copy content files (e.g. html, js and images) of main project into "root" folder of the exported package
            var rootFolderPath = Path.Combine(TargetPath, "root");
            var rootProjectJson = Path.Combine(rootFolderPath, Runtime.Project.ProjectFileName);

            root.Operations.Delete(rootFolderPath);
            CopyProject(root, project, rootFolderPath, includeSource: false);

            UpdateWebRoot(root, rootFolderPath);

            UpdateJson(rootProjectJson, jsonObj =>
            {
                // Update the project entrypoint
                jsonObj["entryPoint"] = _projectDescription.Identity.Name;

                // Set mark this as non loadable
                jsonObj["loadable"] = false;

                // Update the dependencies node to reference the main project
                var deps = new JObject();
                jsonObj["dependencies"] = deps;

                deps[_projectDescription.Identity.Name] = _projectDescription.Identity.Version.ToString();
            });

            var appBase = Path.Combine(PublishRoot.AppRootName, "packages", resolver.GetPackageDirectory(_projectDescription.Identity.Name, _projectDescription.Identity.Version), "root");

            _relativeAppBase = Path.Combine("..", appBase);
            ApplicationBasePath = Path.Combine(root.OutputPath, appBase);

            root.Reports.Quiet.WriteLine("Removing {0}", srcNupkgPath);
            File.Delete(srcNupkgPath);

            root.Reports.Quiet.WriteLine("Removing {0}", srcSymbolsNupkgPath);
            File.Delete(srcSymbolsNupkgPath);

            return true;
        }

        private IEnumerable<FrameworkName> SelectCompatibleFrameworks(PublishRoot root, Runtime.Project project, IEnumerable<FrameworkName> requestedFrameworks)
        {
            var selectedFrameworks = new List<FrameworkName>();
            foreach (var framework in requestedFrameworks)
            {
                var selectedFramework = project.GetCompatibleTargetFramework(framework);
                if (selectedFramework == null)
                {
                    root.Reports.WriteError($"Unable to build {project.Name}. It is not compatible with the requested target framework: {framework}");
                    return null;
                }
                selectedFrameworks.Add(selectedFramework.FrameworkName);
            }
            return selectedFrameworks;
        }

        private void CopyRelativeSources(Runtime.Project project)
        {
            // We can reference source files outside of project root with "code" property in project.json,
            // e.g. { "code" : "..\\ExternalProject\\**.cs" }
            // So we find out external source files and copy them separately
            var rootDirectory = ProjectResolver.ResolveRootDirectory(project.ProjectDirectory);
            foreach (var sourceFile in project.Files.SourceFiles)
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
                        string.Format("TODO: Warning: the referenced source file '{0}' is not in solution root and it is not published to output.", sourceFile));
                }
            }
        }

        private void UpdateWebRoot(PublishRoot root, string targetPath)
        {
            // Update the 'webroot' property, which was specified with '--wwwroot-out' option
            if (!string.IsNullOrEmpty(WwwRootOut))
            {
                var hostingConfig = Path.Combine(targetPath, "hosting.json");
                if (!File.Exists(hostingConfig))
                {
                    File.AppendAllText(hostingConfig, $@"{{
    ""webroot"": ""{PathUtility.GetRelativePath(hostingConfig, Path.Combine(root.OutputPath, WwwRootOut), separator: '/')}""
}}");
                }
                else
                {
                    UpdateJson(hostingConfig, jsonObj =>
                    {
                        var targetWebRootPath = Path.Combine(root.OutputPath, WwwRootOut);
                        PublishOperations.SetWebRootJson(jsonObj, PathUtility.GetRelativePath(hostingConfig, targetWebRootPath, separator: '/'));
                    });
                }
            }
        }

        private static void UpdateJson(string jsonFile, Action<JObject> modifier)
        {
            var jsonObj = JObject.Parse(File.ReadAllText(jsonFile));
            modifier(jsonObj);
            File.WriteAllText(jsonFile, jsonObj.ToString());
        }

        private void CopyProject(PublishRoot root, Runtime.Project project, string targetPath, bool includeSource)
        {
            var additionalExcluding = new List<string>();

            // If a public folder is specified with 'webroot' or '--wwwroot', we ignore it when copying project files
            var wwwRootPath = string.Empty;
            if (!string.IsNullOrEmpty(WwwRoot))
            {
                wwwRootPath = Path.GetFullPath(Path.Combine(project.ProjectDirectory, WwwRoot));
                wwwRootPath = PathUtility.EnsureTrailingSlash(wwwRootPath);
            }

            // If project root is used as value of '--wwwroot', we shouldn't exclude it when copying
            if (string.Equals(wwwRootPath, PathUtility.EnsureTrailingSlash(project.ProjectDirectory)))
            {
                wwwRootPath = string.Empty;
            }

            if (!string.IsNullOrEmpty(wwwRootPath))
            {
                additionalExcluding.Add(wwwRootPath.Substring(PathUtility.EnsureTrailingSlash(project.ProjectDirectory).Length));
            }

            var sourceFiles = project.Files.GetFilesForBundling(includeSource, additionalExcluding);
            root.Operations.Copy(sourceFiles, project.ProjectDirectory, targetPath);
        }

        public FrameworkName SelectFrameworkForRuntime(PublishRuntime runtime)
        {
            return runtime.SelectBestFramework(
                GetCurrentProject().GetTargetFrameworks().Select(f => f.FrameworkName));
        }

        public bool PostProcess(PublishRoot root)
        {
            // At this point, all nupkgs generated from dependency projects are available in packages folder
            // So we can add them to lockfile now
            if (!UpdateLockFile(root))
            {
                return false;
            }

            // Prune the packages folder only leaving things that are required
            PrunePackages(root);

            // If --wwwroot-out doesn't have a non-empty value, we don't need a public app folder in output
            if (string.IsNullOrEmpty(WwwRootOut))
            {
                return true;
            }

            var project = GetCurrentProject();

            // Construct path to public app folder, which contains content files and tool dlls
            // The name of public app folder is specified with "--appfolder" option
            // Default name of public app folder is the same as main project
            var wwwRootOutPath = Path.Combine(root.OutputPath, WwwRootOut);

            // Delete old public app folder because we don't want leftovers from previous operations
            root.Operations.Delete(wwwRootOutPath);
            Directory.CreateDirectory(wwwRootOutPath);

            // Copy content files (e.g. html, js and images) of main project into public app folder
            CopyContentFiles(root, project, wwwRootOutPath);

            return GenerateWebConfigFileForWwwRootOut(root, project, wwwRootOutPath);
        }

        private void PrunePackages(PublishRoot root)
        {
            if (root.MainProjectLockFile == null)
            {
                return;
            }

            var resolver = new DefaultPackagePathResolver(root.TargetPackagesPath);

            var lockFilePath = Path.GetFullPath(Path.Combine(ApplicationBasePath, LockFileFormat.LockFileName));
            root.PublishedLockFile = new LockFileFormat().Read(lockFilePath);

            var filesToKeep = new HashSet<string>();
            var filesToRemove = new HashSet<string>();

            foreach (var target in root.PublishedLockFile.Targets)
            {
                foreach (var library in target.Libraries)
                {
                    var packageDir = resolver.GetInstallPath(library.Name, library.Version);

                    if (library.Name != root.MainProjectName && string.Equals(library.Type, Runtime.LibraryTypes.Package, StringComparison.OrdinalIgnoreCase))
                    {
                        filesToRemove.Add(resolver.GetHashPath(library.Name, library.Version));
                        filesToRemove.Add(resolver.GetPackageFilePath(library.Name, library.Version));
                        filesToRemove.AddRange(Directory.EnumerateFiles(packageDir, $"{library.Name}.xml", SearchOption.AllDirectories));
                    }

                    foreach (var path in library.RuntimeAssemblies)
                    {
                        filesToKeep.Add(Path.Combine(packageDir, path));
                    }
                    foreach (var path in library.CompileTimeAssemblies)
                    {
                        filesToKeep.Add(Path.Combine(packageDir, path));
                    }
                    foreach (var path in library.NativeLibraries)
                    {
                        filesToKeep.Add(Path.Combine(packageDir, path));
                    }
                    foreach (var path in library.ResourceAssemblies)
                    {
                        filesToKeep.Add(Path.Combine(packageDir, path));
                    }
                }
            }

            foreach (var target in root.MainProjectLockFile.Targets.Where(projectTarget =>
                string.IsNullOrEmpty(projectTarget.RuntimeIdentifier) &&
                !root.PublishedLockFile.Targets.Any(publishTarget =>
                projectTarget.TargetFramework == publishTarget.TargetFramework)))
            {
                foreach (var library in target.Libraries)
                {
                    var packageDir = resolver.GetInstallPath(library.Name, library.Version);

                    if (Directory.Exists(packageDir))
                    {
                        foreach (var path in library.RuntimeAssemblies)
                        {
                            filesToRemove.Add(Path.Combine(packageDir, path));
                        }
                        foreach (var path in library.CompileTimeAssemblies)
                        {
                            filesToRemove.Add(Path.Combine(packageDir, path));
                        }
                        foreach (var path in library.NativeLibraries)
                        {
                            filesToRemove.Add(Path.Combine(packageDir, path));
                        }
                        foreach (var path in library.ResourceAssemblies)
                        {
                            filesToRemove.Add(Path.Combine(packageDir, path));
                        }
                    }
                }
            }

            foreach (var file in filesToRemove.Except(filesToKeep))
            {
                File.Delete(file);
            }

            if (Directory.Exists(root.TargetPackagesPath))
            {
                root.Operations.DeleteEmptyFolders(root.TargetPackagesPath);
            }

            return;
        }

        private async Task<bool> Restore(PublishRoot root, PublishProject publishProject, string restoreDirectory, IEnumerable<FrameworkName> targetFrameworks)
        {
            var appEnv = PlatformServices.Default.Application;

            var feedOptions = new FeedOptions();
            feedOptions.IgnoreFailedSources = true;
            feedOptions.Sources.Add(root.TargetPackagesPath);
            feedOptions.TargetPackagesFolder = root.TargetPackagesPath;

            var restoreCommand = new RestoreCommand(appEnv);

            restoreCommand.TargetFrameworks.AddRange(targetFrameworks);
            restoreCommand.RequestedRuntimes = root.RuntimeIdentifiers;
            restoreCommand.SkipRestoreEvents = true;
            restoreCommand.SkipInstall = true;
            // This is a workaround for #1322. Since we use restore to generate the lock file
            // after publish, it's possible to fail restore after copying the closure
            // if framework assemblies and packages have the same name. This is more likely now
            // since dependencies may exist in the top level
            restoreCommand.IgnoreMissingDependencies = true;
            restoreCommand.CheckHashFile = false;
            restoreCommand.RestoreDirectories.Add(restoreDirectory);
            restoreCommand.FeedOptions = feedOptions;

            // Mute "dnu restore" subcommand
            restoreCommand.Reports = Reports.Constants.NullReports;

            var success = await restoreCommand.Execute();
            return success;
        }

        private bool UpdateLockFile(PublishRoot root)
        {
            var tasks = new Task<bool>[root.Projects.Count];
            for (int i = 0; i < root.Projects.Count; i++)
            {
                var project = root.Projects[i];
                var restoreDirectory = project.IsPackage ? Path.Combine(project.TargetPath, "root") : project.TargetPath;
                tasks[i] = Restore(root, project, restoreDirectory, SelectCompatibleFrameworks(root, project.GetCurrentProject(), root.Frameworks.Keys));
            }

            Task.WaitAll(tasks);

            return tasks.All(t => t.Result);
        }

        private bool GenerateWebConfigFileForWwwRootOut(PublishRoot root, Runtime.Project project, string wwwRootOutPath)
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

            // <system.webServer>
            //   <handlers>
            //    <add name="httpplatformhandler" path="*" verb="*" modules="httpPlatformHandler" resourceType="Unspecified" />
            //  </handlers>
            //  <httpPlatform processPath="..\command.cmd"
            //                arguments=""
            //                stdoutLogEnabled="true"
            //                stdoutLogFile="..\logs\stdout.log">
            //  </httpPlatform>
            // </system.webServer>

            // Look for specified command

            var command = root.IISCommand;

            if (!string.IsNullOrEmpty(command) && !project.Commands.ContainsKey(command))
            {
                root.Reports.WriteError($"Specified command {command} cannot be found.");
                return false;
            }
            else if (project.Commands.Count == 1)
            {
                command = project.Commands.First().Key;
            }
            else if (project.Commands.Count == 0)
            {
                root.Reports.WriteWarning("No commands defined. Defaulting to web.");
            }
            else
            {
                root.Reports.WriteWarning("Multiple commands defined. Defaulting to web.");
            }

            command = command ?? "web";

            root.Reports.Information.WriteLine($"Using command '{command}' as the entry point for web.config.");

            var azurePublishValue = Environment.GetEnvironmentVariable("DNU_PUBLISH_AZURE");
            var publishingToAzure = string.Equals(azurePublishValue, "true", StringComparison.Ordinal) ||
                                    string.Equals(azurePublishValue, "1", StringComparison.Ordinal) ||
                                    !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("WEBSITE_SITE_NAME"));

            var basePath = publishingToAzure ? @"%home%\site" : "..";
            var baseLogPath = publishingToAzure ? @"\\?\%home%\LogFiles" : @"..\logs";

            var targetDocument = XDocument.Parse($@"<configuration><system.webServer>
  <handlers>
    <add name=""httpplatformhandler"" path=""*"" verb=""*"" modules=""httpPlatformHandler"" resourceType=""Unspecified"" />
  </handlers>
  <httpPlatform processPath=""{basePath}\approot\{command}.cmd""
                arguments=""""
                stdoutLogEnabled=""true""
                stdoutLogFile=""{baseLogPath}\stdout.log"">
  </httpPlatform>
</system.webServer></configuration>");

            var attributesToOverwrite = new[]
            {
                "processPath",
                "arguments"
            };

            var result = xDoc.Root.MergeWith(targetDocument.Root, (name, sourceChild, targetChild) =>
            {
                if (sourceChild != null)
                {
                    // We're only going to merge the httpPlatform element attributes we don't care about
                    if (string.Equals(name.LocalName, "httpPlatform"))
                    {
                        foreach (var attr in sourceChild.Attributes())
                        {
                            if (!attributesToOverwrite.Contains(attr.Name.LocalName))
                            {
                                targetChild.SetAttributeValue(attr.Name, attr.Value);
                            }
                        }
                    }

                    targetChild.Remove();
                    sourceChild.Parent.Add(targetChild);
                    sourceChild.Remove();
                    return true;
                }

                return false;
            });

            var xmlWriterSettings = new XmlWriterSettings
            {
                Indent = true,
                ConformanceLevel = ConformanceLevel.Auto
            };

            using (var xmlWriter = XmlWriter.Create(File.Create(wwwRootOutWebConfigFilePath), xmlWriterSettings))
            {
                result.WriteTo(xmlWriter);
            }

            // Create the logs directory so the httpPlatformHandler can write there
            Directory.CreateDirectory(Path.Combine(root.OutputPath, "logs"));

            return true;
        }

        private void CopyContentFiles(PublishRoot root, Runtime.Project project, string targetFolderPath)
        {
            root.Reports.Quiet.WriteLine("Copying contents of {0} dependency {1} to {2}",
                _projectDescription.Type, _projectDescription.Identity.Name, targetFolderPath);

            var contentSourcePath = GetWwwRootSourcePath(project.ProjectDirectory, WwwRoot);

            root.Reports.Quiet.WriteLine("  Source {0}", contentSourcePath);
            root.Reports.Quiet.WriteLine("  Target {0}", targetFolderPath);

            if (Directory.Exists(contentSourcePath))
            {
                root.Operations.Copy(contentSourcePath, targetFolderPath);
            }
        }

        private static string GetWwwRootSourcePath(string projectDirectory, string wwwRoot)
        {
            wwwRoot = wwwRoot ?? string.Empty;
            var wwwRootSourcePath = Path.Combine(projectDirectory, wwwRoot);

            // If the value of '--wwwroot' is ".", we need to publish the project root dir
            // Use Path.GetFullPath() to get rid of the trailing "."
            return Path.GetFullPath(wwwRootSourcePath);
        }

        private Runtime.Project GetCurrentProject()
        {
            return _projectDescription.Project;
        }

        private bool IsWrappingAssembly()
        {
            /* If this project is wrapping an assembly, the project.json has the format like:
            {
              "frameworks": {
                "dnx451": {
                  "bin": {
                    "assembly": "relative/path/to/ClassLibrary1.dll"
                  }
                }
              }
            } */
            var project = GetCurrentProject();
            return project.GetTargetFrameworks().Any(f => !string.IsNullOrEmpty(f.AssemblyPath));
        }
    }
}
