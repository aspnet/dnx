// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Dnx.Runtime.Helpers;
using Microsoft.Dnx.Runtime.Internals;
using Microsoft.Extensions.JsonParser.Sources;
using Microsoft.Extensions.CompilationAbstractions;
using NuGet;

namespace Microsoft.Dnx.Runtime
{
    public class ProjectReader
    {
        public Project ReadProject(Stream stream, string projectName, string projectPath, ICollection<DiagnosticMessage> diagnostics)
        {
            var project = new Project();

            var reader = new StreamReader(stream);
            var rawProject = JsonDeserializer.Deserialize(reader) as JsonObject;
            if (rawProject == null)
            {
                throw FileFormatException.Create(
                    "The JSON file can't be deserialized to a JSON object.",
                    projectPath);
            }

            // Meta-data properties
            project.Name = projectName;
            project.ProjectFilePath = Path.GetFullPath(projectPath);

            var version = rawProject.Value("version") as JsonString;
            if (version == null)
            {
                project.Version = new SemanticVersion("1.0.0");
            }
            else
            {
                try
                {
                    var buildVersion = Environment.GetEnvironmentVariable("DNX_BUILD_VERSION");
                    project.Version = SpecifySnapshot(version, buildVersion);
                }
                catch (Exception ex)
                {
                    throw FileFormatException.Create(ex, version, project.ProjectFilePath);
                }
            }

            var fileVersion = Environment.GetEnvironmentVariable("DNX_ASSEMBLY_FILE_VERSION");
            if (string.IsNullOrWhiteSpace(fileVersion))
            {
                project.AssemblyFileVersion = project.Version.Version;
            }
            else
            {
                try
                {
                    var simpleVersion = project.Version.Version;
                    project.AssemblyFileVersion = new Version(simpleVersion.Major,
                        simpleVersion.Minor,
                        simpleVersion.Build,
                        int.Parse(fileVersion));
                }
                catch (FormatException ex)
                {
                    throw new FormatException("The assembly file version is invalid: " + fileVersion, ex);
                }
            }

            project.Description = rawProject.ValueAsString("description");
            project.Summary = rawProject.ValueAsString("summary");
            project.Copyright = rawProject.ValueAsString("copyright");
            project.Title = rawProject.ValueAsString("title");
            project.EntryPoint = rawProject.ValueAsString("entryPoint");
            project.ProjectUrl = rawProject.ValueAsString("projectUrl");
            project.LicenseUrl = rawProject.ValueAsString("licenseUrl");
            project.IconUrl = rawProject.ValueAsString("iconUrl");

            project.Authors = rawProject.ValueAsStringArray("authors") ?? new string[] { };
            project.Owners = rawProject.ValueAsStringArray("owners") ?? new string[] { };
            project.Tags = rawProject.ValueAsStringArray("tags") ?? new string[] { };

            project.Language = rawProject.ValueAsString("language");
            project.ReleaseNotes = rawProject.ValueAsString("releaseNotes");

            project.RequireLicenseAcceptance = rawProject.ValueAsBoolean("requireLicenseAcceptance", defaultValue: false);
            project.IsLoadable = rawProject.ValueAsBoolean("loadable", defaultValue: true);
            // TODO: Move this to the dependencies node
            project.EmbedInteropTypes = rawProject.ValueAsBoolean("embedInteropTypes", defaultValue: false);

            project.Dependencies = new List<LibraryDependency>();

            // Project files
            project.Files = new ProjectFilesCollection(rawProject, project.ProjectDirectory, project.ProjectFilePath);

            var compilerInfo = rawProject.ValueAsJsonObject("compiler");
            if (compilerInfo != null)
            {
                var languageName = compilerInfo.ValueAsString("name") ?? "C#";
                var compilerAssembly = compilerInfo.ValueAsString("compilerAssembly");
                var compilerType = compilerInfo.ValueAsString("compilerType");

                var compiler = new TypeInformation(compilerAssembly, compilerType);
                project.CompilerServices = new CompilerServices(languageName, compiler);
            }

            var commands = rawProject.Value("commands") as JsonObject;
            if (commands != null)
            {
                foreach (var key in commands.Keys)
                {
                    var value = commands.ValueAsString(key);
                    if (value != null)
                    {
                        project.Commands[key] = value;
                    }
                }
            }

            var scripts = rawProject.Value("scripts") as JsonObject;
            if (scripts != null)
            {
                foreach (var key in scripts.Keys)
                {
                    var stringValue = scripts.ValueAsString(key);
                    if (stringValue != null)
                    {
                        project.Scripts[key] = new string[] { stringValue };
                        continue;
                    }

                    var arrayValue = scripts.ValueAsStringArray(key);
                    if (arrayValue != null)
                    {
                        project.Scripts[key] = arrayValue;
                        continue;
                    }

                    throw FileFormatException.Create(
                        string.Format("The value of a script in {0} can only be a string or an array of strings", Project.ProjectFileName),
                        scripts.Value(key),
                        project.ProjectFilePath);
                }
            }

            BuildTargetFrameworksAndConfigurations(project, rawProject, diagnostics);

            PopulateDependencies(
                project.ProjectFilePath,
                project.Dependencies,
                rawProject,
                "dependencies",
                isGacOrFrameworkReference: false);

            return project;
        }

        private static SemanticVersion SpecifySnapshot(string version, string snapshotValue)
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

            return new SemanticVersion(version);
        }

        private static void PopulateDependencies(
            string projectPath,
            IList<LibraryDependency> results,
            JsonObject settings,
            string propertyName,
            bool isGacOrFrameworkReference)
        {
            var dependencies = settings.ValueAsJsonObject(propertyName);
            if (dependencies != null)
            {
                foreach (var dependencyKey in dependencies.Keys)
                {
                    if (string.IsNullOrEmpty(dependencyKey))
                    {
                        throw FileFormatException.Create(
                            "Unable to resolve dependency ''.",
                            dependencies.Value(dependencyKey),
                            projectPath);
                    }

                    var dependencyValue = dependencies.Value(dependencyKey);
                    var dependencyTypeValue = LibraryDependencyType.Default;
                    JsonString dependencyVersionAsString = null;
                    string target = null;

                    if (dependencyValue is JsonObject)
                    {
                        // "dependencies" : { "Name" : { "version": "1.0", "type": "build", "target": "project" } }
                        var dependencyValueAsObject = (JsonObject)dependencyValue;
                        dependencyVersionAsString = dependencyValueAsObject.ValueAsString("version");

                        // Remove support for flags (we only support build and nothing right now)
                        var type = dependencyValueAsObject.ValueAsString("type");
                        if (type != null)
                        {
                            dependencyTypeValue = LibraryDependencyType.Parse(type.Value);
                        }

                        // Read the target if specified
                        target = dependencyValueAsObject.ValueAsString("target")?.Value;
                    }
                    else if (dependencyValue is JsonString)
                    {
                        // "dependencies" : { "Name" : "1.0" }
                        dependencyVersionAsString = (JsonString)dependencyValue;
                    }
                    else
                    {
                        throw FileFormatException.Create(
                            string.Format("Invalid dependency version: {0}. The format is not recognizable.", dependencyKey),
                            dependencyValue,
                            projectPath);
                    }

                    SemanticVersionRange dependencyVersionRange = null;
                    if (!string.IsNullOrEmpty(dependencyVersionAsString?.Value))
                    {
                        try
                        {
                            dependencyVersionRange = VersionUtility.ParseVersionRange(dependencyVersionAsString.Value);
                        }
                        catch (Exception ex)
                        {
                            throw FileFormatException.Create(
                                ex,
                                dependencyValue,
                                projectPath);
                        }
                    }

                    results.Add(new LibraryDependency
                    {
                        LibraryRange = new LibraryRange(dependencyKey, isGacOrFrameworkReference)
                        {
                            VersionRange = dependencyVersionRange,
                            FileName = projectPath,
                            Line = dependencyValue.Line,
                            Column = dependencyValue.Column,
                            Target = target
                        },
                        Type = dependencyTypeValue
                    });
                }
            }
        }

        private static bool TryGetStringEnumerable(JsonObject parent, string property, out IEnumerable<string> result)
        {
            var collection = new List<string>();
            var valueInString = parent.ValueAsString(property);
            if (valueInString != null)
            {
                collection.Add(valueInString);
            }
            else
            {
                var valueInArray = parent.ValueAsStringArray(property);
                if (valueInArray != null)
                {
                    collection.AddRange(valueInArray);
                }
                else
                {
                    result = null;
                    return false;
                }
            }

            result = collection.SelectMany(value => value.Split(new[] { ' ', ',' }, StringSplitOptions.RemoveEmptyEntries));
            return true;
        }

        private void BuildTargetFrameworksAndConfigurations(Project project, JsonObject projectJsonObject, ICollection<DiagnosticMessage> diagnostics)
        {
            // Get the shared compilationOptions
            project._defaultCompilerOptions = GetCompilationOptions(projectJsonObject) ?? new CompilerOptions();

            project._defaultTargetFrameworkConfiguration = new TargetFrameworkInformation
            {
                Dependencies = new List<LibraryDependency>()
            };

            // Add default configurations
            project._compilerOptionsByConfiguration["Debug"] = new CompilerOptions
            {
                Defines = new[] { "DEBUG", "TRACE" },
                Optimize = false
            };

            project._compilerOptionsByConfiguration["Release"] = new CompilerOptions
            {
                Defines = new[] { "RELEASE", "TRACE" },
                Optimize = true
            };

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

            var configurationsSection = projectJsonObject.ValueAsJsonObject("configurations");
            if (configurationsSection != null)
            {
                foreach (var configKey in configurationsSection.Keys)
                {
                    var compilerOptions = GetCompilationOptions(configurationsSection.ValueAsJsonObject(configKey));

                    // Only use this as a configuration if it's not a target framework
                    project._compilerOptionsByConfiguration[configKey] = compilerOptions;
                }
            }

            // The frameworks node is where target frameworks go
            /*
                {
                    "frameworks": {
                        "net45": {
                        },
                        "dnxcore50": {
                        }
                    }
                }
            */

            var frameworks = projectJsonObject.ValueAsJsonObject("frameworks");
            if (frameworks != null)
            {
                foreach (var frameworkKey in frameworks.Keys)
                {
                    try
                    {
                        var frameworkToken = frameworks.ValueAsJsonObject(frameworkKey);
                        var success = BuildTargetFrameworkNode(project, frameworkKey, frameworkToken);
                        if (!success)
                        {
                            diagnostics?.Add(
                                new DiagnosticMessage(
                                    DiagnosticMonikers.NU1008,
                                    $"\"{frameworkKey}\" is an unsupported framework.",
                                    project.ProjectFilePath,
                                    DiagnosticMessageSeverity.Error,
                                    frameworkToken.Line,
                                    frameworkToken.Column));
                        }
                    }
                    catch (Exception ex)
                    {
                        throw FileFormatException.Create(ex, frameworks.Value(frameworkKey), project.ProjectFilePath);
                    }
                }
            }
        }

        /// <summary>
        /// Parse a Json object which represents project configuration for a specified framework
        /// </summary>
        /// <param name="frameworkKey">The name of the framework</param>
        /// <param name="frameworkValue">The Json object represent the settings</param>
        /// <returns>Returns true if it successes.</returns>
        private bool BuildTargetFrameworkNode(Project project, string frameworkKey, JsonObject frameworkValue)
        {
            // If no compilation options are provided then figure them out from the node
            var compilerOptions = GetCompilationOptions(frameworkValue) ??
                                  new CompilerOptions();

            var frameworkName = FrameworkNameHelper.ParseFrameworkName(frameworkKey);

            // If it's not unsupported then keep it
            if (frameworkName == VersionUtility.UnsupportedFrameworkName)
            {
                // REVIEW: Should we skip unsupported target frameworks
                return false;
            }

            // Add the target framework specific define
            var defines = new HashSet<string>(compilerOptions.Defines ?? Enumerable.Empty<string>());
            var frameworkDefinition = Tuple.Create(frameworkKey, frameworkName);
            var frameworkDefine = FrameworkNameHelper.MakeDefaultTargetFrameworkDefine(frameworkDefinition);

            if (!string.IsNullOrEmpty(frameworkDefine))
            {
                defines.Add(frameworkDefine);
            }

            compilerOptions.Defines = defines;

            var targetFrameworkInformation = new TargetFrameworkInformation
            {
                FrameworkName = frameworkName,
                Dependencies = new List<LibraryDependency>()
            };

            var frameworkDependencies = new List<LibraryDependency>();

            PopulateDependencies(
                project.ProjectFilePath,
                frameworkDependencies,
                frameworkValue,
                "dependencies",
                isGacOrFrameworkReference: false);

            var frameworkAssemblies = new List<LibraryDependency>();
            PopulateDependencies(
                project.ProjectFilePath,
                frameworkAssemblies,
                frameworkValue,
                "frameworkAssemblies",
                isGacOrFrameworkReference: true);

            frameworkDependencies.AddRange(frameworkAssemblies);
            targetFrameworkInformation.Dependencies = frameworkDependencies;

            targetFrameworkInformation.WrappedProject = frameworkValue.ValueAsString("wrappedProject");

            var binNode = frameworkValue.ValueAsJsonObject("bin");
            if (binNode != null)
            {
                targetFrameworkInformation.AssemblyPath = binNode.ValueAsString("assembly");
                targetFrameworkInformation.PdbPath = binNode.ValueAsString("pdb");
            }

            project._compilerOptionsByFramework[frameworkName] = compilerOptions;
            project._targetFrameworks[frameworkName] = targetFrameworkInformation;

            return true;
        }

        private static CompilerOptions GetCompilationOptions(JsonObject rawObject)
        {
            var rawOptions = rawObject.ValueAsJsonObject("compilationOptions");
            if (rawOptions == null)
            {
                return null;
            }

            return new CompilerOptions
            {
                Defines = rawOptions.ValueAsStringArray("define"),
                LanguageVersion = rawOptions.ValueAsString("languageVersion"),
                AllowUnsafe = rawOptions.ValueAsNullableBoolean("allowUnsafe"),
                Platform = rawOptions.ValueAsString("platform"),
                WarningsAsErrors = rawOptions.ValueAsNullableBoolean("warningsAsErrors"),
                Optimize = rawOptions.ValueAsNullableBoolean("optimize"),
                KeyFile = rawOptions.ValueAsString("keyFile"),
                DelaySign = rawOptions.ValueAsNullableBoolean("delaySign"),
                UseOssSigning = rawOptions.ValueAsNullableBoolean("useOssSigning"),
                EmitEntryPoint = rawOptions.ValueAsNullableBoolean("emitEntryPoint")
            };
        }
    }
}
