// Copyright (c) Microsoft Open Technologies, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Versioning;
using Microsoft.Framework.Runtime;
using NuGet;

namespace Microsoft.Framework.PackageManager.Bundle
{
    public class BundleRuntime
    {
        private readonly FrameworkName _frameworkName;
        private readonly string _runtimePath;
        private readonly string _runtimeBinPath;

        public BundleRuntime(BundleRoot root, FrameworkName frameworkName, string runtimePath)
        {
            _frameworkName = frameworkName;
            _runtimePath = runtimePath;
            _runtimeBinPath = Path.Combine(runtimePath, "bin");
            Name = new DirectoryInfo(_runtimePath).Name;
            TargetPath = Path.Combine(root.TargetPackagesPath, Name);
        }

        public string Name { get; private set; }
        public string TargetPath { get; private set; }
        public FrameworkName Framework { get { return _frameworkName; } }

        public void Emit(BundleRoot root)
        {
            root.Reports.Quiet.WriteLine("Bundling runtime {0}", Name);

            if (root.OneFolder)
            {
                var assemblyNames = GetMinimalRuntimeAssemblyClosure(root);
                var extensions = new[] { ".dll", ".pdb", ".xml" };
                foreach (var name in assemblyNames)
                {
                    foreach (var extension in extensions)
                    {
                        var fileName = name + extension;
                        var path = Path.Combine(_runtimeBinPath, fileName);
                        var dest = Path.Combine(root.OutputPath, fileName);

                        // If the assembly is already there, we don't overwrite it.
                        // So if we have two assemblies with exact same name, one from NuGet package and
                        // the other one from runtime bin folder, we prefer the one from NuGet package.
                        if (File.Exists(path) && !File.Exists(dest))
                        {
                            File.Copy(path, dest);
                        }
                    }
                }

                var extraFilesToCopy = new List<string> { Runtime.Constants.BootstrapperExeName + ".exe" };
                if (VersionUtility.IsDesktop(Framework))
                {
                    extraFilesToCopy.Add(Runtime.Constants.BootstrapperClrName + ".config");
                }

                foreach (var fileName in extraFilesToCopy)
                {
                    var path = Path.Combine(_runtimeBinPath, fileName);
                    var dest = Path.Combine(root.OutputPath, fileName);
                    File.Copy(path, dest, overwrite: true);
                }

                return;
            }

            if (Directory.Exists(TargetPath))
            {
                root.Reports.Quiet.WriteLine("  {0} already exists.", TargetPath);
                return;
            }

            if (!Directory.Exists(TargetPath))
            {
                Directory.CreateDirectory(TargetPath);
            }

            new BundleOperations().Copy(_runtimePath, TargetPath);

            if (PlatformHelper.IsMono)
            {
                // Executable permissions on klr lost on copy. 
                var klrPath = Path.Combine(TargetPath, "bin", "klr");
                FileOperationUtils.MarkExecutable(klrPath, root.Reports);
            }
        }

        private IEnumerable<string> GetMinimalRuntimeAssemblyClosure(BundleRoot root)
        {
            var assemblyCopyFlag = new Dictionary<string, bool>();
            foreach (var assembly in Directory.EnumerateFiles(_runtimeBinPath, "*.dll"))
            {
                assemblyCopyFlag.Add(Path.GetFileNameWithoutExtension(assembly), false);
            }

            var runtimeBasicManagedAssemblyNames = new List<string> { Runtime.Constants.BootstrapperHostName };
            var runtimeBasicNativeAssemblyNames = new List<string>();

            if (VersionUtility.IsDesktop(Framework))
            {
                runtimeBasicManagedAssemblyNames.Add(Runtime.Constants.BootstrapperClrManagedName);
                runtimeBasicNativeAssemblyNames.Add(Runtime.Constants.BootstrapperClrName);
            }
            else
            {
                runtimeBasicManagedAssemblyNames.Add(Runtime.Constants.BootstrapperCoreclrManagedName);
                runtimeBasicNativeAssemblyNames.Add(Runtime.Constants.BootstrapperCoreclrName);
                runtimeBasicNativeAssemblyNames.Add("coreclr");
            }

            var runtimeBasicManagedAssemblyPaths = runtimeBasicManagedAssemblyNames
                .Select(x => Path.Combine(_runtimeBinPath, x + ".dll"));

            var bundledAppAssemblyPaths = Directory.EnumerateFiles(root.OutputPath, "*.dll");

            var accessor = root.HostServices.GetService(typeof(IAssemblyLoadContextAccessor)) as IAssemblyLoadContextAccessor;
            var assemblyLoadContext = accessor.Default;
            foreach (var assemblyPath in runtimeBasicManagedAssemblyPaths.Concat(bundledAppAssemblyPaths))
            {
                MarkAssembliesToCopy(assemblyPath, assemblyCopyFlag, assemblyLoadContext);
            }

            return assemblyCopyFlag.Where(x => x.Value).Select(x => x.Key).Concat(runtimeBasicNativeAssemblyNames);
        }

        private void MarkAssembliesToCopy(string rootAssemblyPath, Dictionary<string, bool> assemblyCopyFlag,
            IAssemblyLoadContext assemblyLoadContext)
        {
#if ASPNETCORE50
            // TODO: remove this workaround after we have Assembly.GetReferencedAssemblies() on CoreCLR
            foreach (var key in assemblyCopyFlag.Keys)
            {
                assemblyCopyFlag[key] = true;
            }
#else
            var rootAssemblyName = Path.GetFileNameWithoutExtension(rootAssemblyPath);
            assemblyCopyFlag[rootAssemblyName] = true;

            var rootAssembly = assemblyLoadContext.LoadFile(rootAssemblyPath);
            foreach (var reference in rootAssembly.GetReferencedAssemblies())
            {
                bool marked;
                if (assemblyCopyFlag.TryGetValue(reference.Name, out marked) && !marked)
                {
                    MarkAssembliesToCopy(
                        Path.Combine(_runtimeBinPath, reference.Name + ".dll"),
                        assemblyCopyFlag,
                        assemblyLoadContext);
                }
            }
#endif
        }
    }
}