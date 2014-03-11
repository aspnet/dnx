#if NET45
using System.Reflection;

namespace klr.hosting
{
    public class LoaderEngine
    {
        public Assembly LoadFile(string path)
        {
            return Assembly.LoadFile(path);
        }

        public Assembly LoadBytes(byte[] assemblyBytes, byte[] pdbBytes)
        {
            return Assembly.Load(assemblyBytes, pdbBytes);
        }
    }
}
#endif