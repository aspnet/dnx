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
    public class ProjectReader : IProjectReader
    {
        public const string ProjectFileName = "project.json";
        private static readonly char[] _sourceSeparator = new[] { ';' };

        internal static readonly string[] _defaultSourcePatterns = new[] { @"**\*.cs" };
        internal static readonly string[] _defaultExcludePatterns = new[] { @"obj", @"bin" };
        internal static readonly string[] _defaultPackExcludePatterns = new[] { @"obj", @"bin", @"**\.*\**" };
        internal static readonly string[] _defaultPreprocessPatterns = new[] { @"compiler\preprocess\**\*.cs" };
        internal static readonly string[] _defaultSharedPatterns = new[] { @"compiler\shared\**\*.cs" };
        internal static readonly string[] _defaultResourcesPatterns = new[] { @"compiler\resources\**\*" };
        internal static readonly string[] _defaultContentsPatterns = new[] { @"**\*" };
        private readonly IAssemblyLoadContext _loadContext;

        public ProjectReader(IAssemblyLoadContext loadContext)
        {
            _loadContext = loadContext;
        }

        public bool TryReadProject(string path, out Project project)
        {
            project = null;

            string projectPath = null;

            if (string.Equals(Path.GetFileName(path), Project.ProjectFileName, StringComparison.OrdinalIgnoreCase))
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
                projectPath = Path.Combine(path, Project.ProjectFileName);
            }

            var json = File.ReadAllText(projectPath);

            // Assume the directory name is the project name if none was specified
            var projectName = GetDirectoryName(path);

            project = GetProject(json, projectName, projectPath);

            return true;
        }

        internal Project GetProject(string json, string projectName, string projectPath)
        {
            var rawProject = JObject.Parse(json);

            // Metadata properties
            var version = rawProject["version"];
            var authors = rawProject["authors"];
            var tags = rawProject["tags"];

            var project = new Project
            {
                Name = projectName,
                Version = new SemanticVersion(version?.Value<string>() ?? "1.0.0"),
                Description = GetValue<string>(rawProject, "description"),
                Authors = authors == null ? new string[] { } : authors.ToObject<string[]>(),
                ProjectFilePath = Path.GetFullPath(projectPath),
                WebRoot = GetValue<string>(rawProject, "webroot"),
                EntryPoint = GetValue<string>(rawProject, "entryPoint"),
                ProjectUrl = GetValue<string>(rawProject, "projectUrl"),
                RequireLicenseAcceptance = GetValue<bool?>(rawProject, "requireLicenseAcceptance") ?? false,
                Tags = tags == null ? new string[] { } : tags.ToObject<string[]>(),
                IsLoadable = GetValue<bool?>(rawProject, "loadable") ?? true,
            };

            // TODO: Move this to the dependencies node
            project.EmbedInteropTypes = GetValue<bool>(rawProject, "embedInteropTypes");

            // Source file patterns
            project.SourcePatterns = GetSourcePattern(project, rawProject, "code", _defaultSourcePatterns);
            project.ExcludePatterns = GetSourcePattern(project, rawProject, "exclude", _defaultExcludePatterns);
            project.PackExcludePatterns = GetSourcePattern(project, rawProject, "packExclude", _defaultPackExcludePatterns);
            project.PreprocessPatterns = GetSourcePattern(project, rawProject, "preprocess", _defaultPreprocessPatterns);
            project.SharedPatterns = GetSourcePattern(project, rawProject, "shared", _defaultSharedPatterns);
            project.ResourcesPatterns = GetSourcePattern(project, rawProject, "resources", _defaultResourcesPatterns);
            project.ContentsPatterns = GetSourcePattern(project, rawProject, "files", _defaultContentsPatterns);

            // Set the default loader information for projects
            var languageServicesAssembly = project.DefaultLanguageServicesAssembly;
            var projectReferenceProviderType = project.DefaultProjectReferenceProviderType;
            var compilerOptionsReaderType = project.DefaultCompilationOptionsReaderType;
            var languageName = "C#";
            var languageInfo = rawProject["language"] as JObject;

            if (languageInfo != null)
            {
                languageName = GetValue<string>(languageInfo, "name");
                languageServicesAssembly = GetValue<string>(languageInfo, "assembly");
                projectReferenceProviderType = GetValue<string>(languageInfo, "projectReferenceProviderType");

                compilerOptionsReaderType = GetValue<string>(languageInfo, "compilerOptionsReaderType");
            }

            var libraryExporter = new TypeInformation(languageServicesAssembly,
                                                      projectReferenceProviderType);

            var compilerOptions = new TypeInformation(languageServicesAssembly,
                                                      compilerOptionsReaderType);

            project.LanguageServices = new LanguageServices(languageName, libraryExporter, compilerOptions);

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
                        throw new InvalidDataException(string.Format(
                            "The value of a script in {0} can only be a string or an array of strings", Project.ProjectFileName));
                    }
                }
            }

            if (project.Version.IsSnapshot)
            {
                var buildVersion = Environment.GetEnvironmentVariable("K_BUILD_VERSION");
                project.Version = project.Version.SpecifySnapshot(buildVersion);
            }

            var compilerOptionsReader = compilerOptions.CreateInstance<ICompilerOptionsReader>(_loadContext, serviceProvider: null);
            BuildTargetFrameworksAndConfigurations(rawProject, project, compilerOptionsReader);

            PopulateDependencies(
                project.Dependencies,
                rawProject,
                "dependencies",
                isGacOrFrameworkReference: false);

            return project;
        }

        private static bool HasProjectFile(string path)
        {
            var projectPath = Path.Combine(path, Project.ProjectFileName);

            return File.Exists(projectPath);
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

        private void BuildTargetFrameworksAndConfigurations(JObject rawProject,
                                                            Project project,
                                                            ICompilerOptionsReader compilerOptionsReader)
        {
            // Get the shared compilationOptions
            project.DefaultCompilerOptions = compilerOptionsReader.ReadCompilerOptions(GetCompilationOptionsContent(rawProject));

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
                    var compilerOptionsContent = GetCompilationOptionsContent(configuration.Value);

                    var compilerOptions = compilerOptionsReader.ReadConfigurationCompilerOptions(compilerOptionsContent,
                                                                                                 configuration.Key);
                    project.Configurations[configuration.Key] = compilerOptions;
                }
            }
            else
            {
                // Add default configurations
                project.Configurations["Debug"] = compilerOptionsReader.ReadConfigurationCompilerOptions(json: null, configuration: "Debug");
                project.Configurations["Release"] = compilerOptionsReader.ReadConfigurationCompilerOptions(json: null, configuration: "Release");
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
                    BuildTargetFrameworkInformation(project, compilerOptionsReader, framework);
                }
            }
        }

        private static string GetCompilationOptionsContent(JToken topLevelOrConfiguration)
        {
            var rawOptions = topLevelOrConfiguration["compilationOptions"];

            if (rawOptions == null)
            {
                return null;
            }

            return rawOptions.ToString();
        }

        private static void PopulateDependencies(
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
                        throw new InvalidDataException("Unable to resolve dependency ''.");
                    }

                    // Support 
                    // "dependencies" : {
                    //    "Name" : "1.0"
                    // }

                    var dependencyValue = dependency.Value;
                    string dependencyVersionValue = null;
                    var dependencyTypeValue = LibraryDependencyType.Default;
                    if (dependencyValue.Type == JTokenType.String)
                    {
                        dependencyVersionValue = dependencyValue.Value<string>();
                    }
                    else
                    {
                        if (dependencyValue.Type == JTokenType.Object)
                        {
                            var dependencyVersionToken = dependencyValue["version"];
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

                    SemanticVersion dependencyVersion = null;
                    if (!string.IsNullOrEmpty(dependencyVersionValue))
                    {
                        dependencyVersion = SemanticVersion.Parse(dependencyVersionValue);
                    }

                    results.Add(new LibraryDependency(
                        name: dependency.Key,
                        version: dependencyVersion,
                        isGacOrFrameworkReference: isGacOrFrameworkReference,
                        type: dependencyTypeValue
                    ));
                }
            }
        }

        private static void BuildTargetFrameworkInformation(Project project,
                                                            ICompilerOptionsReader compilerOptionsReader,
                                                            KeyValuePair<string, JToken> targetFramework)
        {
            var frameworkName = ParseFrameworkName(targetFramework.Key);

            // If it's not unsupported then keep it
            if (frameworkName == VersionUtility.UnsupportedFrameworkName)
            {
                // REVIEW: Should we skip unsupported target frameworks
                return;
            }

            // Add the target framework specific define
            var frameworkDefinition = Tuple.Create(targetFramework.Key, frameworkName);
            var targetFrameworkInformation = new TargetFrameworkInformation
            {
                FrameworkName = frameworkName,
                Dependencies = new List<LibraryDependency>()
            };

            var properties = targetFramework.Value.Value<JObject>();

            PopulateDependencies(
                targetFrameworkInformation.Dependencies,
                properties,
                "dependencies",
                isGacOrFrameworkReference: false);

            var frameworkAssemblies = new List<LibraryDependency>();
            PopulateDependencies(
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

            // If no compilation options are provided then figure them out from the node
            var compilerOptionsContent = GetCompilationOptionsContent(targetFramework.Value);
            var compilerOptions = compilerOptionsReader.ReadFrameworkCompilerOptions(compilerOptionsContent,
                                                                                     targetFramework.Key,
                                                                                     frameworkName);

            // Add the target framework specific define
            targetFrameworkInformation.CompilerOptions = compilerOptions;

            project.AddTargetFramework(frameworkName, targetFrameworkInformation);
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