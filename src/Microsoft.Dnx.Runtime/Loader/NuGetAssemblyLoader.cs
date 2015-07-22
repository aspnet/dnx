// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Reflection;

namespace Microsoft.Dnx.Runtime.Loader
{
    public class NuGetAssemblyLoader : IAssemblyLoader
    {
        private readonly NuGetDependencyResolver _dependencyResolver;
        private readonly IAssemblyLoadContextAccessor _loadContextAccessor;

        public NuGetAssemblyLoader(IAssemblyLoadContextAccessor loadContextAccessor,
                                   NuGetDependencyResolver dependencyResolver)
        {
            _dependencyResolver = dependencyResolver;
            _loadContextAccessor = loadContextAccessor;
        }

        public Assembly Load(AssemblyName assemblyName)
        {
            return Load(assemblyName, _loadContextAccessor.Default);
        }

        public Assembly Load(AssemblyName assemblyName, IAssemblyLoadContext loadContext)
        {
            // TODO: preserve name and culture info (we don't need to look at any other information)
            PackageAssembly assemblyInfo;
            if (_dependencyResolver.PackageAssemblyLookup.TryGetValue(new AssemblyName(assemblyName.Name), out assemblyInfo))
            {
                return loadContext.LoadFile(assemblyInfo.Path);
            }

            return null;
        }
    }
}
