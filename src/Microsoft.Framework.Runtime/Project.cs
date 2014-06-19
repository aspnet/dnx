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
        internal static readonly string[] _defaultSourceExcludePatterns = new[] { @"obj\**\*", @"bin\**\*" };
        internal static readonly string[] _defaultPreprocessPatterns = new[] { @"Compiler\Preprocess\**\*.cs" };
        internal static readonly string[] _defaultSharedPatterns = new[] { @"Compiler\Shared\**\*.cs" };
        internal static readonly string[] _defaultResourcesPatterns = new[] { @"Compiler\Resources\**\*" };

        private readonly Dictionary<FrameworkName, TargetFrameworkConfiguration> _configurations = new Dictionary<FrameworkName, TargetFrameworkConfiguration>();
        private readonly Dictionary<FrameworkName, CompilerOptions> _compilationOptions = new Dictionary<FrameworkName, CompilerOptions>();

        private CompilerOptions _defaultCompilerOptions;

        private TargetFrameworkConfiguration _defaultTargetFrameworkConfiguration;

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

        public LoaderInformation Loader { get; private set; }

        internal IEnumerable<string> SourcePatterns { get; set; }

        internal IEnumerable<string> SourceExcludePatterns { get; set; }

        internal IEnumerable<string> PreprocessPatterns { get; set; }

        internal IEnumerable<string> SharedPatterns { get; set; }

        internal IEnumerable<string> ResourcesPatterns { get; set; }

        public IEnumerable<string> SourceFiles
        {
            get
            {
                string path = ProjectDirectory;

                var files = Enumerable.Empty<string>();

                var includeFiles = SourcePatterns
                    .SelectMany(pattern => PathResolver.PerformWildcardSearch(path, pattern))
                    .ToArray();

                var excludePatterns = PreprocessPatterns.Concat(SharedPatterns).Concat(ResourcesPatterns).Concat(SourceExcludePatterns)
                    .Select(pattern => PathResolver.NormalizeWildcardForExcludedFiles(path, pattern))
                    .ToArray();

                var excludeFiles = PathResolver.GetMatches(includeFiles, x => x, excludePatterns)
                    .ToArray();

                return files.Concat(includeFiles.Except(excludeFiles)).Distinct().ToArray();
            }
        }

        public IEnumerable<string> SourceExcludeFiles
        {
            get
            {
                string path = ProjectDirectory;

                var sourceExcludeFiles = SourceExcludePatterns
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

        public IDictionary<string, string> Commands { get; private set; }

        public IEnumerable<TargetFrameworkConfiguration> GetTargetFrameworkConfigurations()
        {
            return _configurations.Values;
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
            project.SourceExcludePatterns = GetSourcePattern(rawProject, "exclude", _defaultSourceExcludePatterns);
            project.PreprocessPatterns = GetSourcePattern(rawProject, "preprocess", _defaultPreprocessPatterns);
            project.SharedPatterns = GetSourcePattern(rawProject, "shared", _defaultSharedPatterns);
            project.ResourcesPatterns = GetSourcePattern(rawProject, "resources", _defaultResourcesPatterns);

            var loaderInformation = new LoaderInformation();

            var loaderInfo = rawProject["loader"] as JObject;

            if (loaderInfo != null)
            {
                loaderInformation.AssemblyName = GetValue<string>(loaderInfo, "name");
                loaderInformation.TypeName = GetValue<string>(loaderInfo, "type");
            }
            else
            {
                loaderInformation.AssemblyName = "Microsoft.Framework.Runtime.Roslyn";
                loaderInformation.TypeName = "Microsoft.Framework.Runtime.Roslyn.RoslynAssemblyLoader";
            }

            project.Loader = loaderInformation;

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

            project.BuildTargetFrameworkConfigurations(rawProject);

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

        public CompilerOptions GetCompilerOptions(string frameworkName)
        {
            return GetCompilerOptions(ParseFrameworkName(frameworkName));
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

        private void BuildTargetFrameworkConfigurations(JObject rawProject)
        {
            // Get the shared compilationOptions
            _defaultCompilerOptions = GetCompilationOptions(rawProject) ?? _emptyOptions;

            _defaultTargetFrameworkConfiguration = new TargetFrameworkConfiguration
            {
                Dependencies = new List<Library>()
            };

            // Parse the specific configuration section
            var configurations = rawProject["configurations"] as JObject;
            if (configurations != null)
            {
                foreach (var configuration in configurations)
                {
                    var config = new TargetFrameworkConfiguration();

                    config.FrameworkName = ParseFrameworkName(configuration.Key);
                    var properties = configuration.Value.Value<JObject>();

                    config.Dependencies = new List<Library>();

                    PopulateDependencies(config.Dependencies, properties);

                    _configurations[config.FrameworkName] = config;
                    _compilationOptions[config.FrameworkName] = GetCompilationOptions(configuration.Value);
                }
            }
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
                AllowUnsafe = GetValue<bool>(rawOptions, "allowUnsafe"),
                Platform = GetValue<string>(rawOptions, "platform"),
                WarningsAsErrors = GetValue<bool>(rawOptions, "warningsAsErrors"),
                CommandLine = GetValue<string>(rawOptions, "commandLineArgs")
            };

            return options;
        }

        public static FrameworkName ParseFrameworkName(string configurationName)
        {
            if (configurationName.Contains("+"))
            {
                var portableProfile = NetPortableProfile.Parse(configurationName);

                if (portableProfile != null &&
                    portableProfile.FrameworkName.Profile != configurationName)
                {
                    return portableProfile.FrameworkName;
                }

                return VersionUtility.UnsupportedFrameworkName;
            }

            if (configurationName.IndexOf(',') != -1)
            {
                // Assume it's a framework name if it contains commas
                // e.g. .NETPortable,Version=v4.5,Profile=Profile78
                return new FrameworkName(configurationName);
            }

            return VersionUtility.ParseFrameworkName(configurationName);

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

        public TargetFrameworkConfiguration GetTargetFrameworkConfiguration(FrameworkName targetFramework)
        {
            TargetFrameworkConfiguration config;
            if (_configurations.TryGetValue(targetFramework, out config))
            {
                return config;
            }

            IEnumerable<TargetFrameworkConfiguration> compatibleConfigurations;
            if (VersionUtility.TryGetCompatibleItems(targetFramework, GetTargetFrameworkConfigurations(), out compatibleConfigurations) &&
                compatibleConfigurations.Any())
            {
                config = compatibleConfigurations.FirstOrDefault();
            }

            return config ?? _defaultTargetFrameworkConfiguration;
        }

        public class LoaderInformation
        {
            public string AssemblyName { get; set; }

            public string TypeName { get; set; }
        }
    }
}
