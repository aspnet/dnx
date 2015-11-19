// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Microsoft.Extensions.PlatformAbstractions;

namespace Microsoft.Dnx.Runtime.Loader
{
    public class PackageAssemblyLoader : IAssemblyLoader
    {
        private readonly IDictionary<AssemblyName, string> _assemblies;
        private readonly IAssemblyLoadContextAccessor _loadContextAccessor;
        private Dictionary<string, string> _nativeLibraryPaths = new Dictionary<string, string>();

        public PackageAssemblyLoader(IAssemblyLoadContextAccessor loadContextAccessor,
                                     IDictionary<AssemblyName, string> assemblies,
                                     IEnumerable<LibraryDescription> libraryDescriptions)
        {
            _loadContextAccessor = loadContextAccessor;
            _assemblies = assemblies;

            foreach (var packageDescription in libraryDescriptions.OfType<PackageDescription>())
            {
                foreach (var nativeLib in packageDescription.Target.NativeLibraries)
                {
                    _nativeLibraryPaths[Path.GetFileNameWithoutExtension(nativeLib.Path)] =
                        Path.Combine(packageDescription.Path, nativeLib.Path);
                }
            }
        }

        public Assembly Load(AssemblyName assemblyName)
        {
            return Load(assemblyName, _loadContextAccessor.Default);
        }

        public Assembly Load(AssemblyName assemblyName, IAssemblyLoadContext loadContext)
        {
            string path;
            var newAssemblyName = new AssemblyName(assemblyName.Name);

#if DNXCORE50
            newAssemblyName.CultureName = assemblyName.CultureName;
#elif DNX451
            // Assigning empty CultureInfo makes the new assembly culture as neutral which won't match the entries in _assemblies dictionary. Hence this check.
            if (assemblyName.CultureInfo != null && !ResourcesHelper.IsResourceNeutralCulture(assemblyName))
            {
                 newAssemblyName.CultureInfo = assemblyName.CultureInfo;
            }
#else
#error Unhandled framework error
#endif
            if (_assemblies.TryGetValue(newAssemblyName, out path))
            {
                return loadContext.LoadFile(path);
            }

            return null;
        }

        public IntPtr LoadUnmanagedLibrary(string name)
        {
#if DNXCORE50
            string path;
            if (_nativeLibraryPaths.TryGetValue(Path.GetFileNameWithoutExtension(name), out path))
            {
                return _loadContextAccessor.Default.LoadUnmanagedLibraryFromPath(path);
            }
#endif
            return IntPtr.Zero;
        }
    }
}
