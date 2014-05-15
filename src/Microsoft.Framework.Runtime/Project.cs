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

        private Dictionary<FrameworkName, TargetFrameworkConfiguration> _configurations = new Dictionary<FrameworkName, TargetFrameworkConfiguration>();
        private Dictionary<FrameworkName, KeyValuePair<string, JToken>> _compilationOptions = new Dictionary<FrameworkName, KeyValuePair<string, JToken>>();
        private JToken _defaultOptions;

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

            project = new Project();

            project.SourcePattern = @"**\*.cs";
            project.PreprocessPattern = @"Compiler\Preprocess\**\*.cs";
            project.ResourcesPattern = @"Compiler\Resources\**\*";

            string json = File.ReadAllText(projectPath);
            var settings = JObject.Parse(json);
            var version = settings["version"];
            var authors = settings["authors"];
            project.Name = GetValue<string>(settings, "name");

            if (String.IsNullOrEmpty(project.Name))
            {
                // Assume the directory name is the project name
                project.Name = GetDirectoryName(path);
            }

            project.Version = version == null ? new SemanticVersion("1.0.0") : new SemanticVersion(version.Value<string>());
            project.Description = GetValue<string>(settings, "description");
            project.Authors = authors == null ? new string[] { } : authors.ToObject<string[]>();
            project.Dependencies = new List<Library>();
            project.ProjectFilePath = projectPath;
            project.EmbedInteropTypes = GetValue<bool>(settings, "embedInteropTypes");

            project.SourcePattern = GetSettingsValue(settings, "code", @"**\*.cs");
            project.SourceExcludePattern = GetSettingsValue(settings, "exclude", @"obj\**\*");
            project.PreprocessPattern = GetSettingsValue(settings, "preprocess", @"Compiler\Preprocess\**\*.cs");
            project.SharedPattern = GetSettingsValue(settings, "shared", @"Compiler\Shared\**\*.cs");
            project.ResourcesPattern = GetSettingsValue(settings, "resources", @"Compiler\Resources\**\*");

            var commands = settings["commands"] as JObject;
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

            project.BuildTargetFrameworkConfigurations(settings);

            PopulateDependencies(project.Dependencies, settings);

            return true;
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

        public JToken GetCompilationOptions()
        {
            return _defaultOptions;
        }

        public KeyValuePair<string, JToken> GetConfiguration(FrameworkName frameworkName)
        {
            KeyValuePair<string, JToken> optionsToken;
            if (_compilationOptions.TryGetValue(frameworkName, out optionsToken))
            {
                return optionsToken;
            }
            return new KeyValuePair<string, JToken>();
        }

        private void BuildTargetFrameworkConfigurations(JObject settings)
        {
            // Get the base configuration
            _defaultOptions = settings["compilationOptions"];

            _defaultTargetFrameworkConfiguration = new TargetFrameworkConfiguration
            {
                Dependencies = new List<Library>()
            };

            // Parse the specific configuration section
            var configurations = settings["configurations"] as JObject;
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
                    _compilationOptions[config.FrameworkName] = configuration;
                }
            }
        }

        private FrameworkName ParseFrameworkName(string configurationName)
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
