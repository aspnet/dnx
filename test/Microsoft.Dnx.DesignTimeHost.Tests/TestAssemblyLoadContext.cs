// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Microsoft.Dnx.Runtime;

namespace Microsoft.Dnx.DesignTimeHost
{
    public class TestAssemblyLoadContext : IAssemblyLoadContext
    {
        private readonly IReadOnlyDictionary<string, Assembly> _assemblyNameLookups;

        public TestAssemblyLoadContext(IReadOnlyDictionary<string, Assembly> assemblyNameLookups)
        {
            _assemblyNameLookups = assemblyNameLookups;
        }

        public void Dispose()
        {
        }

        public virtual Assembly Load(AssemblyName assemblyName)
        {
            return _assemblyNameLookups[assemblyName.Name];
        }

        public Assembly LoadFile(string path)
        {
            throw new NotImplementedException();
        }

        public Assembly LoadStream(Stream assemblyStream, Stream assemblySymbols)
        {
            throw new NotImplementedException();
        }
    }
}