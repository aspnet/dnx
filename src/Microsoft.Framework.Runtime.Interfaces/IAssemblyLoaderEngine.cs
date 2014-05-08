using System.Reflection;

namespace Microsoft.Framework.Runtime
{
    [AssemblyNeutral]
    public interface IAssemblyLoaderEngine
    {
        Assembly LoadFile(string path);
        Assembly LoadBytes(byte[] assemblyBytes, byte[] pdbBytes);
    }
}
