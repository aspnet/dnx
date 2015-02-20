using System;
using System.IO;
using System.IO.Compression;
using System.Text;

namespace NuGet.Test.Utility
{
    public static class ZipExtensions
    {
        public static void AddEntry(this ZipArchive archive, string path, byte[] data)
        {
            var entry = archive.CreateEntry(path);

            using (var stream = entry.Open())
            {
                stream.Write(data, 0, data.Length);
            }
        }

        public static void AddEntry(this ZipArchive archive, string path, string value, Encoding encoding)
        {
            var entry = archive.CreateEntry(path);

            using (var stream = entry.Open())
            {
                var data = encoding.GetBytes(value);
                stream.Write(data, 0, data.Length);
            }
        }
        
        public static void ExtractAll(this ZipArchive archive, string targetPath)
        {
            foreach (ZipArchiveEntry entry in archive.Entries)
            {
                var entryFullName = entry.FullName;
                if (entryFullName.StartsWith("/", StringComparison.Ordinal))
                {
                    entryFullName = entryFullName.Substring(1);
                }

                entryFullName = Uri.UnescapeDataString(entryFullName.Replace('/', Path.DirectorySeparatorChar));


                var targetFile = Path.Combine(targetPath, entryFullName);
                if (!targetFile.StartsWith(targetPath, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }
                
                if (Path.GetFileName(targetFile).Length == 0)
                {
                    Directory.CreateDirectory(targetFile);
                }
                else
                {
                    var targetEntryPath = Path.GetDirectoryName(targetFile);
                    if (!Directory.Exists(targetEntryPath))
                    {
                        Directory.CreateDirectory(targetEntryPath);
                    }

                    using (var entryStream = entry.Open())
                    {
                        using (var targetStream = new FileStream(targetFile, FileMode.Create, FileAccess.Write, FileShare.None))
                        {
                            entryStream.CopyTo(targetStream);
                        }
                    }
                }
            }
        }
    }
}