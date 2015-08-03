// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.Versioning;
using System.Text;
using Microsoft.Dnx.Runtime.Helpers;
using Microsoft.Dnx.Runtime.Json;
using NuGet;

namespace Microsoft.Dnx.Runtime
{
    public class Project
    {
        public const string ProjectFileName = "project.json";

        internal static readonly TypeInformation DefaultRuntimeCompiler = new TypeInformation("Microsoft.Dnx.Compilation.CSharp", "Microsoft.Dnx.Compilation.CSharp.RoslynProjectCompiler");
        internal static readonly TypeInformation DefaultDesignTimeCompiler = new TypeInformation("Microsoft.Dnx.Compilation.DesignTime", "Microsoft.Dnx.Compilation.DesignTime.DesignTimeHostProjectCompiler");

        public static TypeInformation DefaultCompiler = DefaultRuntimeCompiler;

        private readonly Dictionary<FrameworkName, TargetFrameworkInformation> _targetFrameworks = new Dictionary<FrameworkName, TargetFrameworkInformation>();
        private readonly Dictionary<FrameworkName, CompilerOptions> _compilationOptions = new Dictionary<FrameworkName, CompilerOptions>();
        private readonly Dictionary<string, CompilerOptions> _configurations = new Dictionary<string, CompilerOptions>(StringComparer.OrdinalIgnoreCase);

        private CompilerOptions _defaultCompilerOptions;

        private TargetFrameworkInformation _defaultTargetFrameworkConfiguration;

        public Project()
        {
        }

        public string ProjectFilePath { get; private set; }

        public string ProjectDirectory
        {
            get
            {
                return Path.GetDirectoryName(ProjectFilePath);
            }
        }

        public string Name { get; private set; }

        public string Title { get; set; }

        public string Description { get; private set; }

        public string Copyright { get; set; }

        public string Summary { get; set; }

        public string Language { get; set; }

        public string ReleaseNotes { get; set; }

        public string[] Authors { get; private set; }

        public string[] Owners { get; private set; }

        public IDictionary<string, string> Repository { get; private set; }

        public bool EmbedInteropTypes { get; set; }

        public SemanticVersion Version { get; private set; }

        public Version AssemblyFileVersion { get; private set; }

        public IList<LibraryDependency> Dependencies { get; private set; }

        public CompilerServices CompilerServices { get; private set; }

        public string WebRoot { get; private set; }

        public string EntryPoint { get; private set; }

        public string ProjectUrl { get; private set; }

        public string LicenseUrl { get; set; }

        public string IconUrl { get; set; }

        public bool RequireLicenseAcceptance { get; private set; }

        public string[] Tags { get; private set; }

        public bool IsLoadable { get; set; }

        public ProjectFilesCollection Files { get; private set; }

        public IDictionary<string, string> Commands { get; } = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        public IDictionary<string, IEnumerable<string>> Scripts { get; } = new Dictionary<string, IEnumerable<string>>(StringComparer.OrdinalIgnoreCase);

        public IEnumerable<TargetFrameworkInformation> GetTargetFrameworks()
        {
            return _targetFrameworks.Values;
        }

        public IEnumerable<string> GetConfigurations()
        {
            return _configurations.Keys;
        }

        public static bool HasProjectFile(string path)
        {
            string projectPath = Path.Combine(path, ProjectFileName);

            return File.Exists(projectPath);
        }

        public static bool TryGetProject(string path, out Project project, ICollection<DiagnosticMessage> diagnostics = null)
        {
            project = null;

            string projectPath = null;

            if (string.Equals(Path.GetFileName(path), ProjectFileName, StringComparison.OrdinalIgnoreCase))
            {
                projectPath = path;
                path = Path.GetDirectoryName(path);
            }
            else if (!HasProjectFile(path))
            {
                return false;
            }
            else
            {
                projectPath = Path.Combine(path, ProjectFileName);
            }

            // Assume the directory name is the project name if none was specified
            var projectName = PathUtility.GetDirectoryName(path);
            projectPath = Path.GetFullPath(projectPath);

            try
            {
                using (var stream = File.OpenRead(projectPath))
                {
                    project = GetProjectFromStream(stream, projectName, projectPath, diagnostics);
                }
            }
            catch (Exception ex)
            {
                throw FileFormatException.Create(ex, projectPath);
            }

            return true;
        }

        internal static Project GetProjectFromStream(Stream stream, string projectName, string projectPath, ICollection<DiagnosticMessage> diagnostics = null)
        {
            var project = new Project();

            var reader = new StreamReader(stream);
            var rawProject = JsonDeserializer.Deserialize(reader) as JsonObject;
            if (rawProject == null)
            {
                throw FileFormatException.Create(
                    "The JSON file can't be deserialized to a JSON object.",
                    projectPath);
            }

            // Meta-data properties
            project.Name = projectName;
            project.ProjectFilePath = Path.GetFullPath(projectPath);

            var version = rawProject.Value("version") as JsonString;
            if (version == null)
            {
                project.Version = new SemanticVersion("1.0.0");
            }
            else
            {
                try
                {
                    var buildVersion = Environment.GetEnvironmentVariable("DNX_BUILD_VERSION");
                    project.Version = SpecifySnapshot(version, buildVersion);
                }
                catch (Exception ex)
                {
                    throw FileFormatException.Create(ex, version, project.ProjectFilePath);
                }
            }

            var fileVersion = Environment.GetEnvironmentVariable("DNX_ASSEMBLY_FILE_VERSION");
            if (string.IsNullOrWhiteSpace(fileVersion))
            {
                project.AssemblyFileVersion = project.Version.Version;
            }
            else
            {
                try
                {
                    var simpleVersion = project.Version.Version;
                    project.AssemblyFileVersion = new Version(simpleVersion.Major,
                        simpleVersion.Minor,
                        simpleVersion.Build,
                        int.Parse(fileVersion));
                }
                catch (FormatException ex)
                {
                    throw new FormatException("The assembly file version is invalid: " + fileVersion, ex);
                }
            }

            project.Description = rawProject.ValueAsString("description");
            project.Summary = rawProject.ValueAsString("summary");
            project.Copyright = rawProject.ValueAsString("copyright");
            project.Title = rawProject.ValueAsString("title");
            project.WebRoot = rawProject.ValueAsString("webroot");
            project.EntryPoint = rawProject.ValueAsString("entryPoint");
            project.ProjectUrl = rawProject.ValueAsString("projectUrl");
            project.LicenseUrl = rawProject.ValueAsString("licenseUrl");
            project.IconUrl = rawProject.ValueAsString("iconUrl");

            project.Authors = rawProject.ValueAsStringArray("authors") ?? new string[] { };
            project.Owners = rawProject.ValueAsStringArray("owners") ?? new string[] { };
            project.Tags = rawProject.ValueAsStringArray("tags") ?? new string[] { };

            project.Language = rawProject.ValueAsString("language");
            project.ReleaseNotes = rawProject.ValueAsString("releaseNotes");

            project.RequireLicenseAcceptance = rawProject.ValueAsBoolean("requireLicenseAcceptance", defaultValue: false);
            project.IsLoadable = rawProject.ValueAsBoolean("loadable", defaultValue: true);
            // TODO: Move this to the dependencies node
            project.EmbedInteropTypes = rawProject.ValueAsBoolean("embedInteropTypes", defaultValue: false);

            project.Dependencies = new List<LibraryDependency>();

            // Project files
            project.Files = new ProjectFilesCollection(rawProject,
                                                       project.ProjectDirectory,
                                                       project.ProjectFilePath,
                                                       diagnostics);

            var compilerInfo = rawProject.ValueAsJsonObject("compiler");
            if (compilerInfo != null)
            {
                var languageName = compilerInfo.ValueAsString("name") ?? "C#";
                var compilerAssembly = compilerInfo.ValueAsString("compilerAssembly");
                var compilerType = compilerInfo.ValueAsString("compilerType");

                var compiler = new TypeInformation(compilerAssembly, compilerType);
                project.CompilerServices = new CompilerServices(languageName, compiler);
            }

            var commands = rawProject.Value("commands") as JsonObject;
            if (commands != null)
            {
                foreach (var key in commands.Keys)
                {
                    var value = commands.ValueAsString(key);
                    if (value != null)
                    {
                        project.Commands[key] = value;
                    }
                }
            }

            var scripts = rawProject.Value("scripts") as JsonObject;
            if (scripts != null)
            {
                foreach (var key in scripts.Keys)
                {
                    var stringValue = scripts.ValueAsString(key);
                    if (stringValue != null)
                    {
                        project.Scripts[key] = new string[] { stringValue };
                        continue;
                    }

                    var arrayValue = scripts.ValueAsStringArray(key);
                    if (arrayValue != null)
                    {
                        project.Scripts[key] = arrayValue;
                        continue;
                    }

                    throw FileFormatException.Create(
                        string.Format("The value of a script in {0} can only be a string or an array of strings", ProjectFileName),
                        scripts.Value(key),
                        project.ProjectFilePath);
                }
            }

            var repository = rawProject.Value("repository") as JsonObject;
            if (repository != null)
            {
                project.Repository = repository
                    .Keys
                    .ToDictionary(
                        key => key,
                        key => repository.ValueAsString(key).Value);
            }

            project.BuildTargetFrameworksAndConfigurations(rawProject, diagnostics);

            PopulateDependencies(
                project.ProjectFilePath,
                project.Dependencies,
                rawProject,
                "dependencies",
                isGacOrFrameworkReference: false);

            return project;
        }

        private static SemanticVersion SpecifySnapshot(string version, string snapshotValue)
        {
            if (version.EndsWith("-*"))
            {
                if (string.IsNullOrEmpty(snapshotValue))
                {
                    version = version.Substring(0, version.Length - 2);
                }
                else
                {
                    version = version.Substring(0, version.Length - 1) + snapshotValue;
                }
            }

            return new SemanticVersion(version);
        }

        private static void PopulateDependencies(
            string projectPath,
            IList<LibraryDependency> results,
            JsonObject settings,
            string propertyName,
            bool isGacOrFrameworkReference)
        {
            var dependencies = settings.ValueAsJsonObject(propertyName);
            if (dependencies != null)
            {
                foreach (var dependencyKey in dependencies.Keys)
                {
                    if (string.IsNullOrEmpty(dependencyKey))
                    {
                        throw FileFormatException.Create(
                            "Unable to resolve dependency ''.",
                            dependencies.Value(dependencyKey),
                            projectPath);
                    }

                    var dependencyValue = dependencies.Value(dependencyKey);
                    var dependencyTypeValue = LibraryDependencyType.Default;
                    JsonString dependencyVersionAsString = null;

                    if (dependencyValue is JsonObject)
                    {
                        // "dependencies" : { "Name" : { "version": "1.0", "type": "build" } }
                        var dependencyValueAsObject = (JsonObject)dependencyValue;
                        dependencyVersionAsString = dependencyValueAsObject.ValueAsString("version");

                        IEnumerable<string> strings;
                        if (TryGetStringEnumerable(dependencyValueAsObject, "type", out strings))
                        {
                            dependencyTypeValue = LibraryDependencyType.Parse(strings);
                        }
                    }
                    else if (dependencyValue is JsonString)
                    {
                        // "dependencies" : { "Name" : "1.0" }
                        dependencyVersionAsString = (JsonString)dependencyValue;
                    }
                    else
                    {
                        throw FileFormatException.Create(
                            string.Format("Invalid dependency version: {0}. The format is not recognizable.", dependencyKey),
                            dependencyValue,
                            projectPath);
                    }

                    SemanticVersionRange dependencyVersionRange = null;
                    if (!string.IsNullOrEmpty(dependencyVersionAsString?.Value))
                    {
                        try
                        {
                            dependencyVersionRange = VersionUtility.ParseVersionRange(dependencyVersionAsString.Value);
                        }
                        catch (Exception ex)
                        {
                            throw FileFormatException.Create(
                                ex,
                                dependencyValue,
                                projectPath);
                        }
                    }

                    results.Add(new LibraryDependency
                    {
                        LibraryRange = new LibraryRange(dependencyKey, isGacOrFrameworkReference)
                        {
                            VersionRange = dependencyVersionRange,
                            FileName = projectPath,
                            Line = dependencyValue.Line,
                            Column = dependencyValue.Column
                        },
                        Type = dependencyTypeValue
                    });
                }
            }
        }

        private static bool TryGetStringEnumerable(JsonObject parent, string property, out IEnumerable<string> result)
        {
            var collection = new List<string>();
            var valueInString = parent.ValueAsString(property);
            if (valueInString != null)
            {
                collection.Add(valueInString);
            }
            else
            {
                var valueInArray = parent.ValueAsStringArray(property);
                if (valueInArray != null)
                {
                    collection.AddRange(valueInArray);
                }
                else
                {
                    result = null;
                    return false;
                }
            }

            result = collection.SelectMany(value => value.Split(new[] { ' ', ',' }, StringSplitOptions.RemoveEmptyEntries));
            return true;
        }

        public CompilerOptions GetCompilerOptions()
        {
            return _defaultCompilerOptions;
        }

        public CompilerOptions GetCompilerOptions(string configurationName)
        {
            CompilerOptions options;
            if (_configurations.TryGetValue(configurationName, out options))
            {
                return options;
            }

            return null;
        }

        public CompilerOptions GetCompilerOptions(FrameworkName frameworkName)
        {
            CompilerOptions options;
            if (_compilationOptions.TryGetValue(frameworkName, out options))
            {
                return options;
            }

            return null;
        }

        public ICompilerOptions GetCompilerOptions(FrameworkName targetFramework,
                                                  string configurationName)
        {
            // Get all project options and combine them
            var rootOptions = GetCompilerOptions();
            var configurationOptions = configurationName != null ? GetCompilerOptions(configurationName) : null;
            var targetFrameworkOptions = targetFramework != null ? GetCompilerOptions(targetFramework) : null;

            // Combine all of the options
            return CompilerOptions.Combine(rootOptions, configurationOptions, targetFrameworkOptions);
        }

        public TargetFrameworkInformation GetTargetFramework(FrameworkName targetFramework)
        {
            TargetFrameworkInformation targetFrameworkInfo;
            if (_targetFrameworks.TryGetValue(targetFramework, out targetFrameworkInfo))
            {
                return targetFrameworkInfo;
            }

            IEnumerable<TargetFrameworkInformation> compatibleConfigurations;
            if (VersionUtility.GetNearest(targetFramework, GetTargetFrameworks(), out compatibleConfigurations) &&
                compatibleConfigurations.Any())
            {
                targetFrameworkInfo = compatibleConfigurations.FirstOrDefault();
            }

            return targetFrameworkInfo ?? _defaultTargetFrameworkConfiguration;
        }

        private void BuildTargetFrameworksAndConfigurations(JsonObject projectJsonObject,
            ICollection<DiagnosticMessage> diagnostics)
        {
            // Get the shared compilationOptions
            _defaultCompilerOptions = GetCompilationOptions(projectJsonObject) ?? new CompilerOptions();

            _defaultTargetFrameworkConfiguration = new TargetFrameworkInformation
            {
                Dependencies = new List<LibraryDependency>()
            };

            // Add default configurations
            _configurations["Debug"] = new CompilerOptions
            {
                Defines = new[] { "DEBUG", "TRACE" },
                Optimize = false
            };

            _configurations["Release"] = new CompilerOptions
            {
                Defines = new[] { "RELEASE", "TRACE" },
                Optimize = true
            };

            // The configuration node has things like debug/release compiler settings
            /*
                {
                    "configurations": {
                        "Debug": {
                        },
                        "Release": {
                        }
                    }
                }
            */

            var configurations = projectJsonObject.ValueAsJsonObject("configurations");
            if (configurations != null)
            {
                foreach (var configKey in configurations.Keys)
                {
                    var compilerOptions = GetCompilationOptions(configurations.ValueAsJsonObject(configKey));

                    // Only use this as a configuration if it's not a target framework
                    _configurations[configKey] = compilerOptions;
                }
            }

            // The frameworks node is where target frameworks go
            /*
                {
                    "frameworks": {
                        "net45": {
                        },
                        "k10": {
                        }
                    }
                }
            */

            var frameworks = projectJsonObject.ValueAsJsonObject("frameworks");
            if (frameworks != null)
            {
                foreach (var frameworkKey in frameworks.Keys)
                {
                    try
                    {
                        var frameworkToken = frameworks.ValueAsJsonObject(frameworkKey);
                        var success = BuildTargetFrameworkNode(frameworkKey, frameworkToken);
                        if (!success)
                        {
                            diagnostics?.Add(
                                new DiagnosticMessage(
                                    $"\"{frameworkKey}\" is an unsupported framework",
                                    ProjectFilePath,
                                    DiagnosticMessageSeverity.Error,
                                    frameworkToken.Line,
                                    frameworkToken.Column));
                        }
                    }
                    catch (Exception ex)
                    {
                        throw FileFormatException.Create(ex, frameworks.Value(frameworkKey), ProjectFilePath);
                    }
                }
            }
        }

        /// <summary>
        /// Parse a Json object which represents project configuration for a specified framework
        /// </summary>
        /// <param name="frameworkKey">The name of the framework</param>
        /// <param name="frameworkValue">The Json object represent the settings</param>
        /// <returns>Returns true if it successes.</returns>
        private bool BuildTargetFrameworkNode(string frameworkKey, JsonObject frameworkValue)
        {
            // If no compilation options are provided then figure them out from the node
            var compilerOptions = GetCompilationOptions(frameworkValue) ??
                                  new CompilerOptions();

            var frameworkName = FrameworkNameHelper.ParseFrameworkName(frameworkKey);

            // If it's not unsupported then keep it
            if (frameworkName == VersionUtility.UnsupportedFrameworkName)
            {
                // REVIEW: Should we skip unsupported target frameworks
                return false;
            }

            // Add the target framework specific define
            var defines = new HashSet<string>(compilerOptions.Defines ?? Enumerable.Empty<string>());
            var frameworkDefinition = Tuple.Create(frameworkKey, frameworkName);
            var frameworkDefine = FrameworkNameHelper.MakeDefaultTargetFrameworkDefine(frameworkDefinition);

            if (!string.IsNullOrEmpty(frameworkDefine))
            {
                defines.Add(frameworkDefine);
            }

            compilerOptions.Defines = defines;

            var targetFrameworkInformation = new TargetFrameworkInformation
            {
                FrameworkName = frameworkName,
                Dependencies = new List<LibraryDependency>()
            };

            var frameworkDependencies = new List<LibraryDependency>();

            PopulateDependencies(
                ProjectFilePath,
                frameworkDependencies,
                frameworkValue,
                "dependencies",
                isGacOrFrameworkReference: false);

            var frameworkAssemblies = new List<LibraryDependency>();
            PopulateDependencies(
                ProjectFilePath,
                frameworkAssemblies,
                frameworkValue,
                "frameworkAssemblies",
                isGacOrFrameworkReference: true);

            frameworkDependencies.AddRange(frameworkAssemblies);
            targetFrameworkInformation.Dependencies = frameworkDependencies;

            targetFrameworkInformation.WrappedProject = frameworkValue.ValueAsString("wrappedProject");

            var binNode = frameworkValue.ValueAsJsonObject("bin");
            if (binNode != null)
            {
                targetFrameworkInformation.AssemblyPath = binNode.ValueAsString("assembly");
                targetFrameworkInformation.PdbPath = binNode.ValueAsString("pdb");
            }

            _compilationOptions[frameworkName] = compilerOptions;
            _targetFrameworks[frameworkName] = targetFrameworkInformation;

            return true;
        }

        private static CompilerOptions GetCompilationOptions(JsonObject rawObject)
        {
            var rawOptions = rawObject.ValueAsJsonObject("compilationOptions");
            if (rawOptions == null)
            {
                return null;
            }

            return new CompilerOptions
            {
                Defines = rawOptions.ValueAsStringArray("define"),
                LanguageVersion = rawOptions.ValueAsString("languageVersion"),
                AllowUnsafe = rawOptions.ValueAsNullableBoolean("allowUnsafe"),
                Platform = rawOptions.ValueAsString("platform"),
                WarningsAsErrors = rawOptions.ValueAsNullableBoolean("warningsAsErrors"),
                Optimize = rawOptions.ValueAsNullableBoolean("optimize"),
                KeyFile = rawOptions.ValueAsString("keyFile"),
                DelaySign = rawOptions.ValueAsNullableBoolean("delaySign"),
                StrongName = rawOptions.ValueAsNullableBoolean("strongName")
            };
        }
    }
}
