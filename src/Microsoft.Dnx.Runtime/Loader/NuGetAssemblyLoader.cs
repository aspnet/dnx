// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;

namespace Microsoft.Dnx.Runtime.Loader
{
    public class NuGetAssemblyLoader : IAssemblyLoader
    {
        private readonly Dictionary<AssemblyName, string> _assemblies;
        private readonly IAssemblyLoadContextAccessor _loadContextAccessor;

        public NuGetAssemblyLoader(IAssemblyLoadContextAccessor loadContextAccessor,
                                   LibraryManager libraryManager)
        {
            _assemblies = ResolveAssemblyPaths(libraryManager);
            _loadContextAccessor = loadContextAccessor;
        }

        public static Dictionary<AssemblyName, string> ResolveAssemblyPaths(LibraryManager libraryManager)
        {
            var assemblies = new Dictionary<AssemblyName, string>(new AssemblyNameComparer());

            foreach (var library in libraryManager.GetLibraryDescriptions())
            {
                if (library.Type != LibraryTypes.Package)
                {
                    foreach (var runtimeAssemblyPath in ((PackageDescription)library).Target.RuntimeAssemblies)
                    {
                        // Fix up the slashes to match the platform
                        var assemblyPath = runtimeAssemblyPath.Path.Replace('/', Path.DirectorySeparatorChar);
                        var name = Path.GetFileNameWithoutExtension(assemblyPath);
                        var path = Path.Combine(library.Path, assemblyPath);
                        var assemblyName = new AssemblyName(name);

                        string replacementPath;
                        if (Servicing.ServicingTable.TryGetReplacement(
                            library.Identity.Name,
                            library.Identity.Version,
                            assemblyPath,
                            out replacementPath))
                        {
                            assemblies[assemblyName] = replacementPath;
                        }
                        else
                        {
                            assemblies[assemblyName] = path;
                        }
                    }
                }
            }

            return assemblies;
        }

        public Assembly Load(AssemblyName assemblyName)
        {
            return Load(assemblyName, _loadContextAccessor.Default);
        }

        public Assembly Load(AssemblyName assemblyName, IAssemblyLoadContext loadContext)
        {
            // TODO: preserve name and culture info (we don't need to look at any other information)
            string path;
            if (_assemblies.TryGetValue(new AssemblyName(assemblyName.Name), out path))
            {
                return loadContext.LoadFile(path);
            }

            return null;
        }

        private class AssemblyNameComparer : IEqualityComparer<AssemblyName>
        {
            public static IEqualityComparer<AssemblyName> OrdinalIgnoreCase = new AssemblyNameComparer();

            public bool Equals(AssemblyName x, AssemblyName y)
            {
                return
                    string.Equals(x.Name, y.Name, StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(x.CultureName ?? "", y.CultureName ?? "", StringComparison.OrdinalIgnoreCase);
            }

            public int GetHashCode(AssemblyName obj)
            {
                var hashCode = 0;
                if (obj.Name != null)
                {
                    hashCode ^= obj.Name.ToUpperInvariant().GetHashCode();
                }

                hashCode ^= (obj.CultureName?.ToUpperInvariant() ?? "").GetHashCode();
                return hashCode;
            }
        }
    }
}
