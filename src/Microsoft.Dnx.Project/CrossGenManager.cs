// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Microsoft.Dnx.Runtime;

namespace Microsoft.Dnx.Project
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

        public bool GenerateNativeImages()
        {
            // Generate a -> [closure]
            foreach (var assemblyInfo in _universe.Values)
            {
                var inputAssemblies = new[] { assemblyInfo };

                // All dependencies except this one
                assemblyInfo.Closure = Sort(inputAssemblies).Except(inputAssemblies)
                                                            .ToList();
            }

            bool success = true;

            // Generate the native images in dependency order
            foreach (var assemblyInfo in Sort(_universe.Values))
            {
                success = success && GenerateNativeImage(assemblyInfo);
            }
            
            return success;
        }
        
        private bool ExecuteCrossgen(string filename, string arguments, string assemblyName)
        {
            var options = new ProcessStartInfo
            {
                FileName = filename,
                Arguments = arguments,
                CreateNoWindow = true,
#if DNX451
                WindowStyle = ProcessWindowStyle.Hidden,
                UseShellExecute = false,
#endif
                RedirectStandardError = true,
                RedirectStandardOutput = true
            };

            if (!_options.Partial)
            {
                // Disable partial NGEN
#if DNX451
                options.EnvironmentVariables["COMPLUS_PartialNGen"] = "0";
#else
                options.Environment["COMPLUS_PartialNGen"] = "0";
#endif
            }

            var p = Process.Start(options);
#if DNX451
            p.EnableRaisingEvents = true;
#endif

            p.ErrorDataReceived += OnErrorDataReceived;
            p.OutputDataReceived += OnOutputDataReceived;

            p.BeginErrorReadLine();
            p.BeginOutputReadLine();
            p.WaitForExit();

            Console.WriteLine("Exit code for {0}: {1}", assemblyName, p.ExitCode);

            if (p.ExitCode == 0)
            {
                return true;
            }
            
            return false;
        }

        private bool GenerateNativeImage(AssemblyInformation assemblyInfo)
        {
            var retCrossgen = false;
        
            if (assemblyInfo.Closure.Any(a => !a.Generated))
            {
                Console.WriteLine("Skipping {0}. Because one or more dependencies failed to generate", assemblyInfo.Name);
                return false;
            }

            // Add the assembly itself to the closure
            var closure = assemblyInfo.Closure.Select(d => d.NativeImagePath)
                                      .Concat(new[] { assemblyInfo.AssemblyPath });

            Console.WriteLine("Generating native images for {0}", assemblyInfo.Name);

            const string crossgenArgsTemplate = @"/Nologo /in ""{0}"" /out ""{1}"" /MissingDependenciesOK /Trusted_Platform_Assemblies ""{2}""";

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
                                     String.Join(";", closure));
            }

            // Make sure the target directory for the native image is there
            Directory.CreateDirectory(Path.GetDirectoryName(assemblyInfo.NativeImagePath));
            
            retCrossgen = ExecuteCrossgen(_options.CrossgenPath, args, assemblyInfo.Name);
            if (retCrossgen)
            {
                assemblyInfo.Generated = true;
            }
            else
            {
                return false;
            }

            if (_options.Symbols)
            {
                Console.WriteLine("Generating native pdb for {0}", assemblyInfo.Name);

                const string crossgenArgsTemplateCreatePdb = @"/Nologo /CreatePDB ""{0}"" /in ""{1}"" /out ""{2}"" /Trusted_Platform_Assemblies ""{3}""";

                // crossgen.exe /CreatePDB {native-pdb-directory} /in {native-image-path}.dll /out {native-pdb-path} /Trusted_Platform_Assemblies {closure}
                string argsPdb = null;

                // Treat mscorlib specially
                if (assemblyInfo.Name.Equals("mscorlib", StringComparison.OrdinalIgnoreCase))
                {
                    argsPdb = String.Format(crossgenArgsTemplateCreatePdb,
                                         assemblyInfo.NativeImageDirectory,
                                         assemblyInfo.NativeImagePath,
                                         assemblyInfo.NativePdbPath,
                                         assemblyInfo.AssemblyPath);
                }
                else
                {
                    // Note: for CreatePDB need the native image (not the il image)
                    // Add the assembly itself to the closure
                    var closurePdb = assemblyInfo.Closure.Select(d => d.NativeImagePath)
                                              .Concat(new[] { assemblyInfo.NativeImagePath });

                    argsPdb = String.Format(crossgenArgsTemplateCreatePdb,
                                         assemblyInfo.NativeImageDirectory,
                                         assemblyInfo.NativeImagePath,
                                         assemblyInfo.NativePdbPath,
                                         String.Join(";", closurePdb));
                }
            
                retCrossgen = ExecuteCrossgen(_options.CrossgenPath, argsPdb, assemblyInfo.Name);
            }

            return retCrossgen;
        }

        void OnErrorDataReceived(object sender, DataReceivedEventArgs e)
        {
            if (!string.IsNullOrEmpty(e.Data))
            {
                Console.Error.WriteLine(e.Data);
            }
        }

        void OnOutputDataReceived(object sender, DataReceivedEventArgs e)
        {
            Console.WriteLine(e.Data);
        }

        private static IDictionary<string, AssemblyInformation> BuildUniverse(string runtimePath, IEnumerable<string> paths)
        {
            var activeRuntimePath = Path.GetFullPath(runtimePath);
            var procArch = ResolveProcessorArchitecture(Path.GetDirectoryName(activeRuntimePath));
            var runtimeAssemblies = Directory.EnumerateFiles(activeRuntimePath, "*.dll")
                                             .Where(AssemblyInformation.IsValidImage)
                                             .Select(path => new AssemblyInformation(path, procArch) { IsRuntimeAssembly = true });

            var otherAssemblies = paths.SelectMany(path => Directory.EnumerateFiles(path, "*.dll"))
                             .Where(AssemblyInformation.IsValidImage)
                             .Select(path => new AssemblyInformation(path, procArch));

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

        public static string ResolveProcessorArchitecture(string runtimePath)
        {
            var runtimeFullName = new DirectoryInfo(runtimePath).Name;
            var runtimeName = runtimeFullName.Substring(0, runtimeFullName.IndexOf('.'));
            var arch = runtimeName.Substring(runtimeName.LastIndexOf('-') + 1);
            return arch;
        }
    }
}
