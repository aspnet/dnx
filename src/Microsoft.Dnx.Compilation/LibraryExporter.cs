// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.Versioning;
using Microsoft.Dnx.Compilation.Caching;
using Microsoft.Dnx.Runtime;
using NuGet;

namespace Microsoft.Dnx.Compilation
{
    public class LibraryExporter
    {
        private readonly CompilationEngine _compilationEngine;
        private readonly string _configuration;

        public LibraryExporter(CompilationEngine compilationEngine, string configuration)
        {
            _compilationEngine = compilationEngine;
            _configuration = configuration;
        }

        public LibraryExport GetExport(LibraryDescription library)
        {
            return GetExport(library, aspect: null);
        }

        public LibraryExport GetExport(LibraryDescription library, string aspect)
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
                return ExportProject((ProjectDescription)library, aspect);
            }
            else
            {
                return ExportAssemblyLibrary(library);
            }
        }

        public ProjectExport ExportProject(Project project, FrameworkName targetFramework, string aspect = null)
        {
            return ExportProject(project, targetFramework, project.GetTargetFramework(targetFramework), aspect);
        }

        public LibraryExport GetAllExports(
            LibraryDescription root,
            string aspect,
            Func<LibraryDescription, bool> include)
        {
            var dependencyStopWatch = Stopwatch.StartNew();
            Logger.TraceInformation($"[{nameof(LibraryExporter)}]: Resolving references for '{root.Identity.Name}' {aspect}");

            var references = new Dictionary<string, IMetadataReference>(StringComparer.OrdinalIgnoreCase);
            var sourceReferences = new Dictionary<string, ISourceReference>(StringComparer.OrdinalIgnoreCase);

            // Walk the dependency tree and resolve the library export for all references to this project
            var queue = new Queue<Node>();
            var processed = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            var rootNode = new Node
            {
                Library = root
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
                    var libraryExport = GetExport(node.Library, aspect: null);

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
                        Library = dependency.Library,
                        Parent = node
                    };

                    queue.Enqueue(childNode);
                }
            }

            dependencyStopWatch.Stop();
            Logger.TraceInformation($"[{nameof(LibraryExporter)}]: Resolved {references.Count} references for '{root.Identity.Name}' in {dependencyStopWatch.ElapsedMilliseconds}ms");

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

        private ProjectExport ExportProject(ProjectDescription project, string aspect)
        {
            return ExportProject(project.Project, project.Framework, project.TargetFrameworkInfo, aspect);
        }

        private ProjectExport ExportProject(Project project, FrameworkName targetFramework, TargetFrameworkInformation targetFrameworkInfo, string aspect)
        {
            Logger.TraceInformation($"[{nameof(LibraryExporter)}]: {nameof(ExportProject)}({project.Name}, {aspect}, {targetFramework}, {_configuration})");

            var key = Tuple.Create(project.Name, targetFramework, _configuration, aspect);

            return _compilationEngine.CompilationCache.Cache.Get<ProjectExport>(key, ctx =>
            {
                var metadataReferences = new List<IMetadataReference>();
                var sourceReferences = new List<ISourceReference>();
                var projectExport = new ProjectExport(this, project, targetFramework, metadataReferences, sourceReferences);

                // Create the compilation context
                var compilationContext = project.ToCompilationContext(targetFramework, _configuration, aspect);

                if (!string.IsNullOrEmpty(targetFrameworkInfo?.AssemblyPath))
                {
                    // Project specifies a pre-compiled binary. We're done!
                    var assemblyPath = ResolvePath(project, _configuration, targetFrameworkInfo.AssemblyPath);
                    var pdbPath = ResolvePath(project, _configuration, targetFrameworkInfo.PdbPath);

                    metadataReferences.Add(new CompiledProjectMetadataReference(compilationContext, assemblyPath, pdbPath));
                }
                else
                {
                    // We need to compile the project.
                    var compilerTypeInfo = project.CompilerServices?.ProjectCompiler ?? Project.DefaultCompiler;

                    // Find the project compiler
                    var projectCompiler = _compilationEngine.GetCompiler(compilerTypeInfo, projectExport.LoadContext);

                    Logger.TraceInformation($"[{nameof(LibraryExporter)}]: {compilerTypeInfo.TypeName}.CompileProject({project.Name}, {targetFramework}, {aspect})");

                    // Resolve the project export
                    IMetadataProjectReference projectReference = projectCompiler.CompileProject(
                        compilationContext,
                        () => projectExport.DependenciesExport,
                        () => CompositeResourceProvider.Default.GetResources(project));

                    metadataReferences.Add(projectReference);

                    // Shared sources
                    foreach (var sharedFile in project.Files.SharedFiles)
                    {
                        sourceReferences.Add(new SourceFileReference(sharedFile));
                    }
                }

                return projectExport;
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
