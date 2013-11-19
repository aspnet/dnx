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
using NuGet;

namespace Loader
{
    public class RoslynLoader : IAssemblyLoader, IDependencyResolver, IAssemblyReferenceResolver
    {
        private readonly Dictionary<string, Tuple<Assembly, MetadataReference>> _compiledAssemblies = new Dictionary<string, Tuple<Assembly, MetadataReference>>(StringComparer.OrdinalIgnoreCase);
        private readonly string _solutionPath;
        private readonly string _symbolsPath;
        private readonly IFileWatcher _watcher;
        private readonly IFrameworkReferenceResolver _resolver;

        public RoslynLoader(string solutionPath, IFileWatcher watcher, IFrameworkReferenceResolver resolver)
        {
            _solutionPath = solutionPath;
            _watcher = watcher;
            _resolver = resolver;
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

            _watcher.WatchFile(project.ProjectFilePath);

            List<MetadataReference> references = null;

            if (project.Dependencies.Count > 0)
            {
                Trace.TraceInformation("[{0}]: Loading dependencies for '{1}'", GetType().Name, project.Name);
                var dependencyStopWatch = Stopwatch.StartNew();

                references = project.Dependencies
                                    .Select(ResolveDependency)
                                    .ToList();

                dependencyStopWatch.Stop();
                Trace.TraceInformation("[{0}]: Completed loading dependencies for '{1}' in {2}ms", GetType().Name, project.Name, dependencyStopWatch.ElapsedMilliseconds);
            }
            else
            {
                references = new List<MetadataReference>();
            }

            // Always use the dll in the bin directory if it exists (unless we're doing a new compilation)
            string cachedFile = Path.Combine(path, "bin", name + ".dll");

            if (File.Exists(cachedFile) && options.OutputPath == null)
            {
                Trace.TraceInformation("[{0}]: Found cached copy of '{1}' in {2}.", GetType().Name, name, cachedFile);

                var cachedAssembly = Assembly.LoadFile(cachedFile);

                MetadataReference cachedReference = new MetadataFileReference(cachedFile);

                _compiledAssemblies[name] = Tuple.Create(cachedAssembly, cachedReference);

                return cachedAssembly;
            }

            references.AddRange(_resolver.GetDefaultReferences(project.TargetFramework));

            Trace.TraceInformation("[{0}]: Compiling '{1}'", GetType().Name, name);
            var sw = Stopwatch.StartNew();

            try
            {
                _watcher.WatchDirectory(path, ".cs");

                var trees = new List<SyntaxTree>();

                bool hasAssemblyInfo = false;

                var sourceFiles = project.SourceFiles.ToList();

                var parseOptions = new ParseOptions(preprocessorSymbols: project.Defines.AsImmutable());

                foreach (var sourcePath in sourceFiles)
                {
                    if (!hasAssemblyInfo && Path.GetFileNameWithoutExtension(sourcePath).Equals("AssemblyInfo"))
                    {
                        hasAssemblyInfo = true;
                    }

                    _watcher.WatchFile(sourcePath);
                    trees.Add(SyntaxTree.ParseFile(sourcePath, parseOptions));
                }

                if (!hasAssemblyInfo)
                {
                    trees.Add(SyntaxTree.ParseText("[assembly: System.Reflection.AssemblyVersion(\"" + project.Version.Version + "\")]"));
                    trees.Add(SyntaxTree.ParseText("[assembly: System.Reflection.AssemblyInformationalVersion(\"" + project.Version + "\")]"));
                }

                foreach (var directory in Directory.EnumerateDirectories(path, "*.*", SearchOption.AllDirectories))
                {
                    _watcher.WatchDirectory(directory, ".cs");
                }

                // Create a compilation
                Compilation compilation = Compilation.Create(
                    name,
                    project.CompilationOptions,
                    syntaxTrees: trees,
                    references: references);

                List<ResourceDescription> resources = GetResources(path);

                if (options.OutputPath != null)
                {
                    Directory.CreateDirectory(options.OutputPath);

                    string assemblyPath = Path.Combine(options.OutputPath, name + ".dll");
                    string pdbPath = Path.Combine(options.OutputPath, name + ".pdb");
                    string nupkg = Path.Combine(options.OutputPath, project.Name + "." + project.Version + ".nupkg");
                    string symbolsNupkg = Path.Combine(options.OutputPath, project.Name + "." + project.Version + ".symbols.nupkg");

                    var result = compilation.Emit(assemblyPath, pdbPath, manifestResources: resources);

                    if (!result.Success)
                    {
                        ReportCompilationError(result);

                        return null;
                    }

                    // Build packages
                    BuildPackage(project, assemblyPath, nupkg);
                    BuildSymbolsPackage(project, assemblyPath, pdbPath, symbolsNupkg, sourceFiles);

                    // TODO: Do we want to build symbol packages as well

                    Trace.TraceInformation("{0} -> {1}", name, assemblyPath);
                    Trace.TraceInformation("{0} -> {1}", name, nupkg);

                    return Assembly.LoadFile(assemblyPath);
                }

                return CompileToMemoryStream(name, compilation, resources);
            }
            finally
            {
                Trace.TraceInformation("[{0}]: Compiled '{1}' in {2}ms", GetType().Name, name, sw.ElapsedMilliseconds);
            }
        }

        public MetadataReference ResolveReference(string name)
        {
            Tuple<Assembly, MetadataReference> cached;
            if (_compiledAssemblies.TryGetValue(name, out cached))
            {
                return cached.Item2;
            }

            try
            {
                return MetadataReference.CreateAssemblyReference(name);
            }
            catch
            {
                return null;
            }
        }

        private static List<ResourceDescription> GetResources(string projectPath)
        {
            // HACK to make resource files work.
            // TODO: This factor this into a system to do file processing based on the current
            // compilation
            return Directory.EnumerateFiles(projectPath, "*.resx", SearchOption.AllDirectories)
                            .Select(resxFilePath =>
                                new ResourceDescription(GetResourceName(resxFilePath),
                                                        () => GetResourceStream(resxFilePath),
                                                        isPublic: true)).ToList();
        }

        private static string GetResourceName(string resxFilePath)
        {
            Trace.TraceInformation("Found resource {0}", resxFilePath);

            string fileNameWithoutExtension = Path.GetFileNameWithoutExtension(resxFilePath);

            return fileNameWithoutExtension + ".resources";
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

        private MetadataReference ResolveDependency(Dependency dependency)
        {
            ExceptionDispatchInfo info = null;

            try
            {
                var loadedAssembly = Assembly.Load(dependency.Name);

                Tuple<Assembly, MetadataReference> cached;
                if (_compiledAssemblies.TryGetValue(dependency.Name, out cached))
                {
                    return cached.Item2;
                }

                return new MetadataFileReference(loadedAssembly.Location);
            }
            catch (FileNotFoundException ex)
            {
                info = ExceptionDispatchInfo.Capture(ex);

                try
                {
                    return MetadataFileReference.CreateAssemblyReference(dependency.Name);
                }
                catch
                {
                    info.Throw();

                    return null;
                }
            }
        }

        private void BuildSymbolsPackage(Project project, string assemblyPath, string pdbPath, string symbolsNupkg, List<string> sourceFiles)
        {
            // TODO: Build symbols packages
        }

        private void BuildPackage(Project project, string assemblyPath, string nupkg)
        {
            // TODO: Support nuspecs in the project folder

            var builder = new PackageBuilder();
            builder.Authors.AddRange(project.Authors);

            if (builder.Authors.Count == 0)
            {
                // Temporary
                builder.Authors.Add("K");
            }

            builder.Description = project.Description ?? project.Name;
            builder.Id = project.Name;
            builder.Version = project.Version;
            builder.Title = project.Name;
            var framework = project.TargetFramework;
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

            foreach (var a in frameworkAssemblies)
            {
                builder.FrameworkReferences.Add(new FrameworkAssemblyReference(a));
            }

            var file = new PhysicalPackageFile();
            file.SourcePath = assemblyPath;
            var folder = VersionUtility.GetShortFrameworkName(project.TargetFramework);
            file.TargetPath = "lib\\" + folder + "\\" + project.Name + ".dll";
            builder.Files.Add(file);

            using (var pkg = File.Create(nupkg))
            {
                builder.Save(pkg);
            }
        }

        private Assembly CompileToMemoryStream(string name, Compilation compilation, IEnumerable<ResourceDescription> resources)
        {
            // Put symbols in a .symbols path
            var pdbPath = Path.Combine(_symbolsPath, name + ".pdb");

            Directory.CreateDirectory(_symbolsPath);

            using (var fs = File.Create(pdbPath))
            using (var ms = new MemoryStream())
            {
                EmitResult result = compilation.Emit(ms, pdbStream: fs, manifestResources: resources);

                if (!result.Success)
                {
                    ReportCompilationError(result);

                    return null;
                }

                var bytes = ms.ToArray();

                var assembly = Assembly.Load(bytes);
                MetadataReference reference = compilation.ToMetadataReference();

                var compiled = Tuple.Create(assembly, reference);

                _compiledAssemblies[name] = compiled;

                return assembly;
            }
        }

        private static void ReportCompilationError(EmitResult result)
        {
            throw new InvalidDataException(String.Join(Environment.NewLine,
                result.Diagnostics.Select(d => DiagnosticFormatter.Instance.Format(d))));
        }

        public IEnumerable<Dependency> GetDependencies(string name, SemanticVersion version, FrameworkName frameworkName)
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

        public void Initialize(IEnumerable<Dependency> dependencies, FrameworkName frameworkName)
        {

        }
    }
}
