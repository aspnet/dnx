using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Runtime.Versioning;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Emit;
using NuGet;

namespace Loader
{
    public class RoslynLoader : IAssemblyLoader, IDependencyResolver
    {
        private readonly Dictionary<string, Tuple<Assembly, MetadataReference>> _compiledAssemblies = new Dictionary<string, Tuple<Assembly, MetadataReference>>(StringComparer.OrdinalIgnoreCase);
        private readonly string _solutionPath;
        private readonly IFileWatcher _watcher;
        private readonly IFrameworkReferenceResolver _resolver;

        public RoslynLoader(string solutionPath, IFileWatcher watcher, IFrameworkReferenceResolver resolver)
        {
            _solutionPath = solutionPath;
            _watcher = watcher;
            _resolver = resolver;
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

            _watcher.WatchDirectory(path, ".cs");
            _watcher.WatchFile(project.ProjectFilePath);

            var trees = new List<SyntaxTree>();

            bool hasAssemblyInfo = false;

            foreach (var sourcePath in project.SourceFiles)
            {
                if (!hasAssemblyInfo && Path.GetFileNameWithoutExtension(sourcePath).Equals("AssemblyInfo"))
                {
                    hasAssemblyInfo = true;
                }

                _watcher.WatchFile(sourcePath);
                trees.Add(SyntaxTree.ParseFile(sourcePath));
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


            List<MetadataReference> references = null;

            if (project.Dependencies.Count > 0)
            {
                Trace.TraceInformation("Loading dependencies for '{0}'", project.Name);

                references = project.Dependencies
                             // .AsParallel() // TODO: Add this when we're threadsafe
                                .Select(d =>
                                {
                                    ExceptionDispatchInfo info = null;

                                    try
                                    {
                                        var loadedAssembly = Assembly.Load(d.Name);

                                        Tuple<Assembly, MetadataReference> compiledDependency;
                                        if (_compiledAssemblies.TryGetValue(d.Name, out compiledDependency))
                                        {
                                            return compiledDependency.Item2;
                                        }

                                        return new MetadataFileReference(loadedAssembly.Location);
                                    }
                                    catch (FileNotFoundException ex)
                                    {
                                        info = ExceptionDispatchInfo.Capture(ex);

                                        try
                                        {
                                            return MetadataFileReference.CreateAssemblyReference(d.Name);
                                        }
                                        catch
                                        {
                                            info.Throw();

                                            return null;
                                        }
                                    }
                                }).ToList();

                Trace.TraceInformation("Completed loading dependencies for '{0}'", project.Name);
            }
            else
            {
                references = new List<MetadataReference>();
            }

            references.AddRange(_resolver.GetDefaultReferences(project.TargetFramework));

            Trace.TraceInformation("[{0}]: Compiling '{1}'", GetType().Name, name);
            var sw = Stopwatch.StartNew();

            try
            {
                // Create a compilation
                var compilation = Compilation.Create(
                    name,
                    new CompilationOptions(OutputKind.DynamicallyLinkedLibrary),
                    syntaxTrees: trees,
                    references: references);

                if (options.OutputPath != null)
                {
                    Directory.CreateDirectory(options.OutputPath);

                    string assemblyPath = Path.Combine(options.OutputPath, name + ".dll");
                    string pdbPath = Path.Combine(options.OutputPath, name + ".pdb");
                    string nupkg = Path.Combine(options.OutputPath, project.Name + "." + project.Version + ".nupkg");

                    var result = compilation.Emit(assemblyPath, pdbPath);

                    if (!result.Success)
                    {
                        ReportCompilationError(result);

                        return null;
                    }

                    // Build packages
                    BuildPackage(project, assemblyPath, nupkg);

                    // TODO: Do we want to build symbol packages as well

                    Trace.TraceInformation("{0} -> {1}", name, assemblyPath);
                    Trace.TraceInformation("{0} -> {1}", name, nupkg);

                    return Assembly.LoadFile(assemblyPath);
                }

                return CompileToMemoryStream(name, compilation);
            }
            finally
            {
                Trace.TraceInformation("[{0}]: Compiled '{1}' in {2}ms", GetType().Name, name, sw.ElapsedMilliseconds);
            }
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

        private Assembly CompileToMemoryStream(string name, Compilation compilation)
        {
            // Put symbols in a .symbols path
            var pdbPath = Path.Combine(_solutionPath, ".symbols", name + ".pdb");

            Directory.CreateDirectory(Path.GetDirectoryName(pdbPath));

            using (var fs = File.Create(pdbPath))
            using (var ms = new MemoryStream())
            {
                EmitResult result = compilation.Emit(ms, pdbStream: fs);

                if (!result.Success)
                {
                    ReportCompilationError(result);

                    return null;
                }

                var bytes = ms.ToArray();

                var assembly = Assembly.Load(bytes);
                MetadataReference reference = new MetadataImageReference(bytes);

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
            if (!Project.TryGetProject(path, out project) || (version != null && project.Version != version))
            {
                return null;
            }

            return project.Dependencies;
        }

        public void Initialize(IEnumerable<Dependency> dependencies)
        {

        }
    }

}
