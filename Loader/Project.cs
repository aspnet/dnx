using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Versioning;
using Newtonsoft.Json.Linq;
using NuGet;

namespace Loader
{
    public class Project
    {
        public const string ProjectFileName = "project.json";

        public string ProjectFilePath { get; private set; }

        public string Name { get; private set; }

        public string Description { get; private set; }

        public string[] Authors { get; private set; }

        public SemanticVersion Version { get; private set; }

        public FrameworkName TargetFramework { get; private set; }

        public IList<Dependency> Dependencies { get; private set; }

        public IEnumerable<string> SourceFiles
        {
            get
            {
                string path = Path.GetDirectoryName(ProjectFilePath);

                string linkedFilePath = Path.Combine(path, ".include");

                var files = Enumerable.Empty<string>();
                if (File.Exists(linkedFilePath))
                {
                    files = File.ReadAllLines(linkedFilePath)
                                .Select(file => Path.Combine(path, file))
                                .Select(p => Path.GetFullPath(p));
                }

                return files.Concat(Directory.EnumerateFiles(path, "*.cs", SearchOption.AllDirectories));
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

        public static bool TryGetProject(string path, out Project project)
        {
            project = null;

            string projectPath = null;

            if (Path.GetFileName(path) == ProjectFileName)
            {
                projectPath = path;
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
            var targetFramework = settings["targetFramework"];
            var name = settings["name"];
            var version = settings["version"];
            var description = settings["description"];
            var authors = settings["authors"];

            string framework = targetFramework == null ? "net45" : targetFramework.Value<string>();

            project.Name = name == null ? null : name.Value<string>();

            if (String.IsNullOrEmpty(project.Name))
            {
                // Assume the directory name is the project name
                project.Name = GetDirectoryName(path);
            }

            project.Version = version == null ? new SemanticVersion("1.0.0") : new SemanticVersion(version.Value<string>());
            project.TargetFramework = VersionUtility.ParseFrameworkName(framework);
            project.Description = description == null ? null : description.Value<string>();
            project.Authors = authors == null ? new string[] { } : authors.ToObject<string[]>();
            project.Dependencies = new List<Dependency>();
            project.ProjectFilePath = projectPath;

            var dependencies = settings["dependencies"] as JArray;
            if (dependencies != null)
            {
                foreach (JObject dependency in (IEnumerable<JToken>)dependencies)
                {
                    foreach (var prop in dependency)
                    {
                        if (String.IsNullOrEmpty(prop.Key))
                        {
                            throw new InvalidDataException("Unable to resolve dependency ''.");
                        }

                        var properties = prop.Value.Value<JObject>();
                        var dependencyVersion = properties["version"];
                        SemanticVersion semVer = null;

                        if (dependencyVersion != null)
                        {
                            SemanticVersion.TryParse(dependencyVersion.Value<string>(), out semVer);
                        }

                        project.Dependencies.Add(new Dependency
                        {
                            Name = prop.Key,
                            Version = semVer
                        });
                    }
                }
            }

            return true;
        }
    }

    public class Dependency : IEquatable<Dependency>
    {
        public string Name { get; set; }

        public SemanticVersion Version { get; set; }

        public override string ToString()
        {
            return Name + " " + Version;
        }

        public bool Equals(Dependency other)
        {
            if (ReferenceEquals(null, other)) return false;
            if (ReferenceEquals(this, other)) return true;
            return string.Equals(Name, other.Name) && Equals(Version, other.Version);
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != this.GetType()) return false;
            return Equals((Dependency) obj);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return ((Name != null ? Name.GetHashCode() : 0)*397) ^ (Version != null ? Version.GetHashCode() : 0);
            }
        }

        public static bool operator ==(Dependency left, Dependency right)
        {
            return Equals(left, right);
        }

        public static bool operator !=(Dependency left, Dependency right)
        {
            return !Equals(left, right);
        }
    }
}
