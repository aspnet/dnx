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
    public class ProjectExportContext
    {
        public ApplicationHostContext ApplicationHostContext { get; set; }
        public IAssemblyLoadContext LoadContext { get; set; }
    }

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
                return GetPackageExport((PackageDescription)library);
            }
            else if (string.Equals(LibraryTypes.Project, library.Type, StringComparison.Ordinal))
            {
                return GetProjectExport((ProjectDescription)library, aspect);
            }
            else
            {
                return GetAssemblyExport(library);
            }
        }

        public LibraryExport GetProjectExport(ProjectDescription project, string aspect)
        {
            return ExportProject(project.Project, project.Framework, aspect);
        }

        public LibraryExport ExportProject(Project project, FrameworkName targetFramework, string aspect)
        {
            Logger.TraceInformation($"[{nameof(LibraryExporter)}]: {nameof(ExportProject)}({project.Name}, {aspect}, {targetFramework}, {_configuration})");

            var key = Tuple.Create(project.Name, targetFramework, _configuration, aspect);

            return _compilationEngine.CompilationCache.Cache.Get<LibraryExport>(key, ctx =>
            {
                var metadataReferences = new List<IMetadataReference>();
                var sourceReferences = new List<ISourceReference>();

                var targetFrameworkInfo = project.GetTargetFramework(targetFramework);

                // Create the compilation context
                var compilationContext = project.ToCompilationContext(targetFramework, _configuration, aspect);

                if (!string.IsNullOrEmpty(targetFrameworkInfo.AssemblyPath))
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

                    // Create the project exporter
                    var context = _compilationEngine.CreateProjectExportContext(project, targetFramework);

                    // Find the project compiler
                    var projectCompiler = _compilationEngine.GetCompiler(compilerTypeInfo, context.LoadContext);

                    Logger.TraceInformation($"[{nameof(LibraryExporter)}]: CompileProject({compilerTypeInfo.TypeName}, {project.Name}, {targetFramework}, {aspect})");

                    // Resolve the project export
                    IMetadataProjectReference projectReference = projectCompiler.CompileProject(
                        compilationContext,
                        () => ResolveReferences(context.ApplicationHostContext, aspect),
                        () => CompositeResourceProvider.Default.GetResources(project));

                    metadataReferences.Add(projectReference);

                    // Shared sources
                    foreach (var sharedFile in project.Files.SharedFiles)
                    {
                        sourceReferences.Add(new SourceFileReference(sharedFile));
                    }
                }

                return new LibraryExport(metadataReferences, sourceReferences);
            });
        }

        private LibraryExport ResolveReferences(ApplicationHostContext context, string aspect)
        {
            var dependencyStopWatch = Stopwatch.StartNew();
            Logger.TraceInformation($"[{nameof(LibraryExporter)}]: Resolving references for '{context.MainProject?.Identity.Name}' {aspect}");

            var references = new Dictionary<string, IMetadataReference>(StringComparer.OrdinalIgnoreCase);
            var sourceReferences = new Dictionary<string, ISourceReference>(StringComparer.OrdinalIgnoreCase);

            foreach (var library in context.LibraryManager.GetLibraryDescriptions())
            {
                LibraryExport libraryExport = null;

                if (library != context.MainProject)
                {
                    libraryExport = GetExport(library, aspect: null);
                }

                if (libraryExport != null)
                {
                    if (library.Parent == context.MainProject)
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

            dependencyStopWatch.Stop();
            Logger.TraceInformation($"[{nameof(LibraryExporter)}]: Resolved {references.Count} references for '{context.MainProject.Name}' in {dependencyStopWatch.ElapsedMilliseconds}ms");

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

        private LibraryExport GetPackageExport(PackageDescription package)
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

        private LibraryExport GetAssemblyExport(LibraryDescription library)
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
    }
}
