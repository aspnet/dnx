// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

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
