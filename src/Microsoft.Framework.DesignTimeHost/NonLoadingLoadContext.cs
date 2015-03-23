// Copyright (c) Microsoft Open Technologies, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.IO;
using System.Reflection;
using Microsoft.Framework.Runtime;

namespace Microsoft.Framework.DesignTimeHost
{
    public class NonLoadingLoadContext : IAssemblyLoadContext
    {
        public Assembly LoadFile(string path)
        {
            AssemblyPath = path;
            return null;
        }

        public Assembly LoadStream(Stream assemblyStream, Stream pdbStream)
        {
            var ms = new MemoryStream((int)assemblyStream.Length);
            assemblyStream.CopyTo(ms);
            AssemblyBytes = ms.ToArray();

            if (pdbStream != null)
            {
                ms = new MemoryStream((int)assemblyStream.Length);
                pdbStream.CopyTo(ms);
                PdbBytes = ms.ToArray();
            }

            return null;
        }

        public Assembly Load(string name)
        {
            throw new NotSupportedException();
        }

        public Assembly Load(AssemblyName assemblyName)
        {
            throw new NotSupportedException();
        }

        public void Dispose()
        {

        }

        public byte[] AssemblyBytes { get; set; }
        public byte[] PdbBytes { get; set; }

        public string AssemblyPath { get; set; }
    }
}