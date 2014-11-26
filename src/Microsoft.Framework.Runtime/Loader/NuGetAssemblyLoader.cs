// Copyright (c) Microsoft Open Technologies, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Reflection;

namespace Microsoft.Framework.Runtime.Loader
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

        public Assembly Load(string name)
        {
            return Load(name, _loadContextAccessor.Default);
        }

        public Assembly Load(string name, IAssemblyLoadContext loadContext)
        {
            PackageAssembly assemblyInfo;
            if (_dependencyResolver.PackageAssemblyLookup.TryGetValue(name, out assemblyInfo))
            {
                return loadContext.LoadFile(assemblyInfo.Path);
            }

            return null;
        }
    }
}
