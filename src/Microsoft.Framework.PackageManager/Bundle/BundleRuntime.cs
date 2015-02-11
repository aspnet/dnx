// Copyright (c) Microsoft Open Technologies, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

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
                var assemblyCopyFlag = new Dictionary<string, bool>();
                foreach (var assembly in Directory.EnumerateFiles(_runtimeBinPath, "*.dll"))
                {
                    assemblyCopyFlag.Add(Path.GetFileNameWithoutExtension(assembly), false);
                }

                var runtimeBasicManagedAssemblyNames = new List<string> { Runtime.Constants.BootstrapperHostName };

                if (VersionUtility.IsDesktop(Framework))
                {
                    runtimeBasicManagedAssemblyNames.Add(Runtime.Constants.BootstrapperClrManagedName);
                }
                else
                {
                    runtimeBasicManagedAssemblyNames.Add(Runtime.Constants.BootstrapperCoreclrManagedName);
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

                var extensions = new[] { ".dll", ".pdb", ".xml" };
                foreach (var assemblyName in assemblyCopyFlag.Where(x => x.Value).Select(x => x.Key))
                {
                    foreach (var extension in extensions)
                    {
                        var fileName = assemblyName + extension;
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
            var rootAssembly = assemblyLoadContext.LoadFile(rootAssemblyPath);
            foreach (var reference in rootAssembly.GetReferencedAssemblies())
            {
                bool marked;
                if (assemblyCopyFlag.TryGetValue(reference.Name, out marked) && !marked)
                {
                    assemblyCopyFlag[reference.Name] = true;
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