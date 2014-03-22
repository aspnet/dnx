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
            _universe = BuildUniverse(options.RuntimePath, options.InputPaths);
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

            const string crossgenArgsTemplate = "/in {0} /out {1} /MissingDependenciesOK /Trusted_Platform_Assemblies {2}";

            // crossgen.exe /in {il-path}.dll /out {native-image-path} /MissingDependenciesOK /Trusted_Platform_Assemblies {closure}
            string args = null;

            // Treat mscorlib specially
            if (assemblyInfo.Name.Equals("mscorlib", StringComparison.OrdinalIgnoreCase))
            {
                args = String.Format(crossgenArgsTemplate,
                                     assemblyInfo.AssemblyPath,
                                     assemblyInfo.NativeImagePath,
                                     assemblyInfo.AssemblyPath);
            }
            else
            {
                args = String.Format(crossgenArgsTemplate,
                                     assemblyInfo.AssemblyPath,
                                     assemblyInfo.NativeImagePath,
                                     String.Join(";", assemblyInfo.Closure.Select(d => d.NativeImagePath)));
            }

            // Make sure the target directory for the native image is there
            Directory.CreateDirectory(Path.GetDirectoryName(assemblyInfo.NativeImagePath));

            var options = new ProcessStartInfo
            {
                FileName = _options.CrossgenPath,
                Arguments = args,
                CreateNoWindow = true,
#if NET45
                WindowStyle = ProcessWindowStyle.Hidden,
                UseShellExecute = false,
#endif
                RedirectStandardError = true,
                RedirectStandardOutput = true
            };

            var p = Process.Start(options);
#if NET45
            p.EnableRaisingEvents = true;
#endif

            p.ErrorDataReceived += OnErrorDataReceived;
            p.OutputDataReceived += OnOutputDataReceived;

            p.BeginErrorReadLine();
            p.BeginOutputReadLine();
            p.WaitForExit();
            Console.WriteLine("Exit code for {0}: {1}", assemblyInfo.Name, p.ExitCode);
        }

        void OnErrorDataReceived(object sender, DataReceivedEventArgs e)
        {
            Console.Error.WriteLine(e.Data);
        }

        void OnOutputDataReceived(object sender, DataReceivedEventArgs e)
        {
            Console.WriteLine(e.Data);
        }

        private static IDictionary<string, AssemblyInformation> BuildUniverse(string runtimePath, IEnumerable<string> paths)
        {
            var runtimeAssemblies = Directory.EnumerateFiles(Path.GetFullPath(runtimePath), "*.dll")
                                             .Where(AssemblyInformation.IsValidImage)
                                             .Select(path => new AssemblyInformation(path) { IsRuntimeAssembly = true });

            var otherAssemblies = paths.SelectMany(path => Directory.EnumerateFiles(path, "*.dll"))
                             .Where(AssemblyInformation.IsValidImage)
                             .Select(path => new AssemblyInformation(path));

            var allAssemblies = runtimeAssemblies
                             .Concat(otherAssemblies)
                             .Distinct(AssemblyInformation.NameComparer);

            // REVIEW: Is this the correct way to deal with duplicate assembly names?
            return allAssemblies.ToDictionary(a => a.Name);
        }

        private IEnumerable<AssemblyInformation> Sort(IEnumerable<AssemblyInformation> input)
        {
            var output = new List<AssemblyInformation>();
            var seen = new HashSet<AssemblyInformation>();

            foreach (var node in input)
            {
                Sort(node, output, seen);
            }

            return output;
        }

        private void Sort(AssemblyInformation node, List<AssemblyInformation> output, HashSet<AssemblyInformation> seen)
        {
            if (!seen.Add(node))
            {
                return;
            }

            foreach (var dependency in node.GetDependencies())
            {
                AssemblyInformation dependencyInfo;
                if (_universe.TryGetValue(dependency, out dependencyInfo))
                {
                    Sort(dependencyInfo, output, seen);
                }
            }

            if (!output.Contains(node))
            {
                output.Add(node);
            }
        }
    }
}
