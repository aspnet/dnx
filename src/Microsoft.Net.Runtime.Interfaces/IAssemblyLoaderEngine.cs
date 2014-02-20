using System.Reflection;

namespace Microsoft.Net.Runtime
{
    public interface IAssemblyLoaderEngine
    {
        Assembly LoadFile(string path);
        Assembly LoadBytes(byte[] assemblyBytes, byte[] pdbBytes);
    }
}
