using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace Microsoft.Net.Project
{
    public class CrossgenManager
    {
        private readonly IDictionary<string, AssemblyInformation> _universe;
        private readonly CrossgenOptions _options;

        public CrossgenManager(CrossgenOptions options)
        {
            _options = options;
            _universe = BuildUniverse(options.InputPaths);
        }

        public void GenerateNativeImages()
        {
            // Generate a -> [closure]
            foreach (var assemblyInfo in _universe.Values)
            {
                var inputAssemblies = new[] { assemblyInfo };

                // All dependencies except this one
                assemblyInfo.Closure = Sort(inputAssemblies).Except(inputAssemblies)
                                                            .ToList();
            }

            // Generate the native images in dependency order
            foreach (var assemblyInfo in Sort(_universe.Values))
            {
                GenerateNativeImage(assemblyInfo);
            }
        }

        private void GenerateNativeImage(AssemblyInformation assemblyInfo)
        {
            Console.WriteLine("Generating native images for {0}", assemblyInfo.Name);

            const string crossGenArgsTemplate = "/in {0} /out {1} /MissingDependenciesOK /Trusted_Platform_Assemblies {2}";

            // crossgen.exe /in {il-path}.dll /out {native-image-path} /MissingDependenciesOK /Trusted_Platform_Assemblies {closure}
            string args = null;

            if (assemblyInfo.Name.Equals("mscorlib", StringComparison.OrdinalIgnoreCase))
            {
                args = String.Format(crossGenArgsTemplate,
                                     assemblyInfo.AssemblyPath,
                                     assemblyInfo.NativeImagePath,
                                     assemblyInfo.AssemblyPath);
            }
            else
            {
                args = String.Format(crossGenArgsTemplate,
                                     assemblyInfo.AssemblyPath,
                                     assemblyInfo.NativeImagePath,
                                     String.Join(";", assemblyInfo.Closure.Select(d => d.NativeImagePath)));
            }

            var options = new ProcessStartInfo
            {
                FileName = _options.CrossgenPath,
                Arguments = args,
                CreateNoWindow = true,
#if NET45
                WindowStyle = ProcessWindowStyle.Hidden,
                UseShellExecute = false,
#endif
            };

            var p = Process.Start(options);
            p.WaitForExit();
        }

        private static IDictionary<string, AssemblyInformation> BuildUniverse(IEnumerable<string> paths)
        {
            // REVIEW: Is this the correct way to deal with duplicate assembly names?
            return paths.SelectMany(path => Directory.EnumerateFiles(path, "*.dll"))
                             .Where(AssemblyInformation.IsValidImage)
                             .Select(path => new AssemblyInformation(path))
                             .Distinct(AssemblyInformation.NameComparer)
                             .ToDictionary(a => a.Name);
        }

        private IEnumerable<AssemblyInformation> Sort(IEnumerable<AssemblyInformation> input)
        {
            var output = new List<AssemblyInformation>();

            foreach (var node in input)
            {
                Sort(node, output);
            }

            return output;
        }

        private void Sort(AssemblyInformation node, List<AssemblyInformation> output)
        {
            // TODO: Cycle check?

            foreach (var dependency in node.GetDependencies(_universe))
            {
                Sort(dependency, output);
            }

            if (!output.Contains(node))
            {
                output.Add(node);
            }
        }
    }
}
