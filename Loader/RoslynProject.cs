using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.Versioning;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NuGet;

namespace Loader
{
    public class RoslynProject
    {
        public const string ProjectFileName = "project.json";

        public string ProjectFilePath { get; private set; }

        public string Name { get; private set; }

        public FrameworkName TargetFramework { get; private set; }

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
            projectName = settings["name"].Value<string>();

            if (String.IsNullOrEmpty(projectName))
            {
                throw new InvalidDataException("A project name is required.");
            }

            return true;
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

            string framework = targetFramework == null ? "net40" : targetFramework.Value<string>();

            project.Name = settings["name"].Value<string>();

            if (String.IsNullOrEmpty(project.Name))
            {
                throw new InvalidDataException("A project name is required.");
            }

            project.TargetFramework = VersionUtility.ParseFrameworkName(framework);
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
                            Version = version != null ? version.Value<string>() : null
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

        public string Version { get; set; }

        public override string ToString()
        {
            return Name + " " + Version;
        }
    }
}
