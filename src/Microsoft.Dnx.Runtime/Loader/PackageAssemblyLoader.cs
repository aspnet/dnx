// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Reflection;

namespace Microsoft.Dnx.Runtime.Loader
{
    public class PackageAssemblyLoader : IAssemblyLoader
    {
        private readonly IDictionary<AssemblyName, string> _assemblies;
        private readonly IAssemblyLoadContextAccessor _loadContextAccessor;

        public PackageAssemblyLoader(IAssemblyLoadContextAccessor loadContextAccessor,
                                     IDictionary<AssemblyName, string> assemblies)
        {
            _loadContextAccessor = loadContextAccessor;
            _assemblies = assemblies;
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
    }
}
