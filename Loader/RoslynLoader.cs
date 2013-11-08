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

        public RoslynLoader(string solutionPath)
        {
            _solutionPath = solutionPath;

            // HACK: We're making this available to other parts of the app domain
            // so they can get a reference to in memory compiled assemblies. 

            // TODO: Formalize a better way of doing this. Maybe DI?
            AppDomain.CurrentDomain.SetData("_compiledAssemblies", _compiledAssemblies);
        }

        public Assembly Load(string name)
        {
            Tuple<Assembly, MetadataReference> compiledAssembly;
            if (_compiledAssemblies.TryGetValue(name, out compiledAssembly))
            {
                return compiledAssembly.Item1;
            }

            string path = Path.Combine(_solutionPath, name);
            ProjectSettings settings;

            // Can't find a project file with the name so bail
            if (!ProjectSettings.TryGetSettings(path, out settings))
            {
                // Fall back to checking for any direct subdirectory that has
                // a project.json that matches this name
                foreach (var subDir in Directory.EnumerateDirectories(_solutionPath))
                {
                    if (ProjectSettings.TryGetSettings(subDir, out settings) &&
                        settings.Name == name)
                    {
                        path = subDir;
                        break;
                    }
                    else
                    {
                        settings = null;
                    }
                }

                // Couldn't find anything
                if (settings == null)
                {
                    return null;
                }
            }

            // Get all the cs files in this directory
            var sources = Directory.GetFiles(path, "*.cs", SearchOption.AllDirectories);

            var trees = sources.Select(p => SyntaxTree.ParseFile(p))
                               .ToList();

            Trace.TraceInformation("Loading dependencies for '{0}'", settings.Name);

            var references = settings.Dependencies
                            .Select(d =>
                            {
                                // Load the assembly so we run the "system" but grab the bytes for those assemblies
                                // that are in memory
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

            string generatedAssemblyName = GetAssemblyName(path);

            // Create a compilation
            var compilation = Compilation.Create(
                name,
                new CompilationOptions(OutputKind.DynamicallyLinkedLibrary),
                syntaxTrees: trees,
                references: references);

            using (var ms = new MemoryStream())
            {
                EmitResult result = compilation.Emit(ms);

                if (!result.Success)
                {
                    Trace.TraceError("COMPILATION ERROR: " +
                        Environment.NewLine +
                        String.Join(Environment.NewLine,
                        result.Diagnostics.Select(d => d.GetMessage())));

                    return null;
                }

                var bytes = ms.ToArray();

                var assembly = Assembly.Load(bytes);

                var compiled = Tuple.Create(assembly, (MetadataReference)new MetadataImageReference(bytes));
                
                _compiledAssemblies[generatedAssemblyName] = compiled;
                _compiledAssemblies[name] = compiled;

                return assembly;
            }
        }

        private string GetAssemblyName(string path)
        {
            return "COMPILED_" + Normalize(path);
        }

        private static string Normalize(string path)
        {
            return path.Replace(':', '_').Replace(Path.DirectorySeparatorChar, '_');
        }

        // The "framework" is always implicitly referenced
        public IEnumerable<MetadataReference> GetFrameworkAssemblies()
        {
            yield return new MetadataFileReference(typeof(object).Assembly.Location);
            yield return new MetadataFileReference(typeof(File).Assembly.Location);
            yield return new MetadataFileReference(typeof(System.Linq.Enumerable).Assembly.Location);
            yield return new MetadataFileReference(typeof(System.Dynamic.DynamicObject).Assembly.Location);
            yield return new MetadataFileReference(typeof(Microsoft.CSharp.RuntimeBinder.RuntimeBinderException).Assembly.Location);
            yield return new MetadataFileReference(typeof(IListSource).Assembly.Location);
            yield return new MetadataFileReference(typeof(RequiredAttribute).Assembly.Location);
        }
    }

}
