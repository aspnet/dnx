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
            _loadContextAccessor = loadContextAccessor;
            _assemblies = PackageDependencyProvider.ResolvePackageAssemblyPaths(libraryManager);
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
    }
}
