using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NuGet.Frameworks;
using NuGet.Versioning;

namespace NuGet.Packaging
{
    public class ProjectReader
    {
        public static bool HasProjectFile(string path)
        {
            string projectPath = Path.Combine(path, PackageSpec.ProjectFileName);

            return File.Exists(projectPath);
        }

        public static bool TryReadProject(string path, out PackageSpec project)
        {
            project = null;

            string projectPath = null;

            if (string.Equals(Path.GetFileName(path), PackageSpec.ProjectFileName, StringComparison.OrdinalIgnoreCase))
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
                projectPath = Path.Combine(path, PackageSpec.ProjectFileName);
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
                throw PackageSpecFormatException.Create(ex, projectPath);
            }

            return true;
        }

        public static PackageSpec GetProject(string json, string projectName, string projectPath)
        {
            var ms = new MemoryStream(Encoding.UTF8.GetBytes(json));
            return GetProject(ms, projectName, projectPath);
        }

        public static PackageSpec GetProject(Stream stream, string projectName, string projectPath)
        {
            var project = new PackageSpec();

            var reader = new JsonTextReader(new StreamReader(stream));
            var rawProject = JObject.Load(reader);

            // Metadata properties
            var version = rawProject["version"];
            var authors = rawProject["authors"];
            var owners = rawProject["owners"];
            var tags = rawProject["tags"];
            
            project.Name = projectName;
            project.ProjectFilePath = Path.GetFullPath(projectPath);

            if (version == null)
            {
                project.Version = new NuGetVersion("1.0.0");
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

                    throw PackageSpecFormatException.Create(ex, version, project.ProjectFilePath);
                }
            }

            project.Description = GetValue<string>(rawProject, "description");
            project.Authors = authors == null ? new string[] { } : authors.Value<string[]>();
            project.Owners = owners == null ? new string[] { } : owners.Value<string[]>();
            project.Dependencies = new List<LibraryDependency>();
            project.ProjectUrl = GetValue<string>(rawProject, "projectUrl");
            project.IconUrl = GetValue<string>(rawProject, "iconUrl");
            project.LicenseUrl = GetValue<string>(rawProject, "licenseUrl");
            project.Copyright = GetValue<string>(rawProject, "copyright");
            project.Language = GetValue<string>(rawProject, "language");
            project.RequireLicenseAcceptance = GetValue<bool?>(rawProject, "requireLicenseAcceptance") ?? false;
            project.Tags = tags == null ? new string[] { } : tags.ToObject<string[]>();

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
                        throw PackageSpecFormatException.Create(
                            string.Format("The value of a script in '{0}' can only be a string or an array of strings", PackageSpec.ProjectFileName),
                            value,
                            project.ProjectFilePath);
                    }
                }
            }

            BuildTargetFrameworks(project, rawProject);

            PopulateDependencies(
                project.ProjectFilePath,
                project.Dependencies,
                rawProject,
                "dependencies",
                isGacOrFrameworkReference: false);

            return project;
        }

        private static NuGetVersion SpecifySnapshot(string version, string snapshotValue)
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

            return new NuGetVersion(version);
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

                        throw PackageSpecFormatException.Create(
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

                    NuGetVersionRange dependencyVersionRange = null;

                    if (!string.IsNullOrEmpty(dependencyVersionValue))
                    {
                        try
                        {
                            dependencyVersionRange = NuGetVersionRange.Parse(dependencyVersionValue);
                        }
                        catch (Exception ex)
                        {
                            throw PackageSpecFormatException.Create(
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

        private static void BuildTargetFrameworks(PackageSpec project, JObject rawProject)
        {
            // The frameworks node is where target frameworks go
            /*
                {
                    "frameworks": {
                        "net45": {
                        },
                        "aspnet50": {
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
                        BuildTargetFrameworkNode(project, framework);
                    }
                    catch (Exception ex)
                    {
                        throw PackageSpecFormatException.Create(ex, framework.Value, project.ProjectFilePath);
                    }
                }
            }
        }

        private static bool BuildTargetFrameworkNode(PackageSpec project, KeyValuePair<string, JToken> targetFramework)
        {
            var frameworkName = NuGetFramework.Parse(targetFramework.Key);

            // If it's not unsupported then keep it
            if (frameworkName == NuGetFramework.UnsupportedFramework)
            {
                // REVIEW: Should we skip unsupported target frameworks
                return false;
            }

            var targetFrameworkInformation = new TargetFrameworkInformation
            {
                FrameworkName = frameworkName,
                Dependencies = new List<LibraryDependency>()
            };

            var properties = targetFramework.Value.Value<JObject>();

            PopulateDependencies(
                project.ProjectFilePath,
                targetFrameworkInformation.Dependencies,
                properties,
                "dependencies",
                isGacOrFrameworkReference: false);

            var frameworkAssemblies = new List<LibraryDependency>();
            PopulateDependencies(
                project.ProjectFilePath,
                frameworkAssemblies,
                properties,
                "frameworkAssemblies",
                isGacOrFrameworkReference: true);

            frameworkAssemblies.ForEach(d => targetFrameworkInformation.Dependencies.Add(d));


            project.TargetFrameworks.Add(targetFrameworkInformation);

            return true;
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
    }
}