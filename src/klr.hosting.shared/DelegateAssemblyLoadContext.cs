#if K10
using System;
using System.IO;
using System.Reflection;
using System.Runtime.Loader;

namespace klr.hosting
{
    public class DelegateAssemblyLoadContext : AssemblyLoadContext
    {
        private Func<AssemblyName, Assembly> _loaderCallback;

        public DelegateAssemblyLoadContext(Func<AssemblyName, Assembly> loaderCallback)
        {
            _loaderCallback = loaderCallback;
        }

        protected override Assembly Load(AssemblyName assemblyName)
        {
            return _loaderCallback(assemblyName);
        }

        public Assembly LoadFile(string path)
        {
            // Look for platform specific native image
            string nativeImagePath = GetNativeImagePath(path);

            if (File.Exists(nativeImagePath))
            {
                // TODO: Remove this tracing
                Console.WriteLine("Information: LoadFromFileWithNativeImage({0})", nativeImagePath);
                return LoadFromNativeImagePath(nativeImagePath, path);
            }

            return LoadFromAssemblyPath(path);
        }

        public Assembly LoadBytes(byte[] assemblyBytes, byte[] pdbBytes)
        {
            if (pdbBytes == null)
            {
                return LoadFromStream(new MemoryStream(assemblyBytes));
            }

            return LoadFromStream(new MemoryStream(assemblyBytes), new MemoryStream(pdbBytes));
        }

        private string GetNativeImagePath(string ilPath)
        {
            var directory = Path.GetDirectoryName(ilPath);
            var arch = Environment.GetEnvironmentVariable("PROCESSOR_ARCHITECTURE");

            return Path.Combine(directory,
                                arch,
                                Path.GetFileNameWithoutExtension(ilPath) + ".ni.dll");
        }
    }
}
#endif