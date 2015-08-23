// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Microsoft.Dnx.Compilation.Caching;
using Microsoft.Dnx.Runtime;
using NuGet;

namespace Microsoft.Dnx.Compilation
{
    public class LibraryExporter : ILibraryExporter
    {
        private readonly CompilationEngine _compilationEngine;
        private readonly string _configuration;
        private readonly IAssemblyLoadContext _loadContext;

        public LibraryExporter(LibraryManager manager, IAssemblyLoadContext loadContext, CompilationEngine compilationEngine, string configuration)
        {
            LibraryManager = manager;
            _compilationEngine = compilationEngine;
            _configuration = configuration;
            _loadContext = loadContext;
        }

        public LibraryManager LibraryManager { get; }

        public LibraryExport GetExport(string name)
        {
            return GetExport(name, aspect: null);
        }

        public LibraryExport GetExport(string name, string aspect)
        {
            var library = LibraryManager.GetLibraryDescription(name);
            if (library == null)
            {
                return null;
            }
            return GetExport(library, aspect);
        }

        public LibraryExport GetAllExports(string name)
        {
            return GetAllExports(name, aspect: null);
        }

        public LibraryExport GetAllExports(string name, string aspect)
        {
            return GetAllExports(name, aspect, l => true);
        }

        public LibraryExport GetNonProjectExports(string name)
        {
            return GetAllExports(
                name,
                aspect: null,
                libraryFilter: l => l.Type != LibraryTypes.Project);
        }

        public LibraryExport GetAllDependencies(
            string name,
            string aspect)
        {
            return GetAllExports(name, aspect, library => !string.Equals(name, library.Identity.Name));
        }

        public LibraryExport GetAllExports(
            string name,
            string aspect,
            Func<LibraryDescription, bool> libraryFilter)
        {
            var project = LibraryManager.GetLibraryDescription(name) as ProjectDescription;
            if (project == null)
            {
                return null;
            }
            return GetAllExports(project, aspect, libraryFilter);
        }

        private LibraryExport GetAllExports(
            ProjectDescription project,
            string aspect,
            Func<LibraryDescription, bool> include)
        {
            var dependencyStopWatch = Stopwatch.StartNew();
            Logger.TraceInformation($"[{nameof(LibraryExporter)}]: Resolving references for '{project.Identity.Name}' {aspect}");

            var references = new Dictionary<string, IMetadataReference>(StringComparer.OrdinalIgnoreCase);
            var sourceReferences = new Dictionary<string, ISourceReference>(StringComparer.OrdinalIgnoreCase);

            // Walk the dependency tree and resolve the library export for all references to this project
            var queue = new Queue<Node>();
            var processed = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            var rootNode = new Node
            {
                Library = project
            };

            queue.Enqueue(rootNode);

            while (queue.Count > 0)
            {
                var node = queue.Dequeue();

                // Skip it if we've already seen it
                if (!processed.Add(node.Library.Identity.Name))
                {
                    continue;
                }

                if (include(node.Library))
                {
                    LibraryExport libraryExport = null;

                    if (node.Library == project)
                    {
                        // We know it's a project so skip the lookup
                        libraryExport = ExportProject(project, aspect: null, exporter: this);
                    }
                    else
                    {
                        libraryExport = GetExport(node.Library, aspect: null);
                    }

                    if (libraryExport != null)
                    {
                        if (node.Parent == rootNode)
                        {
                            // Only export sources from first level dependencies
                            ProcessExport(libraryExport, references, sourceReferences);
                        }
                        else
                        {
                            // Skip source exports from anything else
                            ProcessExport(libraryExport, references, sourceReferences: null);
                        }
                    }
                }

                foreach (var dependency in node.Library.Dependencies)
                {
                    var childNode = new Node
                    {
                        Library =  dependency.Library,
                        Parent = node
                    };

                    queue.Enqueue(childNode);
                }
            }

            dependencyStopWatch.Stop();
            Logger.TraceInformation($"[{nameof(LibraryExporter)}]: Resolved {references.Count} references for '{project.Identity.Name}' in {dependencyStopWatch.ElapsedMilliseconds}ms");

            return new LibraryExport(
                references.Values.ToList(),
                sourceReferences.Values.ToList());
        }

        private void ProcessExport(LibraryExport export,
                                   IDictionary<string, IMetadataReference> metadataReferences,
                                   IDictionary<string, ISourceReference> sourceReferences)
        {
            var references = new List<IMetadataReference>(export.MetadataReferences);

            foreach (var reference in references)
            {
                metadataReferences[reference.Name] = reference;
            }

            if (sourceReferences != null)
            {
                foreach (var sourceReference in export.SourceReferences)
                {
                    sourceReferences[sourceReference.Name] = sourceReference;
                }
            }
        }

        private LibraryExport GetExport(LibraryDescription library, string aspect)
        {
            // Don't even try to export unresolved libraries
            if (!library.Resolved)
            {
                return null;
            }

            if (string.Equals(LibraryTypes.Package, library.Type, StringComparison.Ordinal))
            {
                return ExportPackage((PackageDescription)library);
            }
            else if (string.Equals(LibraryTypes.Project, library.Type, StringComparison.Ordinal))
            {
                return ExportProject((ProjectDescription)library, aspect, exporter: null);
            }
            else
            {
                return ExportAssemblyLibrary(library);
            }
        }

        private LibraryExport ExportPackage(PackageDescription package)
        {
            var references = new Dictionary<string, IMetadataReference>(StringComparer.OrdinalIgnoreCase);

            PopulateMetadataReferences(package, references);

            var sourceReferences = new List<ISourceReference>();

            foreach (var sharedSource in GetSharedSources(package))
            {
                sourceReferences.Add(new SourceFileReference(sharedSource));
            }

            return new LibraryExport(references.Values.ToList(), sourceReferences);
        }

        private LibraryExport ExportProject(ProjectDescription project, string aspect, LibraryExporter exporter)
        {
            Logger.TraceInformation($"[{nameof(LibraryExporter)}]: {nameof(ExportProject)}({project.Identity.Name}, {aspect}, {project.Framework}, {_configuration})");

            var key = Tuple.Create(project.Identity.Name, project.Framework, _configuration, aspect);

            return _compilationEngine.CompilationCache.Cache.Get<LibraryExport>(key, ctx =>
            {
                var metadataReferences = new List<IMetadataReference>();
                var sourceReferences = new List<ISourceReference>();

                // Create the compilation context
                var compilationContext = project.Project.ToCompilationContext(project.Framework, _configuration, aspect);

                if (!string.IsNullOrEmpty(project.TargetFrameworkInfo.AssemblyPath))
                {
                    // Project specifies a pre-compiled binary. We're done!

                    var assemblyPath = ResolvePath(project.Project, _configuration, project.TargetFrameworkInfo.AssemblyPath);
                    var pdbPath = ResolvePath(project.Project, _configuration, project.TargetFrameworkInfo.PdbPath);

                    metadataReferences.Add(new CompiledProjectMetadataReference(compilationContext, assemblyPath, pdbPath));
                }
                else
                {
                    // We need to compile the project.
                    var compilerTypeInfo = project.Project.CompilerServices?.ProjectCompiler ?? Project.DefaultCompiler;

                    // Create the project exporter
                    exporter = exporter ?? _compilationEngine.CreateProjectExporter(project.Project, project.Framework, _configuration);

                    // Get the exports for the project dependencies
                    var projectDependenciesExport = new Lazy<LibraryExport>(() => exporter.GetAllDependencies(project.Identity.Name, aspect));

                    // Find the project compiler
                    var projectCompiler = _compilationEngine.GetCompiler(compilerTypeInfo, exporter._loadContext);

                    Logger.TraceInformation($"[{nameof(LibraryExporter)}]: GetProjectReference({compilerTypeInfo.TypeName}, {project.Identity.Name}, {project.Framework}, {aspect})");

                    // Resolve the project export
                    IMetadataProjectReference projectReference = projectCompiler.CompileProject(
                        compilationContext,
                        () => projectDependenciesExport.Value,
                        () => CompositeResourceProvider.Default.GetResources(project.Project));

                    metadataReferences.Add(projectReference);

                    // Shared sources
                    foreach (var sharedFile in project.Project.Files.SharedFiles)
                    {
                        sourceReferences.Add(new SourceFileReference(sharedFile));
                    }
                }

                return new LibraryExport(metadataReferences, sourceReferences);
            });
        }

        private static string ResolvePath(Project project, string configuration, string path)
        {
            if (string.IsNullOrEmpty(path))
            {
                return null;
            }

            path = PathUtility.GetPathWithDirectorySeparator(path);

            path = path.Replace("{configuration}", configuration);

            return Path.Combine(project.ProjectDirectory, path);
        }

        private LibraryExport ExportAssemblyLibrary(LibraryDescription library)
        {
            if (string.IsNullOrEmpty(library.Path))
            {
                Logger.TraceError($"[{nameof(LibraryExporter)}] Failed to export: {library.Identity.Name}");
                return null;
            }

            // We assume the path is to an assembly 
            return new LibraryExport(new MetadataFileReference(library.Identity.Name, library.Path));
        }

        private IEnumerable<string> GetSharedSources(PackageDescription package)
        {
            var directory = Path.Combine(package.Path, "shared");

            return package
                .Library
                .Files
                .Where(path => path.StartsWith("shared" + Path.DirectorySeparatorChar))
                .Select(path => Path.Combine(package.Path, path));
        }


        private void PopulateMetadataReferences(PackageDescription package, IDictionary<string, IMetadataReference> paths)
        {
            foreach (var assemblyPath in package.Target.CompileTimeAssemblies)
            {
                if (PackageDependencyProvider.IsPlaceholderFile(assemblyPath))
                {
                    continue;
                }

                var name = Path.GetFileNameWithoutExtension(assemblyPath);
                var path = Path.Combine(package.Path, assemblyPath);
                paths[name] = new MetadataFileReference(name, path);
            }
        }

        private class Node
        {
            public LibraryDescription Library { get; set; }

            public Node Parent { get; set; }
        }

    }
}
