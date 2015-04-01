// Copyright (c) Microsoft Open Technologies, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Microsoft.Framework.Runtime;

namespace Microsoft.Framework.DesignTimeHost
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

        public virtual Assembly Load(string name)
        {
            return _assemblyNameLookups[name];
        }

        public virtual Assembly Load(AssemblyName assemblyName)
        {
            return Load(assemblyName.Name);
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