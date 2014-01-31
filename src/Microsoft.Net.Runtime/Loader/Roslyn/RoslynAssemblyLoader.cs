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

#if NET45 // TODO: Temporary due to CoreCLR and Desktop Roslyn being out of sync
using EmitResult = Microsoft.CodeAnalysis.Emit.CommonEmitResult;
#endif

namespace Microsoft.Net.Runtime.Loader.Roslyn
{
    public class RoslynAssemblyLoader : IAssemblyLoader, IPackageLoader, IMetadataLoader
    {
        private readonly Dictionary<string, CompiledAssembly> _compiledAssemblies = new Dictionary<string, CompiledAssembly>(StringComparer.OrdinalIgnoreCase);

        private readonly IProjectResolver _projectResolver;
        private readonly IFileWatcher _watcher;
        private readonly IFrameworkReferenceResolver _resolver;
        private readonly IGlobalAssemblyCache _globalAssemblyCache;
        private readonly IAssemblyLoader _dependencyLoader;
        private readonly IResourceProvider _resourceProvider;

        private IEnumerable<PackageDescription> _packages;

        public RoslynAssemblyLoader(IProjectResolver projectResolver,
                                    IFileWatcher watcher,
                                    IFrameworkReferenceResolver resolver,
                                    IGlobalAssemblyCache globalAssemblyCache,
                                    IAssemblyLoader dependencyLoader,
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

            CompiledAssembly compiledAssembly;
            if (_compiledAssemblies.TryGetValue(name, out compiledAssembly))
            {
                return new AssemblyLoadResult(compiledAssembly.Assembly);
            }

            Project project;
            // Can't find a project file with the name so bail
            if (!_projectResolver.TryResolveProject(name, out project))
            {
                return null;
            }

            string path = project.ProjectDirectory;

            _watcher.WatchFile(project.ProjectFilePath);

            TargetFrameworkConfiguration targetFrameworkConfig = project.GetTargetFrameworkConfiguration(loadContext.TargetFramework);

            Trace.TraceInformation("[{0}]: Found project '{1}' framework={2}", GetType().Name, project.Name, loadContext.TargetFramework);

            var references = new List<MetadataReference>();

            if (project.Dependencies.Count > 0 ||
                targetFrameworkConfig.Dependencies.Count > 0)
            {
                Trace.TraceInformation("[{0}]: Loading dependencies for '{1}'", GetType().Name, project.Name);
                var dependencyStopWatch = Stopwatch.StartNew();

                var errors = new List<string>();

                foreach (var dependency in project.Dependencies.Concat(targetFrameworkConfig.Dependencies))
                {
                    MetadataReference reference;
                    if (TryResolveDependency(dependency, loadContext, errors, out reference))
                    {
                        references.Add(reference);
                    }
                }

                if (errors.Count > 0)
                {
                    return new AssemblyLoadResult(errors);
                }

                dependencyStopWatch.Stop();
                Trace.TraceInformation("[{0}]: Completed loading dependencies for '{1}' in {2}ms", GetType().Name, project.Name, dependencyStopWatch.ElapsedMilliseconds);
            }

            references.AddRange(_resolver.GetDefaultReferences(loadContext.TargetFramework));

            Trace.TraceInformation("[{0}]: Compiling '{1}'", GetType().Name, name);
            var sw = Stopwatch.StartNew();

            try
            {
                _watcher.WatchDirectory(path, ".cs");

                var trees = new List<SyntaxTree>();

                bool hasAssemblyInfo = false;

                var sourceFiles = project.SourceFiles.ToList();

                var parseOptions = new CSharpParseOptions(preprocessorSymbols: targetFrameworkConfig.Defines.AsImmutable());

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

                foreach (var directory in System.IO.Directory.EnumerateDirectories(path, "*.*", SearchOption.AllDirectories))
                {
                    _watcher.WatchDirectory(directory, ".cs");
                }

                Compilation compilation = CSharpCompilation.Create(
                    name,
                    targetFrameworkConfig.CompilationOptions,
                    syntaxTrees: trees,
                    references: references);

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

                // If we're not loading from the output path then compile in memory
                if (!loadContext.CreateArtifacts)
                {
                    return CompileInMemory(name, compilation, resources);
                }

                // Ensure there's an output directory
                System.IO.Directory.CreateDirectory(loadContext.OutputPath);

                AssemblyLoadResult loadResult = CompileToDisk(assemblyPath, pdbPath, compilation, resources);

                if (loadResult != null && loadResult.Errors == null)
                {
                    // Attempt to build packages for this project
                    BuildPackages(loadContext, project, assemblyPath, pdbPath, sourceFiles);
                }

                return loadResult;

            }
            finally
            {
                Trace.TraceInformation("[{0}]: Compiled '{1}' in {2}ms", GetType().Name, name, sw.ElapsedMilliseconds);
            }
        }

        public MetadataReference GetMetadata(string name)
        {
            CompiledAssembly compiledAssembly;
            if (_compiledAssemblies.TryGetValue(name, out compiledAssembly))
            {
                return compiledAssembly.MetadataReference;
            }

            string assemblyLocation;
            if (_globalAssemblyCache.TryResolvePartialName(name, out assemblyLocation))
            {
                return new MetadataFileReference(assemblyLocation);
            }

            return null;
        }

        public PackageDescription GetDescription(string name, SemanticVersion version, FrameworkName frameworkName)
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

            var config = project.GetTargetFrameworkConfiguration(frameworkName);

            return new PackageDescription
            {
                Identity = new PackageReference { Name = project.Name, Version = project.Version },
                Dependencies = project.Dependencies.Concat(config.Dependencies),
            };
        }

        public void Initialize(IEnumerable<PackageDescription> packages, FrameworkName frameworkName)
        {
            _packages = packages;
        }

        private bool TryResolveDependency(PackageReference dependency, LoadContext loadContext, List<string> errors, out MetadataReference resolved)
        {
            resolved = null;

            var childContext = new LoadContext(dependency.Name, loadContext.TargetFramework);

            var loadResult = _dependencyLoader.Load(childContext);

            if (loadResult == null)
            {
                string assemblyLocation;
                if (_globalAssemblyCache.TryResolvePartialName(dependency.Name, out assemblyLocation))
                {
                    resolved = new MetadataFileReference(assemblyLocation);
                    return true;
                }

                errors.Add(String.Format("Unable to resolve dependency '{0}' for target framework '{1}'.", dependency, loadContext.TargetFramework));
                return false;
            }

            if (loadResult.Errors != null)
            {
                errors.AddRange(loadResult.Errors);
                return false;
            }

            CompiledAssembly compiledAssembly;
            if (_compiledAssemblies.TryGetValue(dependency.Name, out compiledAssembly))
            {
                resolved = compiledAssembly.MetadataReference;
                return true;
            }

            resolved = new MetadataFileReference(loadResult.Assembly.Location);
            return true;
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

            return new AssemblyLoadResult(Assembly.LoadFile(assemblyPath));
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

                var compiled = new CompiledAssembly
                {
#if NET45
                    Assembly = Assembly.Load(assemblyBytes, pdbBytes),
#else
                    Assembly = Assembly.Load(assemblyBytes),
#endif
                    MetadataReference = compilation.ToMetadataReference()
                };

                _compiledAssemblies[name] = compiled;

                return new AssemblyLoadResult(compiled.Assembly);
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

        private class CompiledAssembly
        {
            public Assembly Assembly { get; set; }

            public MetadataReference MetadataReference { get; set; }
        }
    }
}
