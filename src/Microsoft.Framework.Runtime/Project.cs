// Copyright (c) Microsoft Open Technologies, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Versioning;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NuGet;

namespace Microsoft.Framework.Runtime
{
    public class Project
    {
        public const string ProjectFileName = "project.json";

        internal static string DefaultLanguageServicesAssembly = "Microsoft.Framework.Runtime.Roslyn";
        internal static string DefaultProjectReferenceProviderType = "Microsoft.Framework.Runtime.Roslyn.RoslynProjectReferenceProvider";

        private static readonly CompilerOptions _emptyOptions = new CompilerOptions();
        private static readonly char[] _sourceSeparator = new[] { ';' };

        internal static readonly string[] _defaultSourcePatterns = new[] { @"**\*.cs" };
        internal static readonly string[] _defaultExcludePatterns = new[] { @"obj", @"bin" };
        internal static readonly string[] _defaultPackExcludePatterns = new[] { @"obj", @"bin", @"**\.*\**" };
        internal static readonly string[] _defaultPreprocessPatterns = new[] { @"compiler\preprocess\**\*.cs" };
        internal static readonly string[] _defaultSharedPatterns = new[] { @"compiler\shared\**\*.cs" };
        internal static readonly string[] _defaultResourcesPatterns = new[] { @"compiler\resources\**\*" };
        internal static readonly string[] _defaultContentsPatterns = new[] { @"**\*" };

        private readonly Dictionary<FrameworkName, TargetFrameworkInformation> _targetFrameworks = new Dictionary<FrameworkName, TargetFrameworkInformation>();
        private readonly Dictionary<FrameworkName, CompilerOptions> _compilationOptions = new Dictionary<FrameworkName, CompilerOptions>();
        private readonly Dictionary<string, CompilerOptions> _configurations = new Dictionary<string, CompilerOptions>(StringComparer.OrdinalIgnoreCase);

        private CompilerOptions _defaultCompilerOptions;

        private TargetFrameworkInformation _defaultTargetFrameworkConfiguration;

        public Project()
        {
            Commands = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            Scripts = new Dictionary<string, IEnumerable<string>>(StringComparer.OrdinalIgnoreCase);
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

        public string Description { get; private set; }

        public string[] Authors { get; private set; }

        public bool EmbedInteropTypes { get; set; }

        public SemanticVersion Version { get; private set; }

        public IList<LibraryDependency> Dependencies { get; private set; }

        public LanguageServices LanguageServices { get; private set; }

        public string WebRoot { get; private set; }

        public string EntryPoint { get; private set; }

        public string ProjectUrl { get; private set; }

        public bool RequireLicenseAcceptance { get; private set; }

        public bool IsLoadable { get; set; }

        public string[] Tags { get; private set; }

        internal IEnumerable<string> SourcePatterns { get; set; }

        internal IEnumerable<string> ExcludePatterns { get; set; }

        internal IEnumerable<string> PackExcludePatterns { get; set; }

        internal IEnumerable<string> PreprocessPatterns { get; set; }

        internal IEnumerable<string> SharedPatterns { get; set; }

        internal IEnumerable<string> ResourcesPatterns { get; set; }

        internal IEnumerable<string> ContentsPatterns { get; set; }

        public IEnumerable<string> SourceFiles
        {
            get
            {
                string path = ProjectDirectory;

                var files = Enumerable.Empty<string>();

                var includeFiles = SourcePatterns
                    .SelectMany(pattern => PathResolver.PerformWildcardSearch(path, pattern))
                    .ToArray();

                var excludePatterns = PreprocessPatterns.Concat(SharedPatterns).Concat(ResourcesPatterns).Concat(ExcludePatterns)
                    .Select(pattern => PathResolver.NormalizeWildcardForExcludedFiles(path, pattern))
                    .ToArray();

                var excludeFiles = PathResolver.GetMatches(
                    includeFiles,
                    x => x,
                    excludePatterns.Select(x => Path.Combine(path, x)))
                    .ToArray();

                return files.Concat(includeFiles.Except(excludeFiles)).Distinct().ToArray();
            }
        }

        public IEnumerable<string> PreprocessSourceFiles
        {
            get
            {
                string path = ProjectDirectory;

                var files = Enumerable.Empty<string>();

                var includeFiles = PreprocessPatterns
                    .SelectMany(pattern => PathResolver.PerformWildcardSearch(path, pattern))
                    .ToArray();

                var excludePatterns = SharedPatterns.Concat(ResourcesPatterns).Concat(ExcludePatterns)
                    .Select(pattern => PathResolver.NormalizeWildcardForExcludedFiles(path, pattern))
                    .ToArray();

                var excludeFiles = PathResolver.GetMatches(
                    includeFiles,
                    x => x,
                    excludePatterns.Select(x => Path.Combine(path, x)))
                    .ToArray();

                return files.Concat(includeFiles.Except(excludeFiles)).Distinct().ToArray();
            }
        }

        public IEnumerable<string> PackExcludeFiles
        {
            get
            {
                string path = ProjectDirectory;

                var packExcludeFiles = PackExcludePatterns
                    .SelectMany(pattern => PathResolver.PerformWildcardSearch(path, pattern))
                    .ToArray();

                return packExcludeFiles;
            }
        }

        public IEnumerable<string> ResourceFiles
        {
            get
            {
                string path = ProjectDirectory;

                var includeFiles = ResourcesPatterns
                    .SelectMany(pattern => PathResolver.PerformWildcardSearch(path, pattern))
                    .ToArray();

                return includeFiles;
            }
        }

        public IEnumerable<string> SharedFiles
        {
            get
            {
                string path = ProjectDirectory;

                var includeFiles = SharedPatterns
                    .SelectMany(pattern => PathResolver.PerformWildcardSearch(path, pattern))
                    .ToArray();

                return includeFiles;
            }
        }

        public IEnumerable<string> ContentFiles
        {
            get
            {
                string path = ProjectDirectory;

                var includeFiles = ContentsPatterns
                    .SelectMany(pattern => PathResolver.PerformWildcardSearch(path, pattern))
                    .ToArray();

                var excludePatterns = PreprocessPatterns.Concat(SharedPatterns).Concat(ResourcesPatterns)
                    .Concat(PackExcludePatterns).Concat(SourcePatterns)
                    .Select(pattern => PathResolver.NormalizeWildcardForExcludedFiles(path, pattern))
                    .ToArray();

                var excludeFiles = PathResolver.GetMatches(
                    includeFiles,
                    x => x,
                    excludePatterns.Select(x => Path.Combine(path, x)))
                    .ToArray();

                return includeFiles.Except(excludeFiles).Distinct().ToArray();
            }
        }

        public IDictionary<string, string> Commands { get; private set; }

        public IDictionary<string, IEnumerable<string>> Scripts { get; private set; }

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

        public static bool TryGetProject(string path, out Project project)
        {
            project = null;

            string projectPath = null;

            if (String.Equals(Path.GetFileName(path), ProjectFileName, StringComparison.OrdinalIgnoreCase))
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
            var projectName = GetDirectoryName(path);
            projectPath = Path.GetFullPath(projectPath);

            try
            {
                using (var stream = File.OpenRead(projectPath))
                {
                    project = GetProject(stream, projectName, projectPath);
                }
            }
            catch (JsonReaderException ex)
            {
                throw FileFormatException.Create(ex, projectPath);
            }

            return true;
        }

        public static Project GetProject(string json, string projectName, string projectPath)
        {
            var ms = new MemoryStream(Encoding.UTF8.GetBytes(json));
            return GetProject(ms, projectName, projectPath);
        }

        public static Project GetProject(Stream stream, string projectName, string projectPath)
        {
            var project = new Project();

            var reader = new JsonTextReader(new StreamReader(stream));
            var rawProject = JObject.Load(reader);

            // Metadata properties
            var version = rawProject["version"];
            var authors = rawProject["authors"];
            var tags = rawProject["tags"];
            var buildVersion = Environment.GetEnvironmentVariable("K_BUILD_VERSION");

            project.Name = projectName;
            project.ProjectFilePath = Path.GetFullPath(projectPath);

            if (version == null)
            {
                project.Version = new SemanticVersion("1.0.0");
            }
            else
            {
                try
                {
                    project.Version = SpecifySnapshot(version.Value<string>(), buildVersion);
                }
                catch (Exception ex)
                {
                    var lineInfo = (IJsonLineInfo)version;

                    throw FileFormatException.Create(ex, version, project.ProjectFilePath);
                }
            }

            project.Description = GetValue<string>(rawProject, "description");
            project.Authors = authors == null ? new string[] { } : authors.ToObject<string[]>();
            project.Dependencies = new List<LibraryDependency>();
            project.WebRoot = GetValue<string>(rawProject, "webroot");
            project.EntryPoint = GetValue<string>(rawProject, "entryPoint");
            project.ProjectUrl = GetValue<string>(rawProject, "projectUrl");
            project.RequireLicenseAcceptance = GetValue<bool?>(rawProject, "requireLicenseAcceptance") ?? false;
            project.Tags = tags == null ? new string[] { } : tags.ToObject<string[]>();
            project.IsLoadable = GetValue<bool?>(rawProject, "loadable") ?? true;

            // TODO: Move this to the dependencies node
            project.EmbedInteropTypes = GetValue<bool>(rawProject, "embedInteropTypes");

            // Source file patterns
            project.SourcePatterns = GetSourcePattern(project, rawProject, "code", _defaultSourcePatterns);
            project.ExcludePatterns = GetSourcePattern(project, rawProject, "exclude", _defaultExcludePatterns);
            project.PackExcludePatterns = GetSourcePattern(project, rawProject, "bundleExclude", _defaultPackExcludePatterns);
            project.PreprocessPatterns = GetSourcePattern(project, rawProject, "preprocess", _defaultPreprocessPatterns);
            project.SharedPatterns = GetSourcePattern(project, rawProject, "shared", _defaultSharedPatterns);
            project.ResourcesPatterns = GetSourcePattern(project, rawProject, "resources", _defaultResourcesPatterns);
            project.ContentsPatterns = GetSourcePattern(project, rawProject, "files", _defaultContentsPatterns);

            // Set the default loader information for projects
            var languageServicesAssembly = DefaultLanguageServicesAssembly;
            var projectReferenceProviderType = DefaultProjectReferenceProviderType;
            var languageName = "C#";

            var languageInfo = rawProject["language"] as JObject;

            if (languageInfo != null)
            {
                languageName = GetValue<string>(languageInfo, "name");
                languageServicesAssembly = GetValue<string>(languageInfo, "assembly");
                projectReferenceProviderType = GetValue<string>(languageInfo, "projectReferenceProviderType");
            }

            var libraryExporter = new TypeInformation(languageServicesAssembly, projectReferenceProviderType);

            project.LanguageServices = new LanguageServices(languageName, libraryExporter);

            var commands = rawProject["commands"] as JObject;
            if (commands != null)
            {
                foreach (var command in commands)
                {
                    project.Commands[command.Key] = command.Value.ToObject<string>();
                }
            }

            var scripts = rawProject["scripts"] as JObject;
            if (scripts != null)
            {
                foreach (var script in scripts)
                {
                    var value = script.Value;
                    if (value.Type == JTokenType.String)
                    {
                        project.Scripts[script.Key] = new string[] { value.ToObject<string>() };
                    }
                    else if (value.Type == JTokenType.Array)
                    {
                        project.Scripts[script.Key] = script.Value.ToObject<string[]>();
                    }
                    else
                    {
                        throw FileFormatException.Create(
                            string.Format("The value of a script in {0} can only be a string or an array of strings", ProjectFileName),
                            value,
                            project.ProjectFilePath);
                    }
                }
            }

            project.BuildTargetFrameworksAndConfigurations(rawProject);

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

        private static IEnumerable<string> GetSourcePattern(Project project, JObject rawProject, string propertyName,
            string[] defaultPatterns)
        {
            return GetSourcePatternCore(rawProject, propertyName, defaultPatterns)
                .Select(p => FolderToPattern(p, project.ProjectDirectory));
        }

        private static IEnumerable<string> GetSourcePatternCore(JObject rawProject, string propertyName, string[] defaultPatterns)
        {
            var token = rawProject[propertyName];

            if (token == null)
            {
                return defaultPatterns;
            }

            if (token.Type == JTokenType.Null)
            {
                return Enumerable.Empty<string>();
            }

            if (token.Type == JTokenType.String)
            {
                return GetSourcesSplit(token.Value<string>());
            }

            // Assume it's an array (it should explode if it's not)
            return token.ToObject<string[]>().SelectMany(GetSourcesSplit);
        }

        private static string FolderToPattern(string candidate, string projectDir)
        {
            // If it's already a pattern, no change is needed
            if (candidate.Contains('*'))
            {
                return candidate;
            }

            // If the given string ends with a path separator, or it is an existing directory
            // we convert this folder name to a pattern matching all files in the folder
            if (candidate.EndsWith(@"\") ||
                candidate.EndsWith("/") ||
                Directory.Exists(Path.Combine(projectDir, candidate)))
            {
                return Path.Combine(candidate, "**", "*");
            }

            // Otherwise, it represents a single file
            return candidate;
        }

        private static IEnumerable<string> GetSourcesSplit(string sourceDescription)
        {
            if (string.IsNullOrEmpty(sourceDescription))
            {
                return Enumerable.Empty<string>();
            }

            return sourceDescription.Split(_sourceSeparator, StringSplitOptions.RemoveEmptyEntries);
        }

        private static void PopulateDependencies(
            string projectPath,
            IList<LibraryDependency> results,
            JObject settings,
            string propertyName,
            bool isGacOrFrameworkReference)
        {
            var dependencies = settings[propertyName] as JObject;
            if (dependencies != null)
            {
                foreach (var dependency in dependencies)
                {
                    if (string.IsNullOrEmpty(dependency.Key))
                    {

                        throw FileFormatException.Create(
                            "Unable to resolve dependency ''.",
                            dependency.Value,
                            projectPath);
                    }

                    // Support 
                    // "dependencies" : {
                    //    "Name" : "1.0"
                    // }

                    var dependencyValue = dependency.Value;
                    var dependencyTypeValue = LibraryDependencyType.Default;

                    string dependencyVersionValue = null;
                    JToken dependencyVersionToken = dependencyValue;

                    if (dependencyValue.Type == JTokenType.String)
                    {
                        dependencyVersionValue = dependencyValue.Value<string>();
                    }
                    else
                    {
                        if (dependencyValue.Type == JTokenType.Object)
                        {
                            dependencyVersionToken = dependencyValue["version"];
                            if (dependencyVersionToken != null && dependencyVersionToken.Type == JTokenType.String)
                            {
                                dependencyVersionValue = dependencyVersionToken.Value<string>();
                            }
                        }

                        IEnumerable<string> strings;
                        if (TryGetStringEnumerable(dependencyValue["type"], out strings))
                        {
                            dependencyTypeValue = LibraryDependencyType.Parse(strings);
                        }
                    }

                    SemanticVersionRange dependencyVersionRange = null;

                    if (!string.IsNullOrEmpty(dependencyVersionValue))
                    {
                        try
                        {
                            dependencyVersionRange = VersionUtility.ParseVersionRange(dependencyVersionValue);
                        }
                        catch (Exception ex)
                        {
                            throw FileFormatException.Create(
                                ex,
                                dependencyVersionToken,
                                projectPath);
                        }
                    }

                    results.Add(new LibraryDependency
                    {
                        LibraryRange = new LibraryRange
                        {
                            Name = dependency.Key,
                            VersionRange = dependencyVersionRange,
                            IsGacOrFrameworkReference = isGacOrFrameworkReference,
                        },
                        Type = dependencyTypeValue
                    });
                }
            }
        }

        private static bool TryGetStringEnumerable(JToken token, out IEnumerable<string> result)
        {
            IEnumerable<string> values;
            if (token == null)
            {
                result = null;
                return false;
            }
            else if (token.Type == JTokenType.String)
            {
                values = new[]
                {
                    token.Value<string>()
                };
            }
            else
            {
                values = token.Value<string[]>();
            }
            result = values
                .SelectMany(value => value.Split(new[] { ' ', ',' }, StringSplitOptions.RemoveEmptyEntries));
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

        private void BuildTargetFrameworksAndConfigurations(JObject rawProject)
        {
            // Get the shared compilationOptions
            _defaultCompilerOptions = GetCompilationOptions(rawProject) ?? _emptyOptions;

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
            var configurations = rawProject["configurations"] as JObject;
            if (configurations != null)
            {
                foreach (var configuration in configurations)
                {
                    var compilerOptions = GetCompilationOptions(configuration.Value);

                    // Only use this as a configuration if it's not a target framework
                    _configurations[configuration.Key] = compilerOptions;
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

            var frameworks = rawProject["frameworks"] as JObject;
            if (frameworks != null)
            {
                foreach (var framework in frameworks)
                {
                    try
                    {
                        BuildTargetFrameworkNode(framework);
                    }
                    catch (Exception ex)
                    {
                        throw FileFormatException.Create(ex, framework.Value, ProjectFilePath);
                    }
                }
            }
        }

        private bool BuildTargetFrameworkNode(KeyValuePair<string, JToken> targetFramework)
        {
            // If no compilation options are provided then figure them out from the node
            var compilerOptions = GetCompilationOptions(targetFramework.Value) ??
                                  new CompilerOptions();

            var frameworkName = ParseFrameworkName(targetFramework.Key);

            // If it's not unsupported then keep it
            if (frameworkName == VersionUtility.UnsupportedFrameworkName)
            {
                // REVIEW: Should we skip unsupported target frameworks
                return false;
            }

            // Add the target framework specific define
            var defines = new HashSet<string>(compilerOptions.Defines ?? Enumerable.Empty<string>());
            var frameworkDefinition = Tuple.Create(targetFramework.Key, frameworkName);
            var frameworkDefine = MakeDefaultTargetFrameworkDefine(frameworkDefinition);

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

            var properties = targetFramework.Value.Value<JObject>();

            PopulateDependencies(
                ProjectFilePath,
                targetFrameworkInformation.Dependencies,
                properties,
                "dependencies",
                isGacOrFrameworkReference: false);

            var frameworkAssemblies = new List<LibraryDependency>();
            PopulateDependencies(
                ProjectFilePath,
                frameworkAssemblies,
                properties,
                "frameworkAssemblies",
                isGacOrFrameworkReference: true);

            targetFrameworkInformation.Dependencies.AddRange(frameworkAssemblies);

            targetFrameworkInformation.WrappedProject = GetValue<string>(properties, "wrappedProject");

            var binNode = properties["bin"];

            if (binNode != null)
            {
                targetFrameworkInformation.AssemblyPath = GetValue<string>(binNode, "assembly");
                targetFrameworkInformation.PdbPath = GetValue<string>(binNode, "pdb");
            }

            _compilationOptions[frameworkName] = compilerOptions;
            _targetFrameworks[frameworkName] = targetFrameworkInformation;

            return true;
        }

        public static FrameworkName ParseFrameworkName(string targetFramework)
        {
            if (targetFramework.Contains("+"))
            {
                var portableProfile = NetPortableProfile.Parse(targetFramework);

                if (portableProfile != null &&
                    portableProfile.FrameworkName.Profile != targetFramework)
                {
                    return portableProfile.FrameworkName;
                }

                return VersionUtility.UnsupportedFrameworkName;
            }

            if (targetFramework.IndexOf(',') != -1)
            {
                // Assume it's a framework name if it contains commas
                // e.g. .NETPortable,Version=v4.5,Profile=Profile78
                return new FrameworkName(targetFramework);
            }

            return VersionUtility.ParseFrameworkName(targetFramework);
        }


        public TargetFrameworkInformation GetTargetFramework(FrameworkName targetFramework)
        {
            TargetFrameworkInformation targetFrameworkInfo;
            if (_targetFrameworks.TryGetValue(targetFramework, out targetFrameworkInfo))
            {
                return targetFrameworkInfo;
            }

            IEnumerable<TargetFrameworkInformation> compatibleConfigurations;
            if (VersionUtility.TryGetCompatibleItems(targetFramework, GetTargetFrameworks(), out compatibleConfigurations) &&
                compatibleConfigurations.Any())
            {
                targetFrameworkInfo = compatibleConfigurations.FirstOrDefault();
            }

            return targetFrameworkInfo ?? _defaultTargetFrameworkConfiguration;
        }

        private static string MakeDefaultTargetFrameworkDefine(Tuple<string, FrameworkName> frameworkDefinition)
        {
            var shortName = frameworkDefinition.Item1;
            var targetFramework = frameworkDefinition.Item2;

            if (VersionUtility.IsPortableFramework(targetFramework))
            {
                return null;
            }

            return shortName.ToUpperInvariant();
        }

        private CompilerOptions GetCompilationOptions(JToken topLevelOrConfiguration)
        {
            var rawOptions = topLevelOrConfiguration["compilationOptions"];

            if (rawOptions == null)
            {
                return null;
            }

            var options = new CompilerOptions
            {
                Defines = ConvertValue<string[]>(rawOptions, "define"),
                LanguageVersion = ConvertValue<string>(rawOptions, "languageVersion"),
                AllowUnsafe = GetValue<bool?>(rawOptions, "allowUnsafe"),
                Platform = GetValue<string>(rawOptions, "platform"),
                WarningsAsErrors = GetValue<bool?>(rawOptions, "warningsAsErrors"),
                Optimize = GetValue<bool?>(rawOptions, "optimize"),
            };

            return options;
        }

        private static T GetValue<T>(JToken token, string name)
        {
            if (token == null)
            {
                return default(T);
            }

            var obj = token[name];

            if (obj == null)
            {
                return default(T);
            }

            return obj.Value<T>();
        }

        private static T ConvertValue<T>(JToken token, string name)
        {
            if (token == null)
            {
                return default(T);
            }

            var obj = token[name];

            if (obj == null)
            {
                return default(T);
            }

            return obj.ToObject<T>();
        }

        private static string GetDirectoryName(string path)
        {
            path = path.TrimEnd(Path.DirectorySeparatorChar);
            return path.Substring(Path.GetDirectoryName(path).Length).Trim(Path.DirectorySeparatorChar);
        }
    }
}
