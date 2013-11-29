using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Resources;
using System.Runtime.ExceptionServices;
using System.Runtime.Versioning;
using System.Xml.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Emit;
using Microsoft.CodeAnalysis.Emit;
using Microsoft.Net.Runtime.FileSystem;
using NuGet;

namespace Microsoft.Net.Runtime.Loader.Roslyn
{
    public class RoslynAssemblyLoader : IAssemblyLoader, IPackageLoader, IMetadataLoader
    {
        private readonly Dictionary<string, Tuple<Assembly, MetadataReference>> _compiledAssemblies = new Dictionary<string, Tuple<Assembly, MetadataReference>>(StringComparer.OrdinalIgnoreCase);
        private readonly string _solutionPath;
        private readonly string _symbolsPath;
        private readonly IFileWatcher _watcher;
        private readonly IFrameworkReferenceResolver _resolver;
        private readonly IAssemblyLoader _dependencyLoader;

        public RoslynAssemblyLoader(string solutionPath, IFileWatcher watcher, IFrameworkReferenceResolver resolver, IAssemblyLoader dependencyLoader)
        {
            _solutionPath = solutionPath;
            _watcher = watcher;
            _resolver = resolver;
            _dependencyLoader = dependencyLoader;
            _symbolsPath = Path.Combine(_solutionPath, ".symbols");
        }

        public Assembly Load(LoadOptions options)
        {
            string name = options.AssemblyName;

            Tuple<Assembly, MetadataReference> compiledAssembly;
            if (_compiledAssemblies.TryGetValue(name, out compiledAssembly))
            {
                return compiledAssembly.Item1;
            }

            string path = Path.Combine(_solutionPath, name);
            Project project;

            // Can't find a project file with the name so bail
            if (!Project.TryGetProject(path, out project))
            {
                return null;
            }

            TargetFrameworkConfiguration configurationSettings = project.GetTargetFrameworkConfiguration(options.TargetFramework);

            Trace.TraceInformation("[{0}]: Found project '{1}' framework={2}", GetType().Name, project.Name, options.TargetFramework);

            _watcher.WatchFile(project.ProjectFilePath);

            List<MetadataReference> references = null;

            if (project.Dependencies.Count > 0)
            {
                Trace.TraceInformation("[{0}]: Loading dependencies for '{1}'", GetType().Name, project.Name);
                var dependencyStopWatch = Stopwatch.StartNew();

                references = project.Dependencies
                                    .Select(r => ResolveDependency(r, options))
                                    .ToList();

                dependencyStopWatch.Stop();
                Trace.TraceInformation("[{0}]: Completed loading dependencies for '{1}' in {2}ms", GetType().Name, project.Name, dependencyStopWatch.ElapsedMilliseconds);
            }
            else
            {
                references = new List<MetadataReference>();
            }

            // Always use the dll in the bin directory if it exists (unless we're doing a new compilation)
            var targetFrameworkFolder = VersionUtility.GetShortFrameworkName(options.TargetFramework);
            string cachedFile = Path.Combine(path, "bin", targetFrameworkFolder, name + ".dll");

            if (File.Exists(cachedFile) && options.OutputPath == null)
            {
                Trace.TraceInformation("[{0}]: Found cached copy of '{1}' in {2}.", GetType().Name, name, cachedFile);

                var cachedAssembly = Assembly.LoadFile(Path.GetFullPath(cachedFile));

                MetadataReference cachedReference = new MetadataFileReference(cachedFile);

                _compiledAssemblies[name] = Tuple.Create(cachedAssembly, cachedReference);

                return cachedAssembly;
            }

            references.AddRange(_resolver.GetDefaultReferences(options.TargetFramework));

            Trace.TraceInformation("[{0}]: Compiling '{1}'", GetType().Name, name);
            var sw = Stopwatch.StartNew();

            try
            {
                _watcher.WatchDirectory(path, ".cs");

                var trees = new List<SyntaxTree>();

                bool hasAssemblyInfo = false;

                var sourceFiles = project.SourceFiles.ToList();

                var parseOptions = new CSharpParseOptions(preprocessorSymbols: configurationSettings.Defines.AsImmutable());

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

                // Create a compilation
                Compilation compilation = CSharpCompilation.Create(
                    name,
                    configurationSettings.CompilationOptions,
                    syntaxTrees: trees,
                    references: references);

                List<ResourceDescription> resources = GetResources(project.Name, path);

                if (options.OutputPath != null)
                {
                    string outputPath = options.OutputPath;

                    if (!options.Clean)
                    {
                        System.IO.Directory.CreateDirectory(outputPath);
                    }

                    string assemblyPath = Path.Combine(outputPath, name + ".dll");
                    string pdbPath = Path.Combine(outputPath, name + ".pdb");

                    if (options.Artifacts != null)
                    {
                        options.Artifacts.Add(assemblyPath);
                        options.Artifacts.Add(pdbPath);

                        if (options.Clean)
                        {
                            // Compile in memory to avoid locking the file
                            return CompileToMemoryStream(name, compilation, resources, cache: false);
                        }
                    }

                    var result = compilation.Emit(assemblyPath, pdbPath, manifestResources: resources);

                    if (!result.Success)
                    {
                        ReportCompilationError(result);

                        return null;
                    }

                    // Build packages
                    BuildPackage(project, assemblyPath, options.PackageBuilder, options.TargetFramework);
                    BuildSymbolsPackage(project, assemblyPath, pdbPath, options.SymbolPackageBuilder, sourceFiles, options.TargetFramework);

                    Trace.TraceInformation("{0} -> {1}", name, assemblyPath);

                    return Assembly.LoadFile(assemblyPath);
                }

                return CompileToMemoryStream(name, compilation, resources);
            }
            finally
            {
                Trace.TraceInformation("[{0}]: Compiled '{1}' in {2}ms", GetType().Name, name, sw.ElapsedMilliseconds);
            }
        }

        public MetadataReference GetMetadata(string name)
        {
            Tuple<Assembly, MetadataReference> cached;
            if (_compiledAssemblies.TryGetValue(name, out cached))
            {
                return cached.Item2;
            }

            string assemblyLocation;
            if (GlobalAssemblyCache.ResolvePartialName(name, out assemblyLocation) != null)
            {
                return new MetadataFileReference(assemblyLocation);
            }

            return null;
        }

        private static List<ResourceDescription> GetResources(string projectName, string projectPath)
        {
            // HACK to make resource files work.
            // TODO: This factor this into a system to do file processing based on the current
            // compilation
            return System.IO.Directory.EnumerateFiles(projectPath, "*.resx", SearchOption.AllDirectories)
                            .Select(resxFilePath =>
                                new ResourceDescription(GetResourceName(projectName, resxFilePath),
                                                        () => GetResourceStream(resxFilePath),
                                                        isPublic: true)).ToList();
        }

        private static string GetResourceName(string projectName, string resxFilePath)
        {
            Trace.TraceInformation("Found resource {0}", resxFilePath);

            string fileNameWithoutExtension = Path.GetFileNameWithoutExtension(resxFilePath);

            return projectName + "." + fileNameWithoutExtension + ".resources";
        }

        private static Stream GetResourceStream(string resxFilePath)
        {
            using (var fs = File.OpenRead(resxFilePath))
            {
                var document = XDocument.Load(fs);

                var ms = new MemoryStream();
                var rw = new ResourceWriter(ms);

                foreach (var e in document.Root.Elements("data"))
                {
                    string name = e.Attribute("name").Value;
                    string value = e.Element("value").Value;

                    rw.AddResource(name, value);
                }

                rw.Generate();
                ms.Seek(0, SeekOrigin.Begin);

                return ms;
            }
        }

        private MetadataReference ResolveDependency(PackageReference dependency, LoadOptions options)
        {
            string assemblyLocation;
            if (GlobalAssemblyCache.ResolvePartialName(dependency.Name, out assemblyLocation) != null)
            {
                return new MetadataFileReference(assemblyLocation);
            }

            var loadedAssembly = _dependencyLoader.Load(new LoadOptions
            {
                AssemblyName = dependency.Name,
                TargetFramework = options.TargetFramework
            });

            Tuple<Assembly, MetadataReference> cached;
            if (_compiledAssemblies.TryGetValue(dependency.Name, out cached))
            {
                return cached.Item2;
            }

            return new MetadataFileReference(loadedAssembly.Location);
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
            file.TargetPath = "lib\\" + folder + "\\" + project.Name + ".dll";
            builder.Files.Add(file);
        }

        private Assembly CompileToMemoryStream(string name, Compilation compilation, IEnumerable<ResourceDescription> resources, bool cache = true)
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
                    ReportCompilationError(result);

                    return null;
                }

                var bytes = ms.ToArray();

                var assembly = Assembly.Load(bytes);
                MetadataReference reference = compilation.ToMetadataReference();

                var compiled = Tuple.Create(assembly, reference);

                if (cache)
                {
                    _compiledAssemblies[name] = compiled;
                }

                return assembly;
            }
        }

        private static void ReportCompilationError(CommonEmitResult result)
        {
            throw new InvalidDataException(String.Join(Environment.NewLine,
                result.Diagnostics.Select(d => DiagnosticFormatter.Instance.Format(d))));
        }

        public IEnumerable<PackageReference> GetDependencies(string name, SemanticVersion version, FrameworkName frameworkName)
        {
            string path = Path.Combine(_solutionPath, name);
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
    }
}
