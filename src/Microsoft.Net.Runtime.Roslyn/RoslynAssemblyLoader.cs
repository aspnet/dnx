using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Versioning;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Emit;
using Microsoft.Net.Runtime.FileSystem;
using NuGet;
using Microsoft.Net.Runtime.Loader;
using Microsoft.Net.Runtime.Roslyn.AssemblyNeutral;

#if NET45 // TODO: Temporary due to CoreCLR and Desktop Roslyn being out of sync
using EmitResult = Microsoft.CodeAnalysis.Emit.CommonEmitResult;
#endif

namespace Microsoft.Net.Runtime.Roslyn
{
    public class RoslynAssemblyLoader : IAssemblyLoader, IPackageLoader, IDependencyExportResolver
    {
        private readonly Dictionary<string, CompilationContext> _compilationCache = new Dictionary<string, CompilationContext>();

        private readonly IProjectResolver _projectResolver;
        private readonly IFileWatcher _watcher;
        private readonly IFrameworkReferenceResolver _resolver;
        private readonly IGlobalAssemblyCache _globalAssemblyCache;
        private readonly IDependencyExportResolver _dependencyResolver;
        private readonly IResourceProvider _resourceProvider;

        private IEnumerable<DependencyDescription> _packages;

        public RoslynAssemblyLoader(IProjectResolver projectResolver,
                                    IFileWatcher watcher,
                                    IDependencyExportResolver dependencyResolver)
        {
            _projectResolver = projectResolver;
            _watcher = watcher;
            _globalAssemblyCache = new DefaultGlobalAssemblyCache();
            _resolver = new FrameworkReferenceResolver(_globalAssemblyCache);
            _dependencyResolver = dependencyResolver;
            _resourceProvider = new ResxResourceProvider();
        }

        public RoslynAssemblyLoader(IProjectResolver projectResolver,
                                    IFileWatcher watcher,
                                    IFrameworkReferenceResolver resolver,
                                    IGlobalAssemblyCache globalAssemblyCache,
                                    IDependencyExportResolver dependencyLoader,
                                    IResourceProvider resourceProvider)
        {
            _projectResolver = projectResolver;
            _watcher = watcher;
            _resolver = resolver;
            _globalAssemblyCache = globalAssemblyCache;
            _dependencyResolver = dependencyLoader;
            _resourceProvider = resourceProvider;
        }

        public AssemblyLoadResult Load(LoadContext loadContext)
        {
            var name = loadContext.AssemblyName;

            Project project;
            // Can't find a project file with the name so bail
            if (!_projectResolver.TryResolveProject(name, out project))
            {
                return null;
            }

            var path = project.ProjectDirectory;

            var compilationContext = GetCompilationContext(loadContext.AssemblyName, loadContext.TargetFramework);

            var resources = _resourceProvider.GetResources(project.Name, path);

            foreach (var typeContext in compilationContext.SuccessfulTypeCompilationContexts)
            {
                resources.Add(new ResourceDescription(typeContext.AssemblyName + ".dll",
                                                      () => typeContext.OutputStream,
                                                      isPublic: true));
            }

            // If the output path is null then load the assembly from memory
            if (loadContext.OutputPath == null)
            {
                return CompileInMemory(name, compilationContext, resources);
            }

            var assemblyPath = Path.Combine(loadContext.OutputPath, name + ".dll");
            var pdbPath = Path.Combine(loadContext.OutputPath, name + ".pdb");

            // Add to the list of generated artifacts
            if (loadContext.ArtifactPaths != null)
            {
                loadContext.ArtifactPaths.Add(assemblyPath);
                loadContext.ArtifactPaths.Add(pdbPath);
            }

            var loadResult = CompileToDisk(loadContext.OutputPath, assemblyPath, pdbPath, compilationContext, resources);

            if (loadResult != null && loadResult.Errors == null)
            {
                // Attempt to build packages for this project
                // BuildPackages(loadContext, project, assemblyPath, pdbPath, sourceFiles);

                if (loadContext.PackageBuilder != null)
                {
                    BuildPackage(project, assemblyPath, loadContext.PackageBuilder, loadContext.TargetFramework);
                }
            }

            return loadResult;

        }

        private CompilationContext GetCompilationContext(string name, FrameworkName targetFramework)
        {
            CompilationContext compilationContext;
            if (_compilationCache.TryGetValue(name, out compilationContext))
            {
                return compilationContext;
            }

            Project project;
            // Can't find a project file with the name so bail
            if (!_projectResolver.TryResolveProject(name, out project))
            {
                return null;
            }

            string path = project.ProjectDirectory;

            _watcher.WatchFile(project.ProjectFilePath);

            TargetFrameworkConfiguration targetFrameworkConfig = project.GetTargetFrameworkConfiguration(targetFramework);

            Trace.TraceInformation("[{0}]: Found project '{1}' framework={2}", GetType().Name, project.Name, targetFramework);

            var references = new List<MetadataReference>();
            var exports = new List<DependencyExport>();
            var failedCompilations = new List<EmitResult>();

            if (project.Dependencies.Count > 0 ||
                targetFrameworkConfig.Dependencies.Count > 0)
            {
                Trace.TraceInformation("[{0}]: Loading dependencies for '{1}'", GetType().Name, project.Name);
                var dependencyStopWatch = Stopwatch.StartNew();

                foreach (var dependency in project.Dependencies.Concat(targetFrameworkConfig.Dependencies))
                {
                    DependencyExport dependencyExport = _dependencyResolver.GetDependencyExport(dependency.Name, targetFramework);

                    // Determine if this dependency has any failed compilations
                    CompilationContext dependencyContext;
                    if (_compilationCache.TryGetValue(dependency.Name, out dependencyContext))
                    {
                        failedCompilations.AddRange(dependencyContext.FailedCompilations);
                    }

                    if (dependencyExport != null)
                    {
                        exports.Add(dependencyExport);
                    }
                }

                dependencyStopWatch.Stop();
                Trace.TraceInformation("[{0}]: Completed loading dependencies for '{1}' in {2}ms", GetType().Name, project.Name, dependencyStopWatch.ElapsedMilliseconds);
            }

            IDictionary<string, MetadataReference> assemblyNeutralReferences;
            IList<MetadataReference> exportedReferences;

            ExtractReferences(exports, out exportedReferences, out assemblyNeutralReferences);

            references.AddRange(exportedReferences);
            references.AddRange(_resolver.GetDefaultReferences(targetFramework));

            Trace.TraceInformation("[{0}]: Compiling '{1}'", GetType().Name, name);
            var sw = Stopwatch.StartNew();

            _watcher.WatchDirectory(path, ".cs");

            var trees = new List<SyntaxTree>();

            var hasAssemblyInfo = false;

            var sourceFiles = project.SourceFiles.ToList();

            var compilationSettings = project.GetCompilationSettings(targetFramework);

            var parseOptions = new CSharpParseOptions(preprocessorSymbols: compilationSettings.Defines.AsImmutable());

            foreach (var sourcePath in sourceFiles)
            {
                if (!hasAssemblyInfo && Path.GetFileNameWithoutExtension(sourcePath).Equals("AssemblyInfo"))
                {
                    hasAssemblyInfo = true;
                }

                _watcher.WatchFile(sourcePath);
                trees.Add(CSharpSyntaxTree.ParseFile(sourcePath, parseOptions));
            }

            if (!hasAssemblyInfo)
            {
                trees.Add(CSharpSyntaxTree.ParseText("[assembly: System.Reflection.AssemblyVersion(\"" + project.Version.Version + "\")]"));
                trees.Add(CSharpSyntaxTree.ParseText("[assembly: System.Reflection.AssemblyInformationalVersion(\"" + project.Version + "\")]"));
            }

            foreach (var directory in Directory.EnumerateDirectories(path, "*.*", SearchOption.AllDirectories))
            {
                _watcher.WatchDirectory(directory, ".cs");
            }

            var compilation = CSharpCompilation.Create(
                name,
                compilationSettings.CompilationOptions,
                syntaxTrees: trees,
                references: references);

            var context = new CompilationContext
            {
                OriginalCompilation = compilation,
                Project = project
            };

            context.FindTypeCompilations(compilation.GlobalNamespace);
            context.OrderTypeCompilations();
            context.GenerateTypeCompilations();

            // REVIEW: Is the order here correct?
            context.FailedCompilations.AddRange(failedCompilations);

            context.Generate(assemblyNeutralReferences);

            context.Compilation = context.Compilation.WithReferences(
                context.Compilation.References.Concat(assemblyNeutralReferences.Values));

            // Cache the compilation
            _compilationCache[name] = context;

            return context;
        }

        private void ExtractReferences(List<DependencyExport> dependencyExports,
                                       out IList<MetadataReference> references,
                                       out IDictionary<string, MetadataReference> assemblyNeutralReferences)
        {
            var paths = new HashSet<string>();
            references = new List<MetadataReference>();
            assemblyNeutralReferences = new Dictionary<string, MetadataReference>();

            foreach (var export in dependencyExports)
            {
                foreach (var reference in export.MetadataReferences)
                {
                    var fileMetadataReference = reference as IMetadataFileReference;

                    if (fileMetadataReference != null)
                    {
                        // Make sure nothing is duped
                        paths.Add(fileMetadataReference.Path);
                    }
                    else
                    {
                        var assemblyNeutralReference = reference as AssemblyNeutralMetadataReference;

                        if (assemblyNeutralReference != null)
                        {
                            assemblyNeutralReferences[assemblyNeutralReference.Name] = assemblyNeutralReference.MetadataReference;
                        }
                        else
                        {
                            references.Add(ConvertMetadataReference(reference));
                        }
                    }
                }
            }

            // Now add the final set to the final set of references
            references.AddRange(paths.Select(path => new MetadataFileReference(path)));
        }

        private MetadataReference ConvertMetadataReference(IMetadataReference metadataReference)
        {
            var fileMetadataReference = metadataReference as IMetadataFileReference;

            if (fileMetadataReference != null)
            {
                return new MetadataFileReference(fileMetadataReference.Path);
            }

            var metadataReferenceWrapper = metadataReference as MetadataReferenceWrapper;

            if (metadataReferenceWrapper != null)
            {
                return metadataReferenceWrapper.MetadataReference;
            }

            throw new NotSupportedException();
        }

        public DependencyDescription GetDescription(string name, SemanticVersion version, FrameworkName targetFramework)
        {
            Project project;

            // Can't find a project file with the name so bail
            if (!_projectResolver.TryResolveProject(name, out project))
            {
                return null;
            }
            else if (version != null && !project.Version.EqualsSnapshot(version))
            {
                return null;
            }

            var config = project.GetTargetFrameworkConfiguration(targetFramework);

            return new DependencyDescription
            {
                Identity = new Dependency { Name = project.Name, Version = project.Version },
                Dependencies = project.Dependencies.Concat(config.Dependencies),
            };
        }

        public void Initialize(IEnumerable<DependencyDescription> packages, FrameworkName targetFramework)
        {
            _packages = packages;
        }

        public DependencyExport GetDependencyExport(string name, FrameworkName targetFramework)
        {
            string assemblyLocation;
            if (_globalAssemblyCache.TryResolvePartialName(name, out assemblyLocation))
            {
                return CreateDependencyExport(assemblyLocation);
            }

            var compilationContext = GetCompilationContext(name, targetFramework);

            if (compilationContext == null)
            {
                return null;
            }

            var references = new List<IMetadataReference>();
            var metadataReference = compilationContext.Compilation.ToMetadataReference(embedInteropTypes: compilationContext.Project.EmbedInteropTypes);
            references.Add(new MetadataReferenceWrapper(metadataReference));

            foreach (var typeContext in compilationContext.SuccessfulTypeCompilationContexts)
            {
                references.Add(new AssemblyNeutralMetadataReference(typeContext.AssemblyName, typeContext.RealOrShallowReference()));
            }

            return new DependencyExport(references);
        }

        private AssemblyLoadResult CompileToDisk(string outputPath, string assemblyPath, string pdbPath, CompilationContext compilationContext, IList<ResourceDescription> resources)
        {
            // REVIEW: Memory bloat?
            using (var pdbStream = new MemoryStream())
            using (var assemblyStream = new MemoryStream())
            {
#if NET45
                EmitResult result = compilationContext.Compilation.Emit(assemblyStream, outputName: Path.GetFileName(assemblyPath), pdbFileName: pdbPath, pdbStream: pdbStream, manifestResources: resources);
#else
                EmitResult result = compilationContext.Compilation.Emit(assemblyStream);
#endif

                if (!result.Success)
                {
                    return ReportCompilationError(compilationContext.FailedCompilations.Concat(new[] { result }));
                }

                if (compilationContext.FailedCompilations.Count > 0)
                {
                    return ReportCompilationError(compilationContext.FailedCompilations);
                }

                // Ensure there's an output directory
                Directory.CreateDirectory(outputPath);

                assemblyStream.Position = 0;
                pdbStream.Position = 0;

                using (var pdbFileStream = File.Create(pdbPath))
                using (var assemblyFileStream = File.Create(assemblyPath))
                {
                    assemblyStream.CopyTo(assemblyFileStream);
                    pdbStream.CopyTo(pdbFileStream);
                }

                // Valid result but we don't have to load it
                return new AssemblyLoadResult();
            }
        }

        private AssemblyLoadResult CompileInMemory(string name, CompilationContext compilationContext, IEnumerable<ResourceDescription> resources)
        {
            using (var pdbStream = new MemoryStream())
            using (var assemblyStream = new MemoryStream())
            {
#if NET45
                EmitResult result = compilationContext.Compilation.Emit(assemblyStream, pdbStream: pdbStream, manifestResources: resources);
#else
                EmitResult result = compilationContext.Compilation.Emit(assemblyStream);
#endif

                if (!result.Success)
                {
                    return ReportCompilationError(compilationContext.FailedCompilations.Concat(new[] { result }));
                }

                if (compilationContext.FailedCompilations.Count > 0)
                {
                    return ReportCompilationError(compilationContext.FailedCompilations);
                }

                var assemblyBytes = assemblyStream.ToArray();

#if NET45
                var pdbBytes = pdbStream.ToArray();
#endif

#if NET45
                var assembly = Assembly.Load(assemblyBytes, pdbBytes);
#else
                var assembly = Assembly.Load(assemblyBytes);
#endif
                return new AssemblyLoadResult(assembly);
            }
        }

        private void BuildPackages(LoadContext loadContext, Project project, string assemblyPath, string pdbPath, List<string> sourceFiles)
        {
            // Build packages
            if (loadContext.PackageBuilder != null)
            {
                BuildPackage(project, assemblyPath, loadContext.PackageBuilder, loadContext.TargetFramework);
            }

            if (loadContext.SymbolPackageBuilder != null)
            {
                BuildSymbolsPackage(project, assemblyPath, pdbPath, loadContext.SymbolPackageBuilder, sourceFiles, loadContext.TargetFramework);
            }
        }

        private static DependencyExport CreateDependencyExport(MetadataReference metadataReference)
        {
            return new DependencyExport(new MetadataReferenceWrapper(metadataReference));
        }

        private static DependencyExport CreateDependencyExport(string assemblyLocation)
        {
            return CreateDependencyExport(new MetadataFileReference(assemblyLocation));
        }

        private static AssemblyLoadResult ReportCompilationError(IEnumerable<EmitResult> results)
        {
            return new AssemblyLoadResult(GetErrors(results));
        }

        private static List<string> GetErrors(IEnumerable<EmitResult> results)
        {
#if NET45 // TODO: Temporary due to CoreCLR and Desktop Roslyn being out of sync
            var formatter = DiagnosticFormatter.Instance;
#else
            var formatter = new DiagnosticFormatter();
#endif
            var errors = new List<string>(results.SelectMany(r => r.Diagnostics.Select(d => formatter.Format(d))));

            return errors;
        }

        private void BuildSymbolsPackage(Project project, string assemblyPath, string pdbPath, PackageBuilder builder, List<string> sourceFiles, FrameworkName targetFramework)
        {
            // TODO: Build symbols packages
        }

        private void BuildPackage(Project project, string assemblyPath, PackageBuilder builder, FrameworkName targetFramework)
        {
            var framework = targetFramework;
            var dependencies = new List<PackageDependency>();

            var frameworkReferences = new HashSet<string>(_resolver.GetFrameworkReferences(framework), StringComparer.OrdinalIgnoreCase);
            var frameworkAssemblies = new List<string>();

            if (project.Dependencies.Count > 0)
            {
                foreach (var dependency in project.Dependencies)
                {
                    Project dependencyProject;
                    if (_projectResolver.TryResolveProject(dependency.Name, out dependencyProject) && dependencyProject.EmbedInteropTypes)
                    {
                        continue;
                    }

                    if (frameworkReferences.Contains(dependency.Name))
                    {
                        frameworkAssemblies.Add(dependency.Name);
                    }
                    else
                    {
                        var dependencyVersion = new VersionSpec()
                        {
                            IsMinInclusive = true,
                            MinVersion = dependency.Version
                        };
                        if (dependencyVersion.MinVersion == null || dependencyVersion.MinVersion.IsSnapshot)
                        {
                            var actual = _packages
                                .Where(pkg => string.Equals(pkg.Identity.Name, project.Name, StringComparison.OrdinalIgnoreCase))
                                .SelectMany(pkg => pkg.Dependencies)
                                .SingleOrDefault(dep => string.Equals(dep.Name, dependency.Name, StringComparison.OrdinalIgnoreCase));
                            if (actual != null)
                            {
                                dependencyVersion.MinVersion = actual.Version;
                            }
                        }

                        dependencies.Add(new PackageDependency(dependency.Name, dependencyVersion));
                    }
                }

                if (dependencies.Count > 0)
                {
                    builder.DependencySets.Add(new PackageDependencySet(framework, dependencies));
                }
            }

            // Only do this on full desktop
            if (targetFramework.Identifier == VersionUtility.DefaultTargetFramework.Identifier)
            {
                foreach (var a in frameworkAssemblies)
                {
                    builder.FrameworkReferences.Add(new FrameworkAssemblyReference(a));
                }
            }

            var file = new PhysicalPackageFile();
            file.SourcePath = assemblyPath;
            var folder = VersionUtility.GetShortFrameworkName(framework);
            file.TargetPath = String.Format(@"lib\{0}\{1}.dll", folder, project.Name);
            builder.Files.Add(file);
        }
    }
}
