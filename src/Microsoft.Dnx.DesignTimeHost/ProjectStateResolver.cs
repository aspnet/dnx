// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Versioning;
using Microsoft.Dnx.Compilation;
using Microsoft.Dnx.Compilation.Caching;
using Microsoft.Dnx.Compilation.CSharp;
using Microsoft.Dnx.DesignTimeHost.InternalModels;
using Microsoft.Dnx.DesignTimeHost.Models.OutgoingMessages;
using Microsoft.Dnx.Runtime;
using Microsoft.Dnx.Runtime.Common.Impl;
using Microsoft.Dnx.Runtime.Internals;
using Microsoft.Extensions.CompilationAbstractions;
using Microsoft.Extensions.CompilationAbstractions.Caching;
using NuGet;

namespace Microsoft.Dnx.DesignTimeHost
{
    internal class ProjectStateResolver
    {
        private readonly CompilationEngine _compilationEngine;
        private readonly FrameworkReferenceResolver _frameworkReferenceResolver;
        private readonly Func<CacheContext, Project, FrameworkName, ApplicationHostContext> _applicationHostContextCreator;

        public ProjectStateResolver(CompilationEngine compilationEngine,
                                    FrameworkReferenceResolver frameworkReferenceResolver,
                                    Func<CacheContext, Project, FrameworkName, ApplicationHostContext> applicaitonHostContextCreator)
        {
            _compilationEngine = compilationEngine;
            _frameworkReferenceResolver = frameworkReferenceResolver;
            _applicationHostContextCreator = applicaitonHostContextCreator;
        }

        public ProjectState Resolve(string appPath,
                                    string configuration,
                                    bool triggerBuildOutputs,
                                    bool triggerDependencies,
                                    int protocolVersion,
                                    IList<string> currentSearchPaths)
        {
            var state = new ProjectState
            {
                Frameworks = new List<FrameworkData>(),
                Projects = new List<ProjectInfo>(),
                Diagnostics = new List<DiagnosticMessage>()
            };

            Project project;
            if (!Project.TryGetProject(appPath, out project, state.Diagnostics))
            {
                throw new InvalidOperationException($"Unable to find project.json in '{appPath}'");
            }

            if (triggerBuildOutputs)
            {
                // Trigger the build outputs for this project
                _compilationEngine.CompilationCache.NamedCacheDependencyProvider.Trigger(project.Name + "_BuildOutputs");
            }

            if (triggerDependencies)
            {
                _compilationEngine.CompilationCache.NamedCacheDependencyProvider.Trigger(project.Name + "_Dependencies");
            }

            state.Name = project.Name;
            state.Project = project;
            state.Configurations = project.GetConfigurations().ToList();
            state.Commands = project.Commands;

            var frameworks = new List<FrameworkName>(project.GetTargetFrameworks().Select(tf => tf.FrameworkName));

            if (!frameworks.Any())
            {
                frameworks.Add(VersionUtility.ParseFrameworkName(FrameworkNames.ShortNames.Dnx451));
            }

            var sourcesProjectWidesources = project.Files.SourceFiles.ToList();

            ResolveSearchPaths(state);

            foreach (var frameworkName in frameworks)
            {
                var dependencyInfo = ResolveProjectDependencies(project,
                                                                configuration,
                                                                frameworkName,
                                                                protocolVersion,
                                                                GetUpdatedSearchPaths(currentSearchPaths, state.ProjectSearchPaths));

                var dependencySources = new List<string>(sourcesProjectWidesources);

                var frameworkData = new FrameworkData
                {
                    ShortName = VersionUtility.GetShortFrameworkName(frameworkName),
                    FrameworkName = frameworkName.ToString(),
                    FriendlyName = _frameworkReferenceResolver.GetFriendlyFrameworkName(frameworkName),
                    RedistListPath = _frameworkReferenceResolver.GetFrameworkRedistListPath(frameworkName)
                };

                state.Frameworks.Add(frameworkData);

                // Add shared files from packages
                dependencySources.AddRange(dependencyInfo.ExportedSourcesFiles);

                // Add shared files from projects
                foreach (var reference in dependencyInfo.ProjectReferences)
                {
                    if (reference.Project == null)
                    {
                        continue;
                    }

                    // Only add direct dependencies as sources
                    if (!project.Dependencies.Any(d => string.Equals(d.Name, reference.Name, StringComparison.OrdinalIgnoreCase)))
                    {
                        continue;
                    }

                    dependencySources.AddRange(reference.Project.Files.SharedFiles);
                }

                var projectInfo = new ProjectInfo
                {
                    Path = appPath,
                    Configuration = configuration,
                    TargetFramework = frameworkData,
                    FrameworkName = frameworkName,
                    // TODO: This shouldn't be Roslyn specific compilation options
                    CompilationSettings = project.GetCompilerOptions(frameworkName, configuration)
                                                 .ToCompilationSettings(frameworkName, project.ProjectDirectory),
                    SourceFiles = dependencySources,
                    DependencyInfo = dependencyInfo
                };

                state.Projects.Add(projectInfo);
            }

            return state;
        }

        private DependencyInfo ResolveProjectDependencies(Project project,
                                                          string configuration,
                                                          FrameworkName frameworkName,
                                                          int protocolVersion,
                                                          List<string> updatedSearchPath)
        {
            var cacheKey = Tuple.Create("DependencyInfo", project.Name, configuration, frameworkName);

            return _compilationEngine.CompilationCache.Cache.Get<DependencyInfo>(cacheKey, ctx =>
            {
                var applicationHostContext = _applicationHostContextCreator(ctx, project, frameworkName);
                var libraryManager = applicationHostContext.LibraryManager;
                var libraryExporter = _compilationEngine.CreateProjectExporter(project, frameworkName, configuration);

                var info = new DependencyInfo
                {
                    Dependencies = new Dictionary<string, DependencyDescription>(),
                    ProjectReferences = new List<ProjectReference>(),
                    References = new List<string>(),
                    RawReferences = new Dictionary<string, byte[]>(),
                    ExportedSourcesFiles = new List<string>(),
                    Diagnostics = libraryManager.GetAllDiagnostics().ToList()
                };

                var diagnosticSources = info.Diagnostics.ToLookup(diagnostic => diagnostic.Source);
                var projectCandiates = GetProjectCandidates(updatedSearchPath);

                foreach (var library in applicationHostContext.LibraryManager.GetLibraryDescriptions())
                {
                    var diagnostics = diagnosticSources[library].ToList();

                    var newDiagnostic = ValidateDependency(library, projectCandiates);
                    if (newDiagnostic != null)
                    {
                        info.Diagnostics.Add(newDiagnostic);
                        diagnostics.Add(newDiagnostic);
                    }

                    var description = CreateDependencyDescription(library, diagnostics, protocolVersion);

                    info.Dependencies[description.Name] = description;

                    if (string.Equals(library.Type, LibraryTypes.Project) &&
                       !string.Equals(library.Identity.Name, project.Name))
                    {
                        var referencedProject = (ProjectDescription)library;

                        var targetFrameworkInformation = referencedProject.TargetFrameworkInfo;

                        // If this is an assembly reference then treat it like a file reference
                        if (!string.IsNullOrEmpty(targetFrameworkInformation?.AssemblyPath) &&
                             string.IsNullOrEmpty(targetFrameworkInformation?.WrappedProject))
                        {
                            string assemblyPath = GetProjectRelativeFullPath(referencedProject.Project,
                                                                             targetFrameworkInformation.AssemblyPath);
                            info.References.Add(assemblyPath);

                            description.Path = assemblyPath;
                            description.Type = "Assembly";
                        }
                        else
                        {
                            string wrappedProjectPath = null;

                            if (!string.IsNullOrEmpty(targetFrameworkInformation?.WrappedProject) &&
                                referencedProject.Project != null)
                            {
                                wrappedProjectPath = GetProjectRelativeFullPath(referencedProject.Project, targetFrameworkInformation.WrappedProject);
                            }

                            info.ProjectReferences.Add(new ProjectReference
                            {
                                Name = referencedProject.Identity.Name,
                                Framework = new FrameworkData
                                {
                                    ShortName = VersionUtility.GetShortFrameworkName(library.Framework),
                                    FrameworkName = library.Framework.ToString(),
                                    FriendlyName = _frameworkReferenceResolver.GetFriendlyFrameworkName(library.Framework)
                                },
                                Path = library.Path,
                                WrappedProjectPath = wrappedProjectPath,
                                Project = referencedProject.Project
                            });
                        }
                    }
                }

                var exportWithoutProjects = libraryExporter.GetNonProjectExports(project.Name);

                foreach (var reference in exportWithoutProjects.MetadataReferences)
                {
                    var fileReference = reference as IMetadataFileReference;
                    if (fileReference != null)
                    {
                        info.References.Add(fileReference.Path);
                    }

                    var embedded = reference as IMetadataEmbeddedReference;
                    if (embedded != null)
                    {
                        info.RawReferences[embedded.Name] = embedded.Contents;
                    }
                }

                foreach (var sourceFileReference in exportWithoutProjects.SourceReferences.OfType<ISourceFileReference>())
                {
                    info.ExportedSourcesFiles.Add(sourceFileReference.Path);
                }

                return info;
            });
        }

        private DiagnosticMessage ValidateDependency(LibraryDescription library, HashSet<string> projectCandidates)
        {
            if (!library.Resolved || projectCandidates == null)
            {
                return null;
            }

            var foundCandidate = projectCandidates.Contains(library.Identity.Name);

            if ((library.Type == LibraryTypes.Project && !foundCandidate) ||
                (library.Type == LibraryTypes.Package && foundCandidate))
            {
                library.Resolved = false;
                library.Type = LibraryTypes.Unresolved;

                return new DiagnosticMessage(
                    DiagnosticMonikers.NU1010,
                    $"The type of dependency {library.Identity.Name} was changed.",
                    library.RequestedRange.FileName,
                    DiagnosticMessageSeverity.Error,
                    library.RequestedRange.Line,
                    library.RequestedRange.Column,
                    library);
            }

            return null;
        }

        private static HashSet<string> GetProjectCandidates(IEnumerable<string> searchPaths)
        {
            if (searchPaths == null)
            {
                return null;
            }

            return new HashSet<string>(searchPaths.Where(path => Directory.Exists(path))
                                                  .SelectMany(path => Directory.GetDirectories(path))
                                                  .Where(path => File.Exists(Path.Combine(path, Project.ProjectFileName)))
                                                  .Select(path => Path.GetFileName(path)));
        }

        private static void ResolveSearchPaths(ProjectState state)
        {
            GlobalSettings settings = null;

            if (state.GlobalJsonPath == null)
            {
                var root = ProjectRootResolver.ResolveRootDirectory(state.Project.ProjectDirectory);

                if (GlobalSettings.TryGetGlobalSettings(root, out settings))
                {
                    state.GlobalJsonPath = settings.FilePath;
                }
            }

            if (state.ProjectSearchPaths == null)
            {
                var searchPaths = new HashSet<string>()
                    {
                        Directory.GetParent(state.Project.ProjectDirectory).FullName
                    };

                if (settings != null)
                {
                    foreach (var searchPath in settings.ProjectSearchPaths)
                    {
                        var path = Path.Combine(settings.DirectoryPath, searchPath);
                        searchPaths.Add(Path.GetFullPath(path));
                    }
                }

                state.ProjectSearchPaths = searchPaths.ToList();
            }
        }

        private static DependencyDescription CreateDependencyDescription(LibraryDescription library,
                                                                         IEnumerable<DiagnosticMessage> diagnostics,
                                                                         int protocolVersion)
        {
            var result = new DependencyDescription
            {
                Name = library.Identity.Name,
                DisplayName = library.Identity.IsGacOrFrameworkReference ? library.RequestedRange.GetReferenceAssemblyName() : library.Identity.Name,
                Version = library.Identity.Version?.ToString(),
                Type = library.Type,
                Resolved = library.Resolved,
                Path = library.Path,
                Dependencies = library.Dependencies.Select(dependency => new DependencyItem
                {
                    Name = dependency.Name,
                    Version = dependency.Library?.Identity?.Version?.ToString()
                }),
                Errors = diagnostics.Where(d => d.Severity == DiagnosticMessageSeverity.Error)
                                    .Select(d => new DiagnosticMessageView(d)),
                Warnings = diagnostics.Where(d => d.Severity == DiagnosticMessageSeverity.Warning)
                                      .Select(d => new DiagnosticMessageView(d))
            };

            if (protocolVersion < 3 && !library.Resolved)
            {
                result.Type = "Unresolved";
            }

            return result;
        }

        private static string GetProjectRelativeFullPath(Project referencedProject, string path)
        {
            return Path.GetFullPath(Path.Combine(referencedProject.ProjectDirectory, path));
        }

        /// <summary>
        /// Returns the search paths if they're updated. Otherwise returns null.
        /// </summary>
        private static List<string> GetUpdatedSearchPaths(IList<string> oldSearchPaths, List<string> newSearchPaths)
        {
            // The oldSearchPaths is null when the current project is not initialized. It is not necessary to 
            // validate the dependency in this case.
            if (oldSearchPaths == null)
            {
                return null;
            }

            if (Enumerable.SequenceEqual(oldSearchPaths, newSearchPaths))
            {
                return null;
            }

            return newSearchPaths;
        }
    }
}
