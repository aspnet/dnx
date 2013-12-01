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

namespace Microsoft.Net.Runtime.Loader.Roslyn
{
    public class RoslynAssemblyLoader : IAssemblyLoader, IPackageLoader, IMetadataLoader
    {
        private readonly Dictionary<string, CompiledAssembly> _compiledAssemblies = new Dictionary<string, CompiledAssembly>(StringComparer.OrdinalIgnoreCase);

        private readonly string _rootPath;
        private readonly string _symbolsPath;

        private readonly IFileWatcher _watcher;
        private readonly IFrameworkReferenceResolver _resolver;
        private readonly IAssemblyLoader _dependencyLoader;
        private readonly IResourceProvider _resourceProvider;

        public RoslynAssemblyLoader(string rootPath,
                                    IFileWatcher watcher,
                                    IFrameworkReferenceResolver resolver,
                                    IAssemblyLoader dependencyLoader,
                                    IResourceProvider resourceProvider)
        {
            _rootPath = rootPath;
            _watcher = watcher;
            _resolver = resolver;
            _dependencyLoader = dependencyLoader;
            _resourceProvider = resourceProvider;
            _symbolsPath = Path.Combine(_rootPath, ".symbols");
        }

        public AssemblyLoadResult Load(LoadContext loadContext)
        {
            string name = loadContext.AssemblyName;

            CompiledAssembly compiledAssembly;
            if (_compiledAssemblies.TryGetValue(name, out compiledAssembly))
            {
                return new AssemblyLoadResult(compiledAssembly.Assembly);
            }

            string path = Path.Combine(_rootPath, name);
            Project project;

            // Can't find a project file with the name so bail
            if (!Project.TryGetProject(path, out project))
            {
                return null;
            }

            TargetFrameworkConfiguration targetFrameworkConfig = project.GetTargetFrameworkConfiguration(loadContext.TargetFramework);

            Trace.TraceInformation("[{0}]: Found project '{1}' framework={2}", GetType().Name, project.Name, loadContext.TargetFramework);

            List<MetadataReference> references = null;

            if (project.Dependencies.Count > 0)
            {
                Trace.TraceInformation("[{0}]: Loading dependencies for '{1}'", GetType().Name, project.Name);
                var dependencyStopWatch = Stopwatch.StartNew();

                references = project.Dependencies
                                    .Select(r => ResolveDependency(r, loadContext))
                                    .Where(r => r != null)
                                    .ToList();

                dependencyStopWatch.Stop();
                Trace.TraceInformation("[{0}]: Completed loading dependencies for '{1}' in {2}ms", GetType().Name, project.Name, dependencyStopWatch.ElapsedMilliseconds);
            }
            else
            {
                references = new List<MetadataReference>();
            }

            string assemblyPath = null;
            string pdbPath = null;

            if (loadContext.OutputPath != null)
            {
                assemblyPath = Path.Combine(loadContext.OutputPath, name + ".dll");
                pdbPath = Path.Combine(loadContext.OutputPath, name + ".pdb");

                // Add the artifacts to the path
                if (loadContext.ArtifactPaths != null)
                {
                    loadContext.ArtifactPaths.Add(assemblyPath);
                    loadContext.ArtifactPaths.Add(pdbPath);
                }
            }

            if (loadContext.SkipAssemblyLoad)
            {
                // Skip everything else
                return new AssemblyLoadResult();
            }

            _watcher.WatchFile(project.ProjectFilePath);

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

                if (loadContext.OutputPath == null)
                {
                    return CompileInMemory(name, compilation, resources);
                }

                // Ensure there's an output directory
                System.IO.Directory.CreateDirectory(loadContext.OutputPath);

                AssemblyLoadResult loadResult = CompileToDisk(assemblyPath, pdbPath, compilation, resources);

                if (loadResult != null)
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
            if (GlobalAssemblyCache.ResolvePartialName(name, out assemblyLocation) != null)
            {
                return new MetadataFileReference(assemblyLocation);
            }

            return null;
        }

        public IEnumerable<PackageReference> GetDependencies(string name, SemanticVersion version, FrameworkName frameworkName)
        {
            string path = Path.Combine(_rootPath, name);
            Project project;

            // Can't find a project file with the name so bail
            if (!Project.TryGetProject(path, out project))
            {
                return null;
            }
            else if (version != null && project.Version != version)
            {
                return null;
            }

            return project.Dependencies;
        }

        public void Initialize(IEnumerable<PackageReference> packages, FrameworkName frameworkName)
        {

        }

        private MetadataReference ResolveDependency(PackageReference dependency, LoadContext loadContext)
        {
            string assemblyLocation;
            if (GlobalAssemblyCache.ResolvePartialName(dependency.Name, out assemblyLocation) != null)
            {
                if (loadContext.SkipAssemblyLoad)
                {
                    return null;
                }

                return new MetadataFileReference(assemblyLocation);
            }

            var childContext = new LoadContext(dependency.Name, loadContext.TargetFramework)
            {
                SkipAssemblyLoad = loadContext.SkipAssemblyLoad
            };

            var loadResult = _dependencyLoader.Load(childContext);

            if (loadResult == null)
            {
                throw new InvalidOperationException(String.Format("Unable to resolve dependency '{0}'.", dependency));
            }

            // If we're not loading assemblies then do nothing
            if (loadContext.SkipAssemblyLoad)
            {
                return null;
            }

            CompiledAssembly compiledAssembly;
            if (_compiledAssemblies.TryGetValue(dependency.Name, out compiledAssembly))
            {
                return compiledAssembly.MetadataReference;
            }

            return new MetadataFileReference(loadResult.Assembly.Location);
        }

        private AssemblyLoadResult CompileToDisk(string assemblyPath, string pdbPath, Compilation compilation, IList<ResourceDescription> resources)
        {
            var result = compilation.Emit(assemblyPath, pdbPath, manifestResources: resources);

            if (!result.Success)
            {
                return ReportCompilationError(result);
            }

            return new AssemblyLoadResult(Assembly.LoadFile(assemblyPath));
        }

        private AssemblyLoadResult CompileInMemory(string name, Compilation compilation, IEnumerable<ResourceDescription> resources)
        {
            // Put symbols in a .symbols path
            var pdbPath = Path.Combine(_symbolsPath, name + ".pdb");

            System.IO.Directory.CreateDirectory(_symbolsPath);

            using (var fs = File.Create(pdbPath))
            using (var ms = new MemoryStream())
            {
                CommonEmitResult result = compilation.Emit(ms, pdbStream: fs, manifestResources: resources);

                if (!result.Success)
                {
                    return ReportCompilationError(result);
                }

                var bytes = ms.ToArray();

                var compiled = new CompiledAssembly
                {
                    Assembly = Assembly.Load(bytes),
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

        private static AssemblyLoadResult ReportCompilationError(CommonEmitResult result)
        {
            var errors = new List<string>(result.Diagnostics.Select(d => DiagnosticFormatter.Instance.Format(d)));

            return new AssemblyLoadResult()
            {
                Errors = errors
            };
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
