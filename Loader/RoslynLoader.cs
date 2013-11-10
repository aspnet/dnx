using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
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
            ProjectSettings settings;

            // Can't find a project file with the name so bail
            if (!TryGetProjectSettings(path, name, out settings))
            {
                return null;
            }

            _watcher.Watch(path);
            _watcher.Watch(settings.ProjectFilePath);

            // Get all the cs files in this directory
            var sources = Directory.GetFiles(path, "*.cs", SearchOption.AllDirectories);

            foreach (var sourcePath in sources)
            {
                _watcher.Watch(Path.GetDirectoryName(sourcePath));
                _watcher.Watch(sourcePath);
            }

            var trees = sources.Select(p => SyntaxTree.ParseFile(p))
                               .ToList();

            Trace.TraceInformation("Loading dependencies for '{0}'", settings.Name);

            var references = settings.Dependencies
                            .Select(d =>
                            {
                                var loadedAssembly = Assembly.Load(d.Name);

                                Tuple<Assembly, MetadataReference> compiledDependency;
                                if (_compiledAssemblies.TryGetValue(d.Name, out compiledDependency))
                                {
                                    return compiledDependency.Item2;
                                }

                                return new MetadataFileReference(loadedAssembly.Location);
                            })
                            .Concat(GetFrameworkAssemblies())
                            .ToList();

            Trace.TraceInformation("Completed loading dependencies for '{0}'", settings.Name);

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
                var result = compilation.Emit(assemblyPath);

                if (!result.Success)
                {
                    ReportCompilationError(result);

                    return null;
                }

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
            Trace.TraceError("COMPILATION ERROR: " +
                Environment.NewLine +
                String.Join(Environment.NewLine,
                result.Diagnostics.Select(d => d.GetMessage())));
        }

        // The "framework" is always implicitly referenced
        private IEnumerable<MetadataReference> GetFrameworkAssemblies()
        {
            yield return new MetadataFileReference(typeof(object).Assembly.Location);
            yield return new MetadataFileReference(typeof(File).Assembly.Location);
            yield return new MetadataFileReference(typeof(System.Linq.Enumerable).Assembly.Location);
            yield return new MetadataFileReference(typeof(System.Dynamic.DynamicObject).Assembly.Location);
            yield return new MetadataFileReference(typeof(Microsoft.CSharp.RuntimeBinder.RuntimeBinderException).Assembly.Location);
            yield return new MetadataFileReference(typeof(IListSource).Assembly.Location);
            yield return new MetadataFileReference(typeof(RequiredAttribute).Assembly.Location);
        }

        private bool TryGetProjectSettings(string path, string name, out ProjectSettings settings)
        {
            // Can't find a project file with the name so bail
            if (!ProjectSettings.TryGetSettings(path, out settings))
            {
                // Fall back to checking for any direct subdirectory that has
                // a settings file that matches this name
                foreach (var subDir in Directory.EnumerateDirectories(_solutionPath))
                {
                    if (ProjectSettings.TryGetSettings(subDir, out settings) &&
                        String.Equals(settings.Name, name, StringComparison.OrdinalIgnoreCase))
                    {
                        path = subDir;
                        break;
                    }
                    else
                    {
                        settings = null;
                    }
                }
            }

            return settings != null;
        }
    }

}
