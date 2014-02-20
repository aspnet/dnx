using System.Reflection;

namespace klr.net45.managed
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
