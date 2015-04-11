// Copyright (c) Microsoft Open Technologies, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Versioning;
using System.Xml.Linq;
using Microsoft.Framework.Runtime;
using Newtonsoft.Json.Linq;
using NuGet;
using NuGet.Configuration;
using Settings = NuGet.Configuration.Settings;

namespace Microsoft.Framework.PackageManager
{
    public class WrapCommand
    {
        private string _referenceResolverPath;
        private static readonly string ReferenceResolverFileName = "ReferenceResolver.xml";
        private static readonly string WrapperProjectVersion = "1.0.0-*";
        private static readonly char PathSeparator = '/';
        private static readonly string WrapRootName = "wrap";

        public string InputFilePath { get; set; }
        public string Configuration { get; set; }
        public string MsBuildPath { get; set; }
        public bool InPlace { get; set; }
        public string Framework { get; set; }
        public Reports Reports { get; set; }

        public bool ExecuteCommand()
        {
            if (string.IsNullOrEmpty(InputFilePath))
            {
                Reports.Error.WriteLine("Please specify an input file to wrap");
                return false;
            }

            if (!File.Exists(InputFilePath))
            {
                Reports.Error.WriteLine("Input file {0} doesn't exist", InputFilePath.Red().Bold());
                return false;
            }

            var extension = Path.GetExtension(InputFilePath);
            if (string.Equals(extension, ".csproj", StringComparison.OrdinalIgnoreCase))
            {
                if (PlatformHelper.IsMono)
                {
                    Reports.Error.WriteLine("Wrapping csproj is not supported on non-Windows platforms");
                    return false;
                }

                return WrapCsProject();
            }
            else if (string.Equals(extension, ".dll", StringComparison.OrdinalIgnoreCase))
            {
                return WrapAssembly();
            }

            Reports.Error.WriteLine("Then extension of input file can only be .csproj or .dll".Red());
            return false;
        }

        private bool WrapCsProject()
        {
            if (string.IsNullOrEmpty(Configuration))
            {
                Configuration = "debug";
            }

            MsBuildPath = string.IsNullOrEmpty(MsBuildPath) ? GetDefaultMSBuildPath() : MsBuildPath;

            XDocument resolutionResults;
            string errorMessage;
            if (!TryResolveReferences(out resolutionResults, out errorMessage))
            {
                Reports.Error.WriteLine(errorMessage);
                return false;
            }

            foreach (var projectElement in resolutionResults.Root.Elements())
            {
                EmitProjectWrapper(projectElement);
            }

            return true;
        }

        private bool WrapAssembly()
        {
            if (string.IsNullOrEmpty(Framework))
            {
                Reports.Error.WriteLine("Please specify framework when wrapping an assembly", Framework.Red().Bold());
                return false;
            }

            var frameworkName = VersionUtility.ParseFrameworkName(Framework);
            if (VersionUtility.UnsupportedFrameworkName.Equals(frameworkName))
            {
                Reports.Error.WriteLine("The framework '{0}' is not supported", Framework.Red().Bold());
                return false;
            }

            var rootDir = ProjectResolver.ResolveRootDirectory(Directory.GetCurrentDirectory());
            var wrapRoot = Path.Combine(rootDir, WrapRootName);
            EmitAssemblyWrapper(wrapRoot, frameworkName, InputFilePath);

            AddWrapFolderToGlobalJson(rootDir);

            return true;
        }

        private bool TryResolveReferences(out XDocument resolutionResults, out string errorMessage)
        {
            resolutionResults = new XDocument();
            errorMessage = string.Empty;

            if (!File.Exists(MsBuildPath))
            {
                errorMessage = string.Format("Unable to locate {0}", MsBuildPath.Red().Bold());
                return false;
            }

            // Put ReferenceResolver.xml and intermediate result files into a temporary dir
            var tempDir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
            Directory.CreateDirectory(tempDir);

            try
            {
                _referenceResolverPath = Path.Combine(tempDir, ReferenceResolverFileName);
                var assembly = typeof(WrapCommand).GetTypeInfo().Assembly;
                var embeddedResourceName = "Microsoft.Framework.PackageManager.compiler.resources." + ReferenceResolverFileName;
                using (var stream = assembly.GetManifestResourceStream(embeddedResourceName))
                {
                    File.WriteAllText(_referenceResolverPath, stream.ReadToEnd());
                }

                resolutionResults.Add(new XElement("root"));
                var projectFiles = new List<string> { Path.GetFullPath(InputFilePath) };
                var intermediateResultFile = Path.Combine(tempDir, Path.GetRandomFileName());

                for (var i = 0; i != projectFiles.Count; i++)
                {
                    var properties = new[]
                    {
                        string.Format("/p:CustomAfterMicrosoftCommonTargets=\"{0}\"", _referenceResolverPath),
                        string.Format("/p:ResultFilePath=\"{0}\"", intermediateResultFile),
                        string.Format("/p:Configuration={0}", Configuration),
                        "/p:DesignTimeBuild=true",
                        "/p:BuildProjectReferences=false",
                        "/p:_ResolveReferenceDependencies=true" // Dump entire assembly reference closure
                    };

                    var msBuildArgs = string.Format("\"{0}\" /t:ResolveAndDump {1}",
                        projectFiles[i], string.Join(" ", properties));

                    Reports.Verbose.WriteLine("Resolving references for {0}", projectFiles[i].Bold());
                    Reports.Verbose.WriteLine();
                    Reports.Verbose.WriteLine("Command:");
                    Reports.Verbose.WriteLine();
                    Reports.Verbose.WriteLine("\"{0}\" {1}", MsBuildPath, msBuildArgs);
                    Reports.Verbose.WriteLine();
                    Reports.Verbose.WriteLine("MSBuild output:");
                    Reports.Verbose.WriteLine();

                    var startInfo = new ProcessStartInfo()
                    {
                        UseShellExecute = false,
                        FileName = MsBuildPath,
                        Arguments = msBuildArgs,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true
                    };

                    var process = Process.Start(startInfo);
                    var stdOut = process.StandardOutput.ReadToEnd();
                    var stdErr = process.StandardError.ReadToEnd();
                    process.WaitForExit();

                    Reports.Verbose.WriteLine(stdOut);

                    if (process.ExitCode != 0)
                    {
                        errorMessage = string.Format("Failed to resolve references for {0}",
                            projectFiles[i].Red().Bold());
                        return false;
                    }

                    var projectXDoc = XDocument.Parse(File.ReadAllText(intermediateResultFile));

                    foreach (var item in GetItemsByType(projectXDoc.Root, type: "ProjectReference"))
                    {
                        var relativePath = item.Attribute("evaluated").Value;
                        var fullPath = PathUtility.GetAbsolutePath(projectFiles[i], relativePath);
                        if (!projectFiles.Contains(fullPath))
                        {
                            projectFiles.Add(fullPath);
                        }
                    }

                    resolutionResults.Root.Add(projectXDoc.Root);
                }
            }
            finally
            {
                FileOperationUtils.DeleteFolder(tempDir);
            }

            return true;
        }

        private string GetDefaultMSBuildPath()
        {
#if DNX451
            var programFilesPath = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);
#else
            var programFilesPath = Environment.GetEnvironmentVariable("PROGRAMFILES(X86)");
#endif
            // On 32-bit Windows
            if (string.IsNullOrEmpty(programFilesPath))
            {
#if DNX451
                programFilesPath = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
#else
                programFilesPath = Environment.GetEnvironmentVariable("PROGRAMFILES");
#endif
            }

            return Path.Combine(programFilesPath, "MSBuild", "14.0", "Bin", "MSBuild.exe");
        }

        private static void AddWrapFolderToGlobalJson(string rootDir)
        {
            var globalJsonPath = Path.Combine(rootDir, GlobalSettings.GlobalFileName);
            var rootObj = LoadOrCreateJson(globalJsonPath);
            var projectsArray = GetOrCreateProjectsArray(rootObj);

            if (!projectsArray.Any(x => string.Equals(x.Value<string>(), WrapRootName, StringComparison.OrdinalIgnoreCase)))
            {
                projectsArray.Add(WrapRootName);
                File.WriteAllText(globalJsonPath, rootObj.ToString());
            }
        }

        private void EmitProjectWrapper(XElement projectElement)
        {
            var projectFile = Path.GetFullPath(projectElement.Attribute("projectFile").Value);

            // Name of the wrapper project is output assembly name, instead of .csproj file name
            var outputAssemblyPath = GetOutputAssemblyPath(projectElement);
            outputAssemblyPath = GetConfigAgnosticAssemblyPath(outputAssemblyPath);
            var projectName = Path.GetFileNameWithoutExtension(outputAssemblyPath);

            var projectDir = Path.GetDirectoryName(projectFile);
            var rootDir = ProjectResolver.ResolveRootDirectory(projectDir);
            var wrapRoot = Path.Combine(rootDir, WrapRootName);

            string targetProjectJson;
            if (InPlace)
            {
                targetProjectJson = Path.Combine(projectDir, Runtime.Project.ProjectFileName);
            }
            else
            {
                var projectResolver = new ProjectResolver(projectDir, rootDir);
                targetProjectJson = LocateExistingProject(projectResolver, projectName);
                if (string.IsNullOrEmpty(targetProjectJson))
                {
                    AddWrapFolderToGlobalJson(rootDir);
                    targetProjectJson = Path.Combine(wrapRoot, projectName, Runtime.Project.ProjectFileName);
                }
            }

            var targetFramework = GetTargetFramework(projectElement);

            Reports.Information.WriteLine("Wrapping project '{0}' for '{1}'", projectName, targetFramework);
            Reports.Information.WriteLine("  Source {0}", projectFile.Bold());
            Reports.Information.WriteLine("  Target {0}", targetProjectJson.Bold());

            var projectJson = LoadOrCreateProjectJson(targetProjectJson);

            var relativeCsProjectPath = PathUtility.GetRelativePath(targetProjectJson, projectFile, PathSeparator);
            AddWrappedProjectPath(projectJson, relativeCsProjectPath, targetFramework);

            // Add 'assembly' and 'pdb' to 'bin' section of the target framework
            var relativeAssemblyPath = PathUtility.GetRelativePath(targetProjectJson, outputAssemblyPath, PathSeparator);
            var relativePdbPath = Path.ChangeExtension(relativeAssemblyPath, ".pdb");

            Reports.Information.WriteLine("  Adding bin paths for '{0}'", targetFramework);
            Reports.Information.WriteLine("    Assembly: {0}", relativeAssemblyPath.Bold());
            Reports.Information.WriteLine("    Pdb: {0}", relativePdbPath.Bold());
            AddFrameworkBinPaths(projectJson, targetFramework, relativeAssemblyPath, relativePdbPath);

            var nugetPackages = ResolveNuGetPackages(projectDir);
            var nugetPackagePaths = nugetPackages.Select(x => x.Path);

            // Add nuget dependency to 'dependencies' section of the target framework
            foreach (var package in nugetPackages)
            {
                Reports.Information.WriteLine("  Adding package dependency '{0}.{1}'",
                    package.Identity, package.Version);
                AddNuGetDependency(projectJson, package, targetFramework);
            }

            // Add dependency projects to 'dependencies' section of the target framework
            foreach (var itemElement in GetItemsByType(projectElement, type: "ProjectReference"))
            {
                var referenceProjectName = GetMetadataValue(itemElement, "Name");
                var outputName = GetReferenceProjectOutputName(projectElement, referenceProjectName);
                Reports.Information.WriteLine("  Adding project dependency '{0}.{1}'",
                    outputName, WrapperProjectVersion);
                AddProjectDependency(projectJson, outputName, targetFramework);
            }

            // Create wrapper projects for assembly references
            // and add wrapper projects as project references
            foreach (var itemElement in GetItemsByType(projectElement, type: "ReferencePath"))
            {
                if (IsAssemblyFromProjectReference(itemElement) ||
                    IsFrameworkAssembly(itemElement) ||
                    IsAssemblyFromNuGetPackage(itemElement, nugetPackagePaths))
                {
                    continue;
                }

                // This assembly becomes a project reference
                var assemblyPath = itemElement.Attribute("evaluated").Value;
                var assemblyProjectName = Path.GetFileNameWithoutExtension(assemblyPath);

                EmitAssemblyWrapper(wrapRoot, targetFramework, assemblyPath, isSubProcedure: true);

                Reports.Information.WriteLine("  Adding project dependency '{0}.{1}'",
                    assemblyProjectName, WrapperProjectVersion);
                AddProjectDependency(projectJson, assemblyProjectName, targetFramework);
                AddWrapFolderToGlobalJson(rootDir);
            }

            PathUtility.EnsureParentDirectory(targetProjectJson);
            File.WriteAllText(targetProjectJson, projectJson.ToString());

            AddToGlobalJsonSources(rootDir, Path.GetDirectoryName(targetProjectJson));

            Reports.Information.WriteLine();
        }

        private static void AddToGlobalJsonSources(string rootDir, string projectDir)
        {
            var globalJsonPath = Path.Combine(rootDir, GlobalSettings.GlobalFileName);
            var rootObj = LoadOrCreateJson(globalJsonPath);
            var sourcesArray = GetOrCreateProjectsArray(rootObj);

            foreach (var sourceDir in sourcesArray)
            {
                var sourceDirPath = Path.Combine(rootDir, sourceDir.Value<string>());
                if (PathUtility.IsChildOfDirectory(dir: sourceDirPath, candidate: projectDir))
                {
                    // If given project dir is already inside a source dir, we don't need to add it
                    return;
                }
            }

            rootDir = PathUtility.EnsureTrailingSlash(rootDir);
            var projectDirParent = new DirectoryInfo(projectDir).Parent.FullName;
            projectDirParent = PathUtility.EnsureTrailingSlash(projectDirParent);
            var newSourceDir = PathUtility.GetRelativePath(rootDir, projectDirParent);
            // If newSourceDir is an empty string, it means we should add root dir itself as a source dir
            sourcesArray.Add(string.IsNullOrEmpty(newSourceDir) ? "." : newSourceDir);
            File.WriteAllText(globalJsonPath, rootObj.ToString());
        }

        private static JArray GetOrCreateProjectsArray(JObject globalJsonRoot)
        {
            if (globalJsonRoot["projects"] == null)
            {
                globalJsonRoot["projects"] = new JArray();
            }

            var sourcesArray = globalJsonRoot["projects"] as JArray;
            if (sourcesArray == null)
            {
                throw new InvalidDataException(
                    string.Format("The value of 'sources' in {0} must be an array", GlobalSettings.GlobalFileName));
            }

            return sourcesArray;
        }

        private static string LocateExistingProject(IProjectResolver projectResolver, string projectName)
        {
            Runtime.Project project;
            if (projectResolver.TryResolveProject(projectName, out project))
            {
                return project.ProjectFilePath;
            }
            return string.Empty;
        }

        private static string GetReferenceProjectOutputName(XElement projectElement, string referenceProjectName)
        {
            foreach (var itemElement in GetItemsByType(projectElement, "ReferencePath"))
            {
                var name = GetMetadataValue(itemElement, "Name", throwsIfNotFound: false);
                if (string.Equals(name, referenceProjectName))
                {
                    return Path.GetFileNameWithoutExtension(itemElement.Attribute("evaluated").Value);
                }
            }

            throw new InvalidDataException(
                string.Format("Unable to find output assembly name for project reference '{0}' in dumped metadata",
                    referenceProjectName));
        }

        private static bool IsAssemblyFromNuGetPackage(XElement itemElement, IEnumerable<string> nugetPackagePaths)
        {
            var assemblyPath = itemElement.Attribute("evaluated").Value;
            return nugetPackagePaths.Any(x => PathUtility.IsChildOfDirectory(dir: x, candidate: assemblyPath));
        }

        private static bool IsAssemblyFromProjectReference(XElement itemElement)
        {
            var metadataElements = itemElement.Elements("metadata");
            return metadataElements
                .Any(x => string.Equals("ProjectReferenceOriginalItemSpec", x.Attribute("name").Value));
        }

        private static bool IsFrameworkAssembly(XElement itemElement)
        {
            var resolvedFrom = GetMetadataValue(itemElement, "ResolvedFrom", throwsIfNotFound: false);

            if (string.Equals("{TargetFrameworkDirectory}", resolvedFrom) ||
                string.Equals("ImplicitlyExpandDesignTimeFacades", resolvedFrom))
            {
                return true;
            }

            var frameworkFile = GetMetadataValue(itemElement, "FrameworkFile", throwsIfNotFound: false);
            if (string.Equals("true", frameworkFile))
            {
                return true;
            }

            var isSystemReference = GetMetadataValue(itemElement, "IsSystemReference", throwsIfNotFound: false);
            if (string.Equals("True", isSystemReference))
            {
                return true;
            }

            var fileName = GetMetadataValue(itemElement, "Filename");
            if (string.Equals("mscorlib", fileName))
            {
                return true;
            }

            return false;
        }

        private void EmitAssemblyWrapper(string wrapRoot, FrameworkName targetFramework, string assemblyPath,
            string projectName = null, bool isSubProcedure = false)
        {
            if (string.IsNullOrEmpty(projectName))
            {
                projectName = Path.GetFileNameWithoutExtension(assemblyPath);
            }

            var targetProjectJson = Path.Combine(wrapRoot, projectName, Runtime.Project.ProjectFileName);

            var outputIndentation = string.Empty;
            if (isSubProcedure)
            {
                outputIndentation = "  ";
            }

            Reports.Information.WriteLine("{0}Wrapping project '{1}' for '{2}'",
                outputIndentation, projectName, targetFramework);
            Reports.Information.WriteLine("{0}  Source {1}", outputIndentation, assemblyPath.Bold());
            Reports.Information.WriteLine("{0}  Target {1}", outputIndentation, targetProjectJson.Bold());

            // Add 'assembly' and 'pdb' to 'bin' section of the target framework
            var relativeAssemblyPath = PathUtility.GetRelativePath(targetProjectJson, assemblyPath, PathSeparator);
            var pdbPath = GetSideBySidePdbPath(assemblyPath);
            string relativePdbPath = null;
            if (!string.IsNullOrEmpty(pdbPath))
            {
                relativePdbPath = PathUtility.GetRelativePath(targetProjectJson, pdbPath, PathSeparator);
            }

            var projectJson = LoadOrCreateProjectJson(targetProjectJson);
            AddFrameworkBinPaths(projectJson, targetFramework, relativeAssemblyPath, relativePdbPath);

            // Wrap dependency assemblies that are in the same dir
            var assemblyInfo = new AssemblyInformation(assemblyPath, processorArchitecture: null);

            // Key: dependency assembly file name. Value: original dependency name used by dependee.
            var dependencyOriginalName = assemblyInfo.GetDependencies()
                .ToDictionary(x => x + ".dll", x => x, StringComparer.OrdinalIgnoreCase);

            foreach (var filePath in Directory.EnumerateFiles(Path.GetDirectoryName(assemblyPath)))
            {
                var fileName = Path.GetFileName(filePath);
                if (dependencyOriginalName.ContainsKey(fileName))
                {
                    // Project dependency name should be case-sensitive, so here we
                    // correct dependency project name by passing in project name with correct case
                    EmitAssemblyWrapper(wrapRoot, targetFramework, filePath,
                        projectName: dependencyOriginalName[fileName], isSubProcedure: isSubProcedure);
                    AddProjectDependency(projectJson, projectName: dependencyOriginalName[fileName],
                        targetFramework: targetFramework);
                }
            }

            PathUtility.EnsureParentDirectory(targetProjectJson);
            File.WriteAllText(targetProjectJson, projectJson.ToString());
        }

        private static string GetSideBySidePdbPath(string assemblyPath)
        {
            var assemblyName = Path.GetFileNameWithoutExtension(assemblyPath);
            foreach (var filePath in Directory.EnumerateFiles(Path.GetDirectoryName(assemblyPath)))
            {
                if (!filePath.EndsWith(".pdb", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var fileName = Path.GetFileNameWithoutExtension(filePath);
                if (string.Equals(assemblyName, fileName, StringComparison.OrdinalIgnoreCase))
                {
                    return filePath;
                }
            }
            return null;
        }

        private static JObject LoadOrCreateJson(string jsonFilePath)
        {
            if (File.Exists(jsonFilePath))
            {
                return JObject.Parse(File.ReadAllText(jsonFilePath));
            }
            else
            {
                return new JObject();
            }
        }

        private static JObject LoadOrCreateProjectJson(string jsonFilePath)
        {
            var projectJson = LoadOrCreateJson(jsonFilePath);
            SetPropertyValueIfEmpty(projectJson, "version", WrapperProjectVersion);
            return projectJson;
        }

        private static IEnumerable<XElement> GetItemsByType(XElement projectElement, string type)
        {
            return projectElement.Elements("item").Where(x => x.Attribute("itemType").Value == type);
        }

        private static string GetOutputAssemblyPath(XElement projectElement)
        {
            const string projectOutputItemType = "BuiltProjectOutputGroupKeyOutput";
            var itemElement = GetItemsByType(projectElement, type: projectOutputItemType).SingleOrDefault();
            if (itemElement == null)
            {
                throw new InvalidDataException(string.Format("'{0}' is missing from MSBuild output", projectOutputItemType));
            }
            return itemElement.Attribute("evaluated").Value;
        }

        private static string GetConfigAgnosticAssemblyPath(string outputAssemblyPath)
        {
            // Convert "obj/Debug/assembly.dll" and "obj/Release/assembly.dll" to
            // "obj/{configuration}/assembly.dll"
            var assemblyFile = Path.GetFileName(outputAssemblyPath);
            var configFolderPath = Path.GetDirectoryName(outputAssemblyPath);
            var objFolderPath = Path.GetDirectoryName(configFolderPath);
            return Path.Combine(objFolderPath, "{configuration}", assemblyFile);
        }

        private static FrameworkName GetTargetFramework(XElement projectElement)
        {
            var propertyElements = projectElement.Elements().Where(x => x.Name == "property");
            var targetFrameworkMonikerElement = propertyElements
                .Where(x => x.Attribute("name").Value == "TargetFrameworkMoniker")
                .SingleOrDefault();
            return new FrameworkName(targetFrameworkMonikerElement?.Attribute("evaluated")?.Value);
        }

        private static void AddNuGetDependency(JObject projectJson, NuGetPackage nugetPackage,
            FrameworkName targetFramework)
        {
            var frameworksObj = GetOrAddJObject(projectJson, "frameworks");
            var targetFrameworkObj = GetOrAddJObject(frameworksObj, GetShortFrameworkName(targetFramework));
            var dependenciesObj = GetOrAddJObject(targetFrameworkObj, "dependencies");
            dependenciesObj[nugetPackage.Identity] = nugetPackage.Version;
        }

        private static void AddProjectDependency(JObject projectJson, string projectName, FrameworkName targetFramework)
        {
            var frameworksObj = GetOrAddJObject(projectJson, "frameworks");
            var targetFrameworkObj = GetOrAddJObject(frameworksObj, GetShortFrameworkName(targetFramework));
            var dependenciesObj = GetOrAddJObject(targetFrameworkObj, "dependencies");
            SetPropertyValueIfEmpty(dependenciesObj, projectName, WrapperProjectVersion);
        }

        private static string GetShortFrameworkName(FrameworkName targetFramework)
        {
            const string portablePrefix = "portable-";
            var shortName = VersionUtility.GetShortFrameworkName(targetFramework);

            // Project.json doesn't accept framework names with portable prefix (e.g. "portable-net45+sl50+win+wpa81")
            // So we strip off the prefix here
            if (shortName.StartsWith(portablePrefix))
            {
                shortName = shortName.Substring(portablePrefix.Length);
            }

            return shortName;
        }

        private static void AddWrappedProjectPath(JObject projectJson, string relativeCsProjectPath, FrameworkName targetFramework)
        {
            var frameworksObj = GetOrAddJObject(projectJson, "frameworks");
            var targetFrameworkObj = GetOrAddJObject(frameworksObj, GetShortFrameworkName(targetFramework));
            targetFrameworkObj["wrappedProject"] = relativeCsProjectPath;
        }

        private static void AddFrameworkBinPaths(JObject projectJson, FrameworkName targetFramework,
            string assemblyPath, string pdbPath = null)
        {
            var frameworksObj = GetOrAddJObject(projectJson, "frameworks");
            var targetFrameworkObj = GetOrAddJObject(frameworksObj, GetShortFrameworkName(targetFramework));
            var binObj = GetOrAddJObject(targetFrameworkObj, "bin");
            binObj["assembly"] = assemblyPath;

            if (!string.IsNullOrEmpty(pdbPath))
            {
                binObj["pdb"] = pdbPath;
            }
        }

        private static JObject GetOrAddJObject(JObject parent, string propertyName)
        {
            var obj = parent[propertyName] as JObject;
            if (obj == null)
            {
                obj = new JObject();
                parent[propertyName] = obj;
            }
            return obj;
        }

        private static IEnumerable<NuGetPackage> ResolveNuGetPackages(string projectDir)
        {
            var configFile = Path.Combine(projectDir, "packages.config");
            var packages = new List<NuGetPackage>();

            if (File.Exists(configFile))
            {
                var packagesDir = ResolvePackagesDirectory(projectDir);
                var xDoc = XDocument.Parse(File.ReadAllText(configFile));
                var packageElements = xDoc.Root.Elements()
                    .Where(x => string.Equals(x.Name.ToString(), "package", StringComparison.OrdinalIgnoreCase));
                foreach (var packageElement in packageElements)
                {
                    var id = packageElement.Attribute("id").Value;
                    var version = packageElement.Attribute("version").Value;
                    packages.Add(new NuGetPackage
                    {
                        Identity = id,
                        Version = version,
                        TargetFramework = packageElement.Attribute("targetFramework")?.Value,
                        Path = GetNuGetPackagePath(packagesDir, id, version)
                    });
                }
            }

            return packages;
        }

        private static string GetNuGetPackagePath(string packagesDir, string id, string version)
        {
            // Layout of NuGet packages used by csprojs is:
            // C:\Solution\packages\{PackageName}.{Version}\
            // which is different from the layout used by kprojs:
            // C:\Solution\packages\{PackageName}\{Version}\
            return Path.Combine(packagesDir, id + "." + version);
        }

        private static string ResolvePackagesDirectory(string projectDir)
        {
            var rootDir = ProjectResolver.ResolveRootDirectory(projectDir);
            var settings = Settings.LoadDefaultSettings(
                rootDir,
                configFileName: null,
                machineWideSettings: new CommandLineMachineWideSettings());
            var packagesDir = SettingsUtility.GetRepositoryPath(settings);

            // If 'repositoryPath' is not specified in NuGet.config, use {SolutionRoot}/packages as default
            if (string.IsNullOrEmpty(packagesDir))
            {
                packagesDir = Path.Combine(rootDir, "packages");
            }

            return Path.GetFullPath(packagesDir);
        }

        private static string GetMetadataValue(XElement itemElement, string metadataName, bool throwsIfNotFound = true)
        {
            var metadataElement = itemElement.Elements("metadata")
                .SingleOrDefault(x => string.Equals(metadataName, x.Attribute("name")?.Value));
            var metadataValue = metadataElement?.Attribute("evaluated")?.Value;
            if (throwsIfNotFound && string.IsNullOrEmpty(metadataValue))
            {
                throw new InvalidDataException(
                    string.Format("Cannot get value of metadata '{0}' in the following item element:{1}{2}",
                        metadataName, Environment.NewLine, itemElement.ToString()));
            }
            return metadataValue;
        }

        private static void SetPropertyValueIfEmpty(JObject obj, string name, string value)
        {
            if (obj[name] == null)
            {
                obj[name] = value;
            }
        }

        private class NuGetPackage
        {
            public string Identity { get; set; }
            public string Version { get; set; }
            public string TargetFramework { get; set; }
            public string Path { get; set; }
        }
    }
}
