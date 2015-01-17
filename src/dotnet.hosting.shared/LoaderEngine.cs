// Copyright (c) Microsoft Open Technologies, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

#if ASPNET50
using System.IO;
using System.Reflection;

namespace dotnet.hosting
{
    public class LoaderEngine
    {
        public Assembly LoadFile(string path)
        {
            return Assembly.LoadFile(path);
        }

        public Assembly LoadStream(Stream assemblyStream, Stream assemblySymbols)
        {
            byte[] assemblyBytes = GetStreamAsByteArray(assemblyStream);
            byte[] assemblySymbolBytes = null;

            if (assemblySymbols != null)
            {
                assemblySymbolBytes = GetStreamAsByteArray(assemblySymbols);
            }

            return Assembly.Load(assemblyBytes, assemblySymbolBytes);
        }

        private byte[] GetStreamAsByteArray(Stream stream)
        {
            // Fast path assuming the stream is a memory stream
            var ms = stream as MemoryStream;
            if (ms != null)
            {
                return ms.ToArray();
            }

            // Otherwise copy the bytes
            using (ms = new MemoryStream())
            {
                stream.CopyTo(ms);
                return ms.ToArray();
            }
        }
    }
}
#endif
