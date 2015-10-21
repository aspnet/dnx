// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.IO;
using System.Reflection;
using Microsoft.Dnx.Runtime;
using Microsoft.Extensions.PlatformAbstractions;

namespace Microsoft.Dnx.DesignTimeHost
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

        public Assembly Load(AssemblyName assemblyName)
        {
            throw new NotSupportedException();
        }

        public IntPtr LoadUnmanagedLibrary(string name)
        {
            throw new NotImplementedException();
        }

        public IntPtr LoadUnmanagedLibraryFromPath(string path)
        {
            throw new NotImplementedException();
        }

        public void Dispose()
        {
        }

        public byte[] AssemblyBytes { get; set; }
        public byte[] PdbBytes { get; set; }

        public string AssemblyPath { get; set; }
    }
}