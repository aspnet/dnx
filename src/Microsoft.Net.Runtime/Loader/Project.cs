using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Versioning;
using Newtonsoft.Json.Linq;
using NuGet;

namespace Microsoft.Net.Runtime.Loader
{
    public class Project
    {
        public const string ProjectFileName = "project.json";

        private Dictionary<FrameworkName, TargetFrameworkConfiguration> _configurations = new Dictionary<FrameworkName, TargetFrameworkConfiguration>();
        private Dictionary<FrameworkName, JToken> _compilationOptions = new Dictionary<FrameworkName, JToken>();
        private JToken _defaultOptions;

        private TargetFrameworkConfiguration _defaultTargetFrameworkConfiguration;

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

        public SemanticVersion Version { get; private set; }

        public IList<PackageReference> Dependencies { get; private set; }

        public IEnumerable<string> SourceFiles
        {
            get
            {
                string path = Path.GetDirectoryName(ProjectFilePath);

                string linkedFilePath = Path.Combine(path, ".include");

                var files = Enumerable.Empty<string>();
                if (File.Exists(linkedFilePath))
                {
                    files = File.ReadLines(linkedFilePath)
                                .Select(file => Path.Combine(path, file))
                                .Select(p => Path.GetFullPath(p));
                }

                return files.Concat(Directory.EnumerateFiles(path, "*.cs", SearchOption.AllDirectories));
            }
        }

        public IEnumerable<TargetFrameworkConfiguration> GetTargetFrameworkConfigurations()
        {
            return _configurations.Values;
        }

        public static bool HasProjectFile(string path)
        {
            string projectPath = Path.Combine(path, ProjectFileName);

            return File.Exists(projectPath);
        }

        public static bool TryGetProjectName(string path, out string projectName)
        {
            projectName = null;

            if (!HasProjectFile(path))
            {
                return false;
            }

            string projectPath = Path.Combine(path, ProjectFileName);
            string json = File.ReadAllText(projectPath);
            var settings = JObject.Parse(json);
            var name = settings["name"];
            projectName = GetValue<string>(settings, "name");

            if (String.IsNullOrEmpty(projectName))
            {
                // Assume the directory name is the project name
                projectName = GetDirectoryName(path);
            }

            return true;
        }

        public static bool TryGetProject(string path, out Project project)
        {
            project = null;

            string projectPath = null;

            if (Path.GetFileName(path) == ProjectFileName)
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
            project.Dependencies = new List<PackageReference>();
            project.ProjectFilePath = projectPath;

            if (project.Version.IsSnapshot)
            {
                var buildVersion = Environment.GetEnvironmentVariable("K_BUILD_VERSION") ?? "";
                project.Version = project.Version.SpecifySnapshot(buildVersion);
            }

            project.BuildTargetFrameworkConfigurations(settings);

            PopulateDependencies(project.Dependencies, settings);

            return true;
        }

        private static void PopulateDependencies(IList<PackageReference> results, JObject settings)
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

                    results.Add(new PackageReference
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

        public JToken GetCompilationOptions(FrameworkName frameworkName)
        {
            JToken optionsToken;
            if (_compilationOptions.TryGetValue(frameworkName, out optionsToken))
            {
                return optionsToken;
            }
            return null;
        }

        private void BuildTargetFrameworkConfigurations(JObject settings)
        {
            // Get the base configuration
            _defaultOptions = settings["compilationOptions"];

            _defaultTargetFrameworkConfiguration = new TargetFrameworkConfiguration
            {
                Dependencies = new List<PackageReference>()
            };

            // Parse the specific configuration section
            var configurations = settings["configurations"] as JObject;
            if (configurations != null)
            {
                foreach (var configuration in configurations)
                {
                    var config = new TargetFrameworkConfiguration();

                    config.FrameworkName = VersionUtility.ParseFrameworkName(configuration.Key);
                    var properties = configuration.Value.Value<JObject>();

                    config.Dependencies = new List<PackageReference>();

                    PopulateDependencies(config.Dependencies, properties);

                    _configurations[config.FrameworkName] = config;
                    _compilationOptions[config.FrameworkName] = properties["compilationOptions"];
                }
            }
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
            return path.Substring(Path.GetDirectoryName(path).Length).Trim(Path.DirectorySeparatorChar);
        }

        public TargetFrameworkConfiguration GetTargetFrameworkConfiguration(FrameworkName frameworkName)
        {
            TargetFrameworkConfiguration config;
            if (_configurations.TryGetValue(frameworkName, out config))
            {
                return config;
            }

            return _defaultTargetFrameworkConfiguration;
        }
    }

    public class TargetFrameworkConfiguration
    {
        public FrameworkName FrameworkName { get; set; }

        public IList<PackageReference> Dependencies { get; set; }
    }
}
