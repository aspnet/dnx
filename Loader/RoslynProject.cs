using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.Versioning;
using Newtonsoft.Json.Linq;
using NuGet;

namespace Loader
{
    public class RoslynProject
    {
        public const string ProjectFileName = "project.json";

        public string ProjectFilePath { get; private set; }

        public string Name { get; private set; }

        public string TargetFramework { get; private set; }

        public IList<Dependency> Dependencies { get; private set; }

        public IEnumerable<string> SourceFiles
        {
            get
            {
                string path = Path.GetDirectoryName(ProjectFilePath);
                return Directory.EnumerateFiles(path, "*.cs", SearchOption.AllDirectories);
            }
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
            projectName = name == null ? null : name.Value<string>();

            if (String.IsNullOrEmpty(projectName))
            {
                // Assume the directory name is the project name
                projectName = GetDirectoryName(path);
            }

            return true;
        }

        public static string GetDirectoryName(string path)
        {
            return path.Substring(Path.GetDirectoryName(path).Length).Trim(Path.DirectorySeparatorChar);
        }

        public static bool TryGetProject(string path, out RoslynProject project)
        {
            project = null;

            if (!HasProjectFile(path))
            {
                return false;
            }

            string projectPath = Path.Combine(path, ProjectFileName);

            project = new RoslynProject();

            string json = File.ReadAllText(projectPath);
            var settings = JObject.Parse(json);
            var targetFramework = settings["targetFramework"];
            var name = settings["name"];

            string framework = targetFramework == null ? "net45" : targetFramework.Value<string>();

            project.Name = name == null ? null : name.Value<string>();

            if (String.IsNullOrEmpty(project.Name))
            {
                // Assume the directory name is the project name
                project.Name = GetDirectoryName(path);
            }

            project.TargetFramework = framework;
            project.Dependencies = new List<Dependency>();
            project.ProjectFilePath = projectPath;

            var dependencies = settings["dependencies"] as JArray;
            if (dependencies != null)
            {
                foreach (JObject dependency in (IEnumerable<JToken>)dependencies)
                {
                    foreach (var prop in dependency)
                    {
                        var properties = prop.Value.Value<JObject>();

                        var version = properties["version"];

                        if (String.IsNullOrEmpty(prop.Key))
                        {
                            throw new InvalidDataException("Unable to resolve dependency ''.");
                        }

                        project.Dependencies.Add(new Dependency
                        {
                            Name = prop.Key,
                            Version = version != null ? new SemanticVersion(version.Value<string>()) : null
                        });
                    }
                }
            }

            return true;
        }
    }

    public class Dependency
    {
        public string Name { get; set; }

        public SemanticVersion Version { get; set; }

        public override string ToString()
        {
            return Name + " " + Version;
        }
    }
}
