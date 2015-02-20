using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NuGet.Packaging.Core
{
    /// <summary>
    /// Nupkg reading helper methods
    /// </summary>
    public static class ZipArchiveHelper
    {
        public static ZipArchiveEntry GetEntry(ZipArchive zipArchive, string path)
        {
            var entry = zipArchive.Entries.Where(e => e.FullName == path).FirstOrDefault();

            if (entry == null)
            {
                throw new FileNotFoundException(path);
            }

            return entry;
        }

        public static IEnumerable<string> GetFiles(ZipArchive zipArchive)
        {
            return zipArchive.Entries.Select(e => UnescapePath(e.FullName));
        }

        public static string UnescapePath(string path)
        {
            if (path != null && path.IndexOf('%') > -1)
            {
                return Uri.UnescapeDataString(path);
            }

            return path;
        }

        public static Stream OpenFile(ZipArchive zipArchive, string path)
        {
            var entry = GetEntry(zipArchive, path);
            return entry.Open();
        }
    }
}
