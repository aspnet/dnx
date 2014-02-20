using System;
using System.Reflection;
using Microsoft.Net.Runtime;

namespace Microsoft.Net.Project
{
    public class NoopLoaderEngine : IAssemblyLoaderEngine
    {
        public Assembly LoadFile(string path)
        {
            throw new NotImplementedException();
        }

        public Assembly LoadBytes(byte[] assemblyBytes, byte[] pdbBytes)
        {
            throw new NotImplementedException();
        }
    }
}
