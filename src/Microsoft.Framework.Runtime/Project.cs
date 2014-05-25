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

        private readonly Dictionary<FrameworkName, TargetFrameworkConfiguration> _configurations = new Dictionary<FrameworkName, TargetFrameworkConfiguration>();
        private readonly Dictionary<FrameworkName, CompilerOptions> _compilationOptions = new Dictionary<FrameworkName, CompilerOptions>();

        private CompilerOptions _defaultCompilerOptions;

        private TargetFrameworkConfiguration _defaultTargetFrameworkConfiguration;

        public Project()
        {
            Commands = new Dictionary<string, string>();
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

        public string SourcePattern { get; private set; }

        public string SourceExcludePattern { get; set; }

        public string PreprocessPattern { get; private set; }

        public string SharedPattern { get; set; }

        public string ResourcesPattern { get; private set; }

        public IEnumerable<string> SourceFiles
        {
            get
            {
                string path = ProjectDirectory;

                var files = Enumerable.Empty<string>();

                var includePatterns = SourcePattern.Split(new[] { ';' });
                var includeFiles = includePatterns
                    .Where(pattern => !string.IsNullOrEmpty(pattern))
                    .SelectMany(pattern => PathResolver.PerformWildcardSearch(path, pattern))
                    .ToArray();

                var excludePatterns = (PreprocessPattern + ";" + SharedPattern + ";" + ResourcesPattern + ";" + SourceExcludePattern)
                    .Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries)
                    .Select(pattern => PathResolver.NormalizeWildcardForExcludedFiles(path, pattern))
                    .ToArray();

                var excludeFiles = PathResolver.GetMatches(includeFiles, x => x, excludePatterns)
                    .ToArray();

                return files.Concat(includeFiles.Except(excludeFiles)).Distinct().ToArray();
            }
        }

        public IEnumerable<string> ResourceFiles
        {
            get
            {
                string path = ProjectDirectory;

                var includePatterns = ResourcesPattern.Split(new[] { ';' });
                var includeFiles = includePatterns
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

                var includePatterns = SharedPattern.Split(new[] { ';' });
                var includeFiles = includePatterns
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
            var fallbackProjectName = GetDirectoryName(path);

            project = GetProject(json, fallbackProjectName, projectPath);

            return true;
        }

        public static Project GetProject(string json, string fallbackProjectName, string projectPath)
        {
            var project = new Project();

            project.SourcePattern = @"**\*.cs";
            project.PreprocessPattern = @"Compiler\Preprocess\**\*.cs";
            project.ResourcesPattern = @"Compiler\Resources\**\*";

            var rawProject = JObject.Parse(json);
            var version = rawProject["version"];
            var authors = rawProject["authors"];
            project.Name = GetValue<string>(rawProject, "name");

            if (String.IsNullOrEmpty(project.Name))
            {
                project.Name = fallbackProjectName;
            }

            project.Version = version == null ? new SemanticVersion("1.0.0") : new SemanticVersion(version.Value<string>());
            project.Description = GetValue<string>(rawProject, "description");
            project.Authors = authors == null ? new string[] { } : authors.ToObject<string[]>();
            project.Dependencies = new List<Library>();
            project.ProjectFilePath = projectPath;
            project.EmbedInteropTypes = GetValue<bool>(rawProject, "embedInteropTypes");

            project.SourcePattern = GetSettingsValue(rawProject, "code", @"**\*.cs");
            project.SourceExcludePattern = GetSettingsValue(rawProject, "exclude", @"obj\**\*");
            project.PreprocessPattern = GetSettingsValue(rawProject, "preprocess", @"Compiler\Preprocess\**\*.cs");
            project.SharedPattern = GetSettingsValue(rawProject, "shared", @"Compiler\Shared\**\*.cs");
            project.ResourcesPattern = GetSettingsValue(rawProject, "resources", @"Compiler\Resources\**\*");

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

        private static string GetSettingsValue(JObject settings, string propertyName, string defaultValue)
        {
            var token = settings[propertyName];
            return token != null ? token.Value<string>() : defaultValue;
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
    }

    public class TargetFrameworkConfiguration : IFrameworkTargetable
    {
        public FrameworkName FrameworkName { get; set; }

        public IList<Library> Dependencies { get; set; }

        public IEnumerable<FrameworkName> SupportedFrameworks
        {
            get
            {
                return new[] { FrameworkName };
            }
        }
    }
}
