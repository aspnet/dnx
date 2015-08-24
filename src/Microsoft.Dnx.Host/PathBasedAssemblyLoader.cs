// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Microsoft.Dnx.Runtime;

namespace Microsoft.Dnx.Host
{
    public class PathBasedAssemblyLoader : IAssemblyLoader
    {
        private static readonly string[] _extensions = new string[] { ".dll", ".exe" };

        private readonly IAssemblyLoadContext _loadContext;
        private readonly IEnumerable<string> _searchPaths;

        public PathBasedAssemblyLoader(IAssemblyLoadContextAccessor loadContextAccessor, IEnumerable<string> searchPaths)
        {
            _loadContext = loadContextAccessor.Default;
            _searchPaths = searchPaths;
        }

        public Assembly Load(AssemblyName assemblyName)
        {
            // searchPaths could be in the following patterns
            // C:\HelloWorld\bin\HelloWorld.dll or 
            // C:\HelloWorld\bin\HelloWorld.exe 
            // C:\HelloWorld\bin\fr-FR\HelloWorld.resources.dll
            // C:\HelloWorld\bin\fr-FR\HelloWorld.resources.exe
            foreach (var searchPath in _searchPaths)
            {
                var path = searchPath;
                if (!string.IsNullOrEmpty(assemblyName.CultureName))
                {
                    path = Path.Combine(path, assemblyName.CultureName);
                }

                foreach (var extension in _extensions)
                {
                    var filePath = Path.Combine(path, assemblyName.Name + extension);

                    if (File.Exists(filePath))
                    {
                        return _loadContext.LoadFile(filePath);
                    }
                }
            }

            return null;
        }
    }
}
