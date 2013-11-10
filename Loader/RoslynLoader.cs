using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Emit;

namespace Loader
{
    public class RoslynLoader : IAssemblyLoader
    {
        private readonly Dictionary<string, Tuple<Assembly, MetadataReference>> _compiledAssemblies = new Dictionary<string, Tuple<Assembly, MetadataReference>>(StringComparer.OrdinalIgnoreCase);
        private readonly string _solutionPath;
        private readonly IFileWatcher _watcher;


        public RoslynLoader(string solutionPath, IFileWatcher watcher)
        {
            _solutionPath = solutionPath;
            _watcher = watcher;

            // HACK: We're making this available to other parts of the app domain
            // so they can get a reference to in memory compiled assemblies. 

            // TODO: Formalize a better way of doing this. Maybe DI?
            AppDomain.CurrentDomain.SetData("_compiledAssemblies", _compiledAssemblies);
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
            RoslynProject project;

            // Can't find a project file with the name so bail
            if (!TryGetProject(path, name, out project))
            {
                return null;
            }

            _watcher.Watch(path);
            _watcher.Watch(project.ProjectFilePath);

            foreach (var sourcePath in project.SourceFiles)
            {
                _watcher.Watch(Path.GetDirectoryName(sourcePath));
                _watcher.Watch(sourcePath);
            }

            var trees = project.SourceFiles.Select(p => SyntaxTree.ParseFile(p))
                                           .ToList();

            List<MetadataReference> references = null;

            if (project.Dependencies.Count > 0)
            {
                Trace.TraceInformation("Loading dependencies for '{0}'", project.Name);

                references = project.Dependencies
                                .Select(d =>
                                {
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
                                    catch (FileNotFoundException)
                                    {
                                        return MetadataFileReference.CreateAssemblyReference(d.Name);
                                    }
                                })
                                .Concat(GetFrameworkAssemblies())
                                .ToList();

                Trace.TraceInformation("Completed loading dependencies for '{0}'", project.Name);
            }
            else
            {
                references = GetFrameworkAssemblies().ToList();
            }

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

                var result = compilation.Emit(assemblyPath, pdbPath);

                if (!result.Success)
                {
                    ReportCompilationError(result);

                    return null;
                }

                Trace.TraceInformation("{0} -> {1}", name, assemblyPath);

                return Assembly.LoadFile(assemblyPath);
            }

            return CompileToMemoryStream(name, compilation);
        }

        private Assembly CompileToMemoryStream(string name, Compilation compilation)
        {
            using (var ms = new MemoryStream())
            {
                // TODO: Handle pdbs
                EmitResult result = compilation.Emit(ms);

                if (!result.Success)
                {
                    ReportCompilationError(result);

                    return null;
                }

                var bytes = ms.ToArray();

                var assembly = Assembly.Load(bytes);

                var compiled = Tuple.Create(assembly, (MetadataReference)new MetadataImageReference(bytes));

                _compiledAssemblies[name] = compiled;

                return assembly;
            }
        }

        private static void ReportCompilationError(EmitResult result)
        {
            throw new InvalidDataException(String.Join(Environment.NewLine, result.Diagnostics.Select(d => d.GetMessage())));
        }

        // The "framework" is always implicitly referenced
        private IEnumerable<MetadataReference> GetFrameworkAssemblies()
        {
            return new[] { 
                new MetadataFileReference(typeof(object).Assembly.Location) 
            };
        }

        private bool TryGetProject(string path, string name, out RoslynProject project)
        {
            // Can't find a project file with the name so bail
            if (!RoslynProject.TryGetProject(path, out project))
            {
                // Fall back to checking for any direct subdirectory that has
                // a settings file that matches this name
                foreach (var subDir in Directory.EnumerateDirectories(_solutionPath))
                {
                    if (RoslynProject.TryGetProject(subDir, out project) &&
                        String.Equals(project.Name, name, StringComparison.OrdinalIgnoreCase))
                    {
                        path = subDir;
                        break;
                    }
                    else
                    {
                        project = null;
                    }
                }
            }

            return project != null;
        }
    }

}
