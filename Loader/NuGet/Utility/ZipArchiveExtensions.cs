using System.IO;
using System.IO.Compression;

namespace NuGet
{
    public static class ZipArchiveExtensions
    {
        public static ZipArchiveEntry GetManifest(this ZipArchive archive)
        {
            foreach (var entry in archive.Entries)
            {
                if (Path.GetExtension(entry.Name) == Constants.ManifestExtension)
                {
                    return entry;
                }
            }

            return null;
        }
    }
}
