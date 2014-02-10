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

#if NET45 // TODO: Temporary due to CoreCLR and Desktop Roslyn being out of sync
using EmitResult = Microsoft.CodeAnalysis.Emit.CommonEmitResult;
#endif

namespace Microsoft.Net.Runtime.Roslyn
{
    public class RoslynAssemblyLoader : IAssemblyLoader, IPackageLoader, IDependencyImpactResolver
    {
        private readonly Dictionary<string, Compilation> _compilationCache = new Dictionary<string, Compilation>();

        private readonly IProjectResolver _projectResolver;
        private readonly IFileWatcher _watcher;
        private readonly IFrameworkReferenceResolver _resolver;
        private readonly IGlobalAssemblyCache _globalAssemblyCache;
        private readonly IDependencyImpactResolver _dependencyLoader;
        private readonly IResourceProvider _resourceProvider;

        private IEnumerable<DependencyDescription> _packages;

        public RoslynAssemblyLoader(IProjectResolver projectResolver,
                                    IFileWatcher watcher,
                                    IDependencyImpactResolver dependencyLoader)
        {
            _projectResolver = projectResolver;
            _watcher = watcher;
            _globalAssemblyCache = new DefaultGlobalAssemblyCache();
            _resolver = new FrameworkReferenceResolver(_globalAssemblyCache);
            _dependencyLoader = dependencyLoader;
            _resourceProvider = new ResxResourceProvider();
        }

        public RoslynAssemblyLoader(IProjectResolver projectResolver,
                                    IFileWatcher watcher,
                                    IFrameworkReferenceResolver resolver,
                                    IGlobalAssemblyCache globalAssemblyCache,
                                    IDependencyImpactResolver dependencyLoader,
                                    IResourceProvider resourceProvider)
        {
            _projectResolver = projectResolver;
            _watcher = watcher;
            _resolver = resolver;
            _globalAssemblyCache = globalAssemblyCache;
            _dependencyLoader = dependencyLoader;
            _resourceProvider = resourceProvider;
        }

        public AssemblyLoadResult Load(LoadContext loadContext)
        {
            string name = loadContext.AssemblyName;

            Project project;
            // Can't find a project file with the name so bail
            if (!_projectResolver.TryResolveProject(name, out project))
            {
                return null;
            }

            string path = project.ProjectDirectory;

            IList<string> errors;
            Compilation compilation = GetCompilation(loadContext.AssemblyName, loadContext.TargetFramework, out errors);

            if (errors != null && errors.Count > 0)
            {
                return new AssemblyLoadResult(errors);
            }

            IList<ResourceDescription> resources = _resourceProvider.GetResources(project.Name, path);

            // If the output path is null then load the assembly from memory
            if (loadContext.OutputPath == null)
            {
                return CompileInMemory(name, compilation, resources);
            }

            string assemblyPath = Path.Combine(loadContext.OutputPath, name + ".dll");
            string pdbPath = Path.Combine(loadContext.OutputPath, name + ".pdb");

            // Add to the list of generated artifacts
            if (loadContext.ArtifactPaths != null)
            {
                loadContext.ArtifactPaths.Add(assemblyPath);
                loadContext.ArtifactPaths.Add(pdbPath);
            }

            // Ensure there's an output directory
            Directory.CreateDirectory(loadContext.OutputPath);

            AssemblyLoadResult loadResult = CompileToDisk(assemblyPath, pdbPath, compilation, resources);

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

        private Compilation GetCompilation(string name, FrameworkName targetFramework, out IList<string> errors)
        {
            Compilation compilation;
            if (_compilationCache.TryGetValue(name, out compilation))
            {
                errors = null;
                return compilation;
            }

            Project project;
            // Can't find a project file with the name so bail
            if (!_projectResolver.TryResolveProject(name, out project))
            {
                errors = null;
                return null;
            }

            string path = project.ProjectDirectory;

            _watcher.WatchFile(project.ProjectFilePath);

            TargetFrameworkConfiguration targetFrameworkConfig = project.GetTargetFrameworkConfiguration(targetFramework);

            Trace.TraceInformation("[{0}]: Found project '{1}' framework={2}", GetType().Name, project.Name, targetFramework);

            var references = new List<MetadataReference>();
            var dependencyImpacts = new List<DependencyImpact>();

            errors = new List<string>();

            if (project.Dependencies.Count > 0 ||
                targetFrameworkConfig.Dependencies.Count > 0)
            {
                Trace.TraceInformation("[{0}]: Loading dependencies for '{1}'", GetType().Name, project.Name);
                var dependencyStopWatch = Stopwatch.StartNew();

                foreach (var dependency in project.Dependencies.Concat(targetFrameworkConfig.Dependencies))
                {
                    DependencyImpact impact = _dependencyLoader.GetDependencyImpact(dependency.Name, targetFramework);

                    if (impact != null)
                    {
                        dependencyImpacts.Add(impact);
                    }
                    else
                    {
                        errors.Add(String.Format("Unable to resolve dependency '{0}' for target framework '{1}'.", dependency, targetFramework));
                    }
                }

                dependencyStopWatch.Stop();
                Trace.TraceInformation("[{0}]: Completed loading dependencies for '{1}' in {2}ms", GetType().Name, project.Name, dependencyStopWatch.ElapsedMilliseconds);
            }

            if (errors.Count > 0)
            {
                return null;
            }

            references.AddRange(GetMetadataReferences(dependencyImpacts));

            references.AddRange(_resolver.GetDefaultReferences(targetFramework));

            Trace.TraceInformation("[{0}]: Compiling '{1}'", GetType().Name, name);
            var sw = Stopwatch.StartNew();

            _watcher.WatchDirectory(path, ".cs");

            var trees = new List<SyntaxTree>();

            bool hasAssemblyInfo = false;

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

            compilation = CSharpCompilation.Create(
                name,
                compilationSettings.CompilationOptions,
                syntaxTrees: trees,
                references: references);

            // Cache the compilation
            _compilationCache[name] = compilation;

            return compilation;
        }

        private IEnumerable<MetadataReference> GetMetadataReferences(List<DependencyImpact> dependencyImpacts)
        {
            var paths = new HashSet<string>();
            var references = new List<MetadataReference>();

            foreach (var impact in dependencyImpacts)
            {
                foreach (var reference in impact.MetadataReferences)
                {
                    var fileMetadataReference = reference as IMetadataFileReference;

                    if (fileMetadataReference != null)
                    {
                        // Make sure nothing is duped
                        paths.Add(fileMetadataReference.Path);
                    }
                    else
                    {
                        references.Add(ConvertMetadataReference(reference));
                    }
                }
            }

            // Now add the final set to the final set of references
            references.AddRange(paths.Select(path => new MetadataFileReference(path)));

            return references;
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

        public DependencyImpact GetDependencyImpact(string name, FrameworkName targetFramework)
        {
            string assemblyLocation;
            if (_globalAssemblyCache.TryResolvePartialName(name, out assemblyLocation))
            {
                return CreateDependencyImpact(assemblyLocation);
            }

            IList<string> errors;
            var compilation = GetCompilation(name, targetFramework, out errors);

            if (compilation == null)
            {
                return null;
            }

            return CreateDependencyImpact(compilation.ToMetadataReference());
        }

        private AssemblyLoadResult CompileToDisk(string assemblyPath, string pdbPath, Compilation compilation, IList<ResourceDescription> resources)
        {
#if NET45
            EmitResult result = compilation.Emit(assemblyPath, pdbPath, manifestResources: resources);
#else
            EmitResult result = compilation.Emit(assemblyPath);
#endif

            if (!result.Success)
            {
                // REVIEW: Emit seems to create the output assembly even if the build fails
                // follow up to see why this happens
                if (File.Exists(assemblyPath))
                {
                    File.Delete(assemblyPath);
                }

                return ReportCompilationError(result);
            }

            // Valid result but we don't have to load it
            return new AssemblyLoadResult();
        }

        private AssemblyLoadResult CompileInMemory(string name, Compilation compilation, IEnumerable<ResourceDescription> resources)
        {
            using (var pdbStream = new MemoryStream())
            using (var assemblyStream = new MemoryStream())
            {
#if NET45
                EmitResult result = compilation.Emit(assemblyStream, pdbStream: pdbStream, manifestResources: resources);
#else
                EmitResult result = compilation.Emit(assemblyStream);
#endif

                if (!result.Success)
                {
                    return ReportCompilationError(result);
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

        private static DependencyImpact CreateDependencyImpact(MetadataReference metadataReference)
        {
            return new DependencyImpact(new MetadataReferenceWrapper(metadataReference));
        }

        private static DependencyImpact CreateDependencyImpact(string assemblyLocation)
        {
            return CreateDependencyImpact(new MetadataFileReference(assemblyLocation));
        }

        private static AssemblyLoadResult ReportCompilationError(EmitResult result)
        {
#if NET45 // TODO: Temporary due to CoreCLR and Desktop Roslyn being out of sync
            var formatter = DiagnosticFormatter.Instance;
#else
            var formatter = new DiagnosticFormatter();
#endif
            var errors = new List<string>(result.Diagnostics.Select(d => formatter.Format(d)));

            return new AssemblyLoadResult(errors);
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
