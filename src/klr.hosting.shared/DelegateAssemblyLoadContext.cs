#if K10
using System;
using System.IO;
using System.Reflection;
using System.Runtime.Hosting.Loader;

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
                return LoadFromFileWithNativeImage(path, nativeImagePath);
            }

            return LoadFromFile(path);
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
            string directory = Path.GetDirectoryName(ilPath);

            return Path.Combine(directory,
                                IntPtr.Size == 4 ? "x86" : "amd64",
                                Path.GetFileNameWithoutExtension(ilPath) + ".ni.dll");
        }
    }
}
#endif