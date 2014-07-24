// Copyright (c) Microsoft Open Technologies, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Versioning;
using Newtonsoft.Json.Linq;
using NuGet;

namespace Microsoft.Framework.Runtime
{
    public class Project
    {
        public const string ProjectFileName = "project.json";

        private static readonly CompilerOptions _emptyOptions = new CompilerOptions();
        private static readonly char[] _sourceSeparator = new[] { ';' };

        internal static readonly string[] _defaultSourcePatterns = new[] { @"**\*.cs" };
        internal static readonly string[] _defaultExcludePatterns = new[] { @"obj\**\*", @"bin\**\*", @"**.csproj",
            @"**.kproj", @"**.user", @"**.vspscc", @"**.vssscc", @"**.pubxml" };
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

        public IList<Library> Dependencies { get; private set; }

        public LanguageServices LanguageServices { get; private set; }

        internal IEnumerable<string> SourcePatterns { get; set; }

        internal IEnumerable<string> ExcludePatterns { get; set; }

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

                var excludeFiles = PathResolver.GetMatches(includeFiles, x => x, excludePatterns)
                    .ToArray();

                return files.Concat(includeFiles.Except(excludeFiles)).Distinct().ToArray();
            }
        }

        public IEnumerable<string> ExcludeFiles
        {
            get
            {
                string path = ProjectDirectory;

                var sourceExcludeFiles = ExcludePatterns
                    .SelectMany(pattern => PathResolver.PerformWildcardSearch(path, pattern))
                    .ToArray();

                return sourceExcludeFiles;
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
                    .Concat(ExcludePatterns).Concat(SourcePatterns)
                    .Select(pattern => PathResolver.NormalizeWildcardForExcludedFiles(path, pattern))
                    .ToArray();

                var excludeFiles = PathResolver.GetMatches(includeFiles, x => x, excludePatterns)
                    .ToArray();

                return includeFiles.Except(excludeFiles).Distinct().ToArray();
            }
        }

        public IDictionary<string, string> Commands { get; private set; }
        
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

            var json = File.ReadAllText(projectPath);

            // Assume the directory name is the project name if none was specified
            var projectName = GetDirectoryName(path);

            project = GetProject(json, projectName, projectPath);

            return true;
        }

        public static Project GetProject(string json, string projectName, string projectPath)
        {
            var project = new Project();

            var rawProject = JObject.Parse(json);

            // Metadata properties
            var version = rawProject["version"];
            var authors = rawProject["authors"];

            project.Name = projectName;
            project.Version = version == null ? new SemanticVersion("1.0.0") : new SemanticVersion(version.Value<string>());
            project.Description = GetValue<string>(rawProject, "description");
            project.Authors = authors == null ? new string[] { } : authors.ToObject<string[]>();
            project.Dependencies = new List<Library>();
            project.ProjectFilePath = projectPath;

            // TODO: Move this to the dependencies node
            project.EmbedInteropTypes = GetValue<bool>(rawProject, "embedInteropTypes");

            // Source file patterns
            project.SourcePatterns = GetSourcePattern(rawProject, "code", _defaultSourcePatterns);
            project.ExcludePatterns = GetSourcePattern(rawProject, "exclude", _defaultExcludePatterns);
            project.PreprocessPatterns = GetSourcePattern(rawProject, "preprocess", _defaultPreprocessPatterns);
            project.SharedPatterns = GetSourcePattern(rawProject, "shared", _defaultSharedPatterns);
            project.ResourcesPatterns = GetSourcePattern(rawProject, "resources", _defaultResourcesPatterns);
            project.ContentsPatterns = GetSourcePattern(rawProject, "files", _defaultContentsPatterns);

            // Set the default loader information for projects
            var languageServicesAssembly = "Microsoft.Framework.Runtime.Roslyn";
            var libraryExportProviderTypeName = "Microsoft.Framework.Runtime.Roslyn.RoslynLibraryExportProvider";
            var languageName = "C#";

            var languageInfo = rawProject["language"] as JObject;

            if (languageInfo != null)
            {
                languageName = GetValue<string>(languageInfo, "name");
                languageServicesAssembly = GetValue<string>(languageInfo, "assembly");
                libraryExportProviderTypeName = GetValue<string>(languageInfo, "libraryExportProviderType");
            }

            var libraryExporter = new TypeInformation(languageServicesAssembly, libraryExportProviderTypeName);

            project.LanguageServices = new LanguageServices(languageName, libraryExporter);

            var commands = rawProject["commands"] as JObject;
            if (commands != null)
            {
                foreach (var command in commands)
                {
                    project.Commands[command.Key] = command.Value.ToObject<string>();
                }
            }

            if (project.Version.IsSnapshot)
            {
                var buildVersion = Environment.GetEnvironmentVariable("K_BUILD_VERSION") ?? "SNAPSHOT";
                project.Version = project.Version.SpecifySnapshot(buildVersion);
            }

            project.BuildTargetFrameworksAndConfigurations(rawProject);

            PopulateDependencies(project.Dependencies, rawProject);

            return project;
        }

        private static IEnumerable<string> GetSourcePattern(JObject rawProject, string propertyName, string[] defaultPatterns)
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

        private static IEnumerable<string> GetSourcesSplit(string sourceDescription)
        {
            if (string.IsNullOrEmpty(sourceDescription))
            {
                return Enumerable.Empty<string>();
            }

            return sourceDescription.Split(_sourceSeparator, StringSplitOptions.RemoveEmptyEntries);
        }

        private static void PopulateDependencies(IList<Library> results, JObject settings)
        {
            var dependencies = settings["dependencies"] as JObject;
            if (dependencies != null)
            {
                foreach (var dependency in dependencies)
                {
                    if (String.IsNullOrEmpty(dependency.Key))
                    {
                        throw new InvalidDataException("Unable to resolve dependency ''.");
                    }

                    // Support 
                    // "dependencies" : {
                    //    "Name" : "1.0"
                    // }

                    string dependencyVersionValue = dependency.Value.Value<string>();

                    SemanticVersion dependencyVersion = null;

                    if (!String.IsNullOrEmpty(dependencyVersionValue))
                    {
                        dependencyVersion = SemanticVersion.Parse(dependencyVersionValue);
                    }

                    results.Add(new Library
                    {
                        Name = dependency.Key,
                        Version = dependencyVersion
                    });
                }
            }
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
                Dependencies = new List<Library>()
            };

            // Add default configurations
            _configurations["debug"] = new CompilerOptions
            {
                DebugSymbols = "full",
                Defines = new[] { "DEBUG", "TRACE" },
                Optimize = false
            };

            _configurations["release"] = new CompilerOptions
            {
                DebugSymbols = "pdbOnly",
                Defines = new[] { "RELEASE", "TRACE" },
                Optimize = true
            };

            // The configuration node has things like debug/release compiler settings
            /*
                {
                    "configurations": {
                        "debug": {
                        },
                        "release": {
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

                    // This code is for backwards compatibility with the old project format until
                    // we make all the necessary changes to understand the new format
                    if (!BuildTargetFrameworkNode(configuration, compilerOptions))
                    {
                        // Only use this as a configuration if it's not a target framework
                        _configurations[configuration.Key] = compilerOptions;
                    }
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
                    BuildTargetFrameworkNode(framework, compilerOptions: null);
                }
            }
        }

        private bool BuildTargetFrameworkNode(KeyValuePair<string, JToken> configuration, CompilerOptions compilerOptions)
        {
            // If no compilation options are provided then figure them out from the
            // node
            compilerOptions = compilerOptions ??
                              GetCompilationOptions(configuration.Value) ??
                              new CompilerOptions();

            var frameworkName = ParseFrameworkName(configuration.Key);

            // If it's not unsupported then keep it
            if (frameworkName == VersionUtility.UnsupportedFrameworkName)
            {
                // REVIEW: Should we skip unsupported target frameworks
                return false;
            }

            // Add the target framework specific define
            var defines = new HashSet<string>(compilerOptions.Defines ?? Enumerable.Empty<string>());
            defines.Add(MakeDefaultTargetFrameworkDefine(frameworkName));
            compilerOptions.Defines = defines;

            var targetFrameworkInformation = new TargetFrameworkInformation
            {
                FrameworkName = frameworkName,
                Dependencies = new List<Library>()
            };

            var properties = configuration.Value.Value<JObject>();

            PopulateDependencies(targetFrameworkInformation.Dependencies, properties);

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

        private static string MakeDefaultTargetFrameworkDefine(FrameworkName targetFramework)
        {
            var shortName = VersionUtility.GetShortFrameworkName(targetFramework);

            if (VersionUtility.IsPortableFramework(targetFramework))
            {
                // #if NET45_WIN8
                // Nobody can figure this out anyways
                return shortName.Substring("portable-".Length)
                                .Replace('+', '_')
                                .ToUpperInvariant();
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
                DebugSymbols = GetValue<string>(rawOptions, "debugSymbols"),
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
