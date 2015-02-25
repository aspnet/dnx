using System;
using System.IO;

namespace Microsoft.Framework.Runtime.Hosting.Internal
{
    /// <summary>
    /// Simple class to consolidate I/O operations into one place for optimization
    /// </summary>
    internal static class FileSystem
    {
        internal static bool FileExists(string file) => File.Exists(file);

        internal static Stream OpenRead(string file)
        {
            return new FileStream(file, FileMode.Open, FileAccess.Read);
        }
    }
}