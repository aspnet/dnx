// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Runtime.Versioning;
using NuGet.Resources;

namespace NuGet
{
    public class ZipPackage : LocalPackage
    {
        private const string CacheKeyFormat = "NUGET_ZIP_PACKAGE_{0}_{1}{2}";
        private const string AssembliesCacheKey = "ASSEMBLIES";
        private const string FilesCacheKey = "FILES";

        private readonly bool _enableCaching;

        private static readonly TimeSpan CacheTimeout = TimeSpan.FromSeconds(15);

        // paths to exclude
        private static readonly string[] ExcludePaths = new[] { "_rels", "package" };

        // We don't store the stream itself, just a way to open the stream on demand
        // so we don't have to hold on to that resource
        private readonly Func<Stream> _streamFactory;

        public ZipPackage(string filePath)
            : this(filePath, enableCaching: false)
        {
        }

        public ZipPackage(Stream stream)
        {
            if (stream == null)
            {
                throw new ArgumentNullException(nameof(stream));
            }
            _enableCaching = false;
            _streamFactory = stream.ToStreamFactory();
            EnsureManifest();
        }

        private ZipPackage(string filePath, bool enableCaching)
        {
            if (String.IsNullOrEmpty(filePath))
            {
                throw new ArgumentNullException(nameof(filePath));
            }
            _enableCaching = enableCaching;
            _streamFactory = () => File.OpenRead(filePath);
            EnsureManifest();
        }

        internal ZipPackage(Func<Stream> streamFactory, bool enableCaching)
        {
            if (streamFactory == null)
            {
                throw new ArgumentNullException(nameof(streamFactory));
            }
            _enableCaching = enableCaching;
            _streamFactory = streamFactory;
            EnsureManifest();
        }

        public override Stream GetStream()
        {
            return _streamFactory();
        }

        public override IEnumerable<FrameworkName> GetSupportedFrameworks()
        {
            IEnumerable<FrameworkName> fileFrameworks;

            using (Stream stream = _streamFactory())
            {
                var package = new ZipArchive(stream);

                string effectivePath;
                fileFrameworks = from part in package.Entries
                                 let path = part.FullName.Replace('/', '\\')
                                 where IsPackageFile(part)
                                 select VersionUtility.ParseFrameworkNameFromFilePath(path, out effectivePath);

            }

            return base.GetSupportedFrameworks()
                       .Concat(fileFrameworks)
                       .Where(f => f != null)
                       .Distinct();
        }

        protected override IEnumerable<IPackageAssemblyReference> GetAssemblyReferencesCore()
        {
            return GetAssembliesNoCache();
        }

        protected override IEnumerable<IPackageAssemblyReference> GetResourceReferencesCore()
        {
            return (from file in GetFiles()
                    where IsResourcesReference(file.Path)
                    select(IPackageAssemblyReference)new ZipPackageAssemblyReference(file)).ToList();
        }

        protected override IEnumerable<IPackageFile> GetFilesBase()
        {
            return GetFilesNoCache();
        }

        private List<IPackageAssemblyReference> GetAssembliesNoCache()
        {
            return (from file in GetFiles()
                    where IsAssemblyReference(file.Path)
                    select (IPackageAssemblyReference)new ZipPackageAssemblyReference(file)).ToList();
        }

        private List<IPackageFile> GetFilesNoCache()
        {
            using (Stream stream = _streamFactory())
            {
                var package = new ZipArchive(stream);

                return (from part in package.Entries
                        where IsPackageFile(part)
                        select (IPackageFile)new ZipPackageFile(part)).ToList();
            }
        }

        private void EnsureManifest()
        {
            using (Stream stream = _streamFactory())
            {
                var package = new ZipArchive(stream);

                ZipArchiveEntry manifestPart = package.GetManifest();

                if (manifestPart == null)
                {
                    throw new InvalidOperationException(NuGetResources.PackageDoesNotContainManifest);
                }

                using (Stream manifestStream = manifestPart.Open())
                {
                    ReadManifest(manifestStream);
                }
            }
        }

        internal static bool IsPackageFile(ZipArchiveEntry part)
        {
            string path = part.FullName;

            // We exclude any opc files and the manifest file (.nuspec)
            return !ExcludePaths.Any(p => path.StartsWith(p, StringComparison.OrdinalIgnoreCase)) &&
                   !PackageHelper.IsManifest(path) &&
                   !path.StartsWith("[Content_Types]", StringComparison.OrdinalIgnoreCase);
        }
    }
}