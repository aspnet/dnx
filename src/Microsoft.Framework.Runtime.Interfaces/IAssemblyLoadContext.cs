using System;
using System.IO;
using System.Reflection;

namespace Microsoft.Framework.Runtime
{
    public interface IAssemblyLoadContext : IDisposable
    {
        Assembly Load(string name);
        Assembly LoadFile(string path);
        Assembly LoadStream(Stream assemblyStream, Stream assemblySymbols);
    }
}