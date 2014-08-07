// Copyright (c) Microsoft Open Technologies, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Microsoft.Framework.Runtime;

namespace Microsoft.Framework.Project
{
    public class CrossgenManager
    {
        private readonly IDictionary<string, AssemblyInformation> _universe;
        private readonly CrossgenOptions _options;

        public CrossgenManager(CrossgenOptions options)
        {
            _options = options;

            var crossgenPaths = options.InputPaths;

            if (options.Packages)
            {
                Console.WriteLine("Crossgen will include all package dependencies");
                var packageRoot = NuGetDependencyResolver.ResolveRepositoryPath(Directory.GetCurrentDirectory());
                var packageDirectories = Directory.EnumerateDirectories(packageRoot, "k10", SearchOption.AllDirectories);
                crossgenPaths = crossgenPaths.Concat(packageDirectories);
            }

            _universe = BuildUniverse(options.RuntimePath, crossgenPaths);
        }

        public CrossgenResult GenerateNativeImages()
        {
            // Generate a -> [closure]
            foreach (var assemblyInfo in _universe.Values)
            {
                var inputAssemblies = new[] { assemblyInfo };

                // All dependencies except this one
                assemblyInfo.Closure = Sort(inputAssemblies).Except(inputAssemblies)
                                                            .ToList();
            }

            var crossgenResult = new CrossgenResult();

            // Generate the native images in dependency order
            foreach (var assemblyInfo in Sort(_universe.Values))
            {
                GenerateNativeImage(assemblyInfo, crossgenResult);
                if (crossgenResult.Failed)
                {
                    break;
                }
            }
            
            return crossgenResult;
        }
        
        private bool ExecuteCrossgen(string filename, string arguments, string assemblyName)
        {
            var options = new ProcessStartInfo
            {
                FileName = filename,
                Arguments = arguments,
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

            Console.WriteLine("Exit code for {0}: {1}", assemblyName, p.ExitCode);

            if (p.ExitCode == 0)
            {
                return true;
            }
            
            return false;
        }

        private void GenerateNativeImage(AssemblyInformation assemblyInfo, CrossgenResult result)
        {
            var retCrossgen = false;

            // Add the assembly itself to the closure
            var closure = assemblyInfo.Closure
                            .Where(a =>
                            {
                                if (a.Generated)
                                {
                                    return true;
                                }
                                else
                                {
                                    result.Warn();
                                    Console.WriteLine(string.Format("WARNING: {0} depends on {1}. Dependency's native image failed to generate", assemblyInfo.Name, a.Name));
                                    return false;
                                }
                            })
                            .Select(d => d.NativeImagePath)
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
                result.Fail();
                return;
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

            if (!retCrossgen)
            {
                result.Fail();
            }
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
