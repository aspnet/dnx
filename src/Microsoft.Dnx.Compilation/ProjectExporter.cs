// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.Versioning;
using Microsoft.Dnx.Compilation.Caching;
using Microsoft.Dnx.Runtime;
using Microsoft.Dnx.Runtime.Infrastructure;

namespace Microsoft.Dnx.Compilation
{
    public static class ProjectExporter
    {
        public static LibraryExport ExportProject(Project project, CompilationEngine compilationEngine, string aspect, FrameworkName targetFramework, string configuration)
        {
            Logger.TraceInformation($"[{nameof(ProjectExporter)}]: {nameof(ExportProject)}({project.Name}, {aspect}, {targetFramework}, {configuration})");

            var targetFrameworkInformation = project.GetTargetFramework(targetFramework);

            // This is the target framework defined in the project. If there were no target frameworks
            // defined then this is the targetFramework specified
            if (targetFrameworkInformation.FrameworkName != null)
            {
                targetFramework = targetFrameworkInformation.FrameworkName;
            }

            var key = Tuple.Create(project.Name, targetFramework, configuration, aspect);

            return compilationEngine.CompilationCache.Cache.Get<LibraryExport>(key, ctx =>
            {
                var metadataReferences = new List<IMetadataReference>();
                var sourceReferences = new List<ISourceReference>();

                if (!string.IsNullOrEmpty(targetFrameworkInformation.AssemblyPath))
                {
                    // Project specifies a pre-compiled binary. We're done!

                    var assemblyPath = ResolvePath(project, configuration, targetFrameworkInformation.AssemblyPath);
                    var pdbPath = ResolvePath(project, configuration, targetFrameworkInformation.PdbPath);

                    metadataReferences.Add(new CompiledProjectMetadataReference(project.ToCompilationContext(targetFramework, configuration, aspect), assemblyPath, pdbPath));
                }
                else
                {
                    // We need to compile the project.

                    var provider = project.CompilerServices?.ProjectCompiler ?? Project.DefaultCompiler;

                    // Find the project compiler
                    var projectCompiler = compilationEngine.GetCompiler(provider);

                    Logger.TraceInformation("[{0}]: GetProjectReference({1}, {2}, {3}, {4})", provider.TypeName, project.Name, targetFramework, configuration, aspect);

                    // Get the exports for the project dependencies
                    var projectExport = new Lazy<LibraryExport>(() => ExportProjectDependencies(project, targetFramework, configuration, aspect, compilationEngine)); 

                    // Resolve the project export
                    IMetadataProjectReference projectReference = projectCompiler.CompileProject(
                        project.ToCompilationContext(targetFramework, configuration, aspect),
                        () => projectExport.Value,
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

        private static LibraryExport ExportProjectDependencies(Project project, FrameworkName targetFramework, string configuration, string aspect, CompilationEngine compilationEngine)
        {
            return compilationEngine
                .CreateProjectExporter(project, targetFramework, configuration)
                .GetAllDependencies(project.Name, aspect);
        }

        private static string ResolvePath(Project project, string configuration, string path)
        {
            if (string.IsNullOrEmpty(path))
            {
                return null;
            }

            if (Path.DirectorySeparatorChar == '/')
            {
                path = path.Replace('\\', Path.DirectorySeparatorChar);
            }
            else
            {
                path = path.Replace('/', Path.DirectorySeparatorChar);
            }

            path = path.Replace("{configuration}", configuration);

            return Path.Combine(project.ProjectDirectory, path);
        }
    }
}
