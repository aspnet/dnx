using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using NuGet.Resources;

namespace NuGet
{
    public class LocalPackageRepository
    {
        private readonly ILookup<string, IPackage> _cache;

        public LocalPackageRepository(string physicalPath)
            : this(new DefaultPackagePathResolver(physicalPath),
                   new PhysicalFileSystem(physicalPath))
        {
        }

        public LocalPackageRepository(IPackagePathResolver pathResolver, IFileSystem fileSystem)
        {
            if (pathResolver == null)
            {
                throw new ArgumentNullException("pathResolver");
            }

            if (fileSystem == null)
            {
                throw new ArgumentNullException("fileSystem");
            }

            FileSystem = fileSystem;
            PathResolver = pathResolver;
            _cache = PopulateCache();
        }

        private ILookup<string, IPackage> PopulateCache()
        {
            string nupkgFilter = "*" + Constants.PackageExtension;
            string nuspecFilter = "*" + Constants.ManifestExtension;

            var packages = new List<IPackage>();

            foreach (var dir in FileSystem.GetDirectories(String.Empty))
            {
                foreach (var path in FileSystem.GetFiles(dir, nupkgFilter))
                {
                    packages.Add(OpenPackage(path));
                }
                foreach (var path in FileSystem.GetFiles(dir, nuspecFilter))
                {
                    packages.Add(OpenPackage(path));
                }
            }

            return packages.ToLookup(p => p.Id);
        }

        public IPackagePathResolver PathResolver
        {
            get;
            set;
        }

        protected IFileSystem FileSystem
        {
            get;
            private set;
        }

        public IPackage FindPackage(string packageId, SemanticVersion version)
        {
            if (String.IsNullOrEmpty(packageId))
            {
                throw new ArgumentNullException("packageId");
            }

            if (version == null)
            {
                throw new ArgumentNullException("version");
            }

            return _cache[packageId].FirstOrDefault(p => p.Version == version);
        }

        public IEnumerable<IPackage> FindPackagesById(string packageId)
        {
            if (String.IsNullOrEmpty(packageId))
            {
                throw new ArgumentNullException("packageId");
            }

            return _cache[packageId];
        }


        private IPackage OpenPackage(string path)
        {
            if (!FileSystem.FileExists(path))
            {
                return null;
            }

            if (Path.GetExtension(path) == Constants.PackageExtension)
            {
                OptimizedZipPackage package;

                try
                {
                    package = new OptimizedZipPackage(FileSystem, path);
                }
                catch (InvalidDataException ex)
                {
                    throw new InvalidDataException(String.Format(CultureInfo.CurrentCulture, NuGetResources.ErrorReadingPackage, path), ex);
                }

                // Set the last modified date on the package
                package.Published = FileSystem.GetLastModified(path);

                return package;
            }

            if (Path.GetExtension(path) == Constants.ManifestExtension)
            {
                UnzippedPackage package;

                try
                {
                    package = new UnzippedPackage(FileSystem, path);
                }
                catch (InvalidDataException ex)
                {
                    throw new InvalidDataException(String.Format(CultureInfo.CurrentCulture, NuGetResources.ErrorReadingPackage, path), ex);
                }

                // Set the last modified date on the package
                package.Published = FileSystem.GetLastModified(path);

                return package;
            }

            return null;
        }

        private string GetPackageFilePath(IPackage package)
        {
            return Path.Combine(PathResolver.GetPackageDirectory(package),
                                PathResolver.GetPackageFileName(package));
        }

        private string GetPackageFilePath(string id, SemanticVersion version)
        {
            return Path.Combine(PathResolver.GetPackageDirectory(id, version),
                                PathResolver.GetPackageFileName(id, version));
        }
    }
}