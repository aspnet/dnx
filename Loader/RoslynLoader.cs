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
        private readonly Dictionary<string, CompiledAssembly> _compiledAssemblies = new Dictionary<string, CompiledAssembly>();
        private readonly string _solutionPath;

        public RoslynLoader(string solutionPath)
        {
            _solutionPath = solutionPath;
        }

        public Assembly Load(string name)
        {
            CompiledAssembly compiledAssembly;
            if (_compiledAssemblies.TryGetValue(name, out compiledAssembly))
            {
                return compiledAssembly.Assembly;
            }

            string path = Path.Combine(_solutionPath, name);
            ProjectSettings settings;

            // Can't find a project file with the name so bail
            if (!ProjectSettings.TryGetSettings(path, out settings))
            {
                return null;
            }

            // Get all the cs files in this directory
            var sources = Directory.GetFiles(path, "*.cs", SearchOption.AllDirectories);

            var trees = sources.Select(p => SyntaxTree.ParseFile(p))
                               .ToList();

            var references = settings.Dependencies
                            .Select(d =>
                            {
                                // Load the assembly so we run the "system" but grab the bytes for those assemblies
                                // that are in memory
                                var loadedAssembly = Assembly.Load(d.Name);

                                CompiledAssembly compiledDependency;
                                if (_compiledAssemblies.TryGetValue(d.Name, out compiledDependency))
                                {
                                    return compiledDependency.Reference;
                                }

                                return new MetadataFileReference(loadedAssembly.Location);
                            })
                            .Concat(GetFrameworkAssemblies())
                            .ToList();

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

                var compiled = new CompiledAssembly
                {
                    Assembly = assembly,
                    Reference = new MetadataImageReference(bytes)
                };

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
        }

        private class CompiledAssembly
        {
            public Assembly Assembly { get; set; }
            public MetadataReference Reference { get; set; }
        }
    }

}
