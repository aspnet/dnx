using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using NuGet.Common;
using NuGet.Packaging;
using NuGet.Packaging.Core;
using NuGet.Repositories;
using NuGet.Versioning;

namespace NuGet.Client
{
    public class NuGetv2LocalRepository
    {
        private readonly string _physicalPath;
        private readonly PackagePathResolver _pathResolver;

        public NuGetv2LocalRepository(string physicalPath)
        {
            _physicalPath = physicalPath;
            _pathResolver = new PackagePathResolver(physicalPath);
        }

        public IEnumerable<LocalPackageInfo> FindPackagesById(string packageId)
        {
            // get packages through nupkg files
            return GetPackages(packageId, GetPackageFiles(packageId + "*.nupkg"))
                .Concat(GetPackages(packageId, GetPackageFiles(packageId + "*.nuspec")))
                .Distinct();
        }

        internal IEnumerable<LocalPackageInfo> GetPackages(string packageId, IEnumerable<string> packagePaths)
        {
            foreach (var path in packagePaths)
            {
                using (var stream = File.OpenRead(path))
                {
                    var zip = new ZipArchive(stream);
                    var spec = zip.GetManifest();

                    using (var specStream = spec.Open())
                    {
                        var reader = new NuspecReader(specStream);
                        if (string.Equals(reader.GetId(), packageId, StringComparison.OrdinalIgnoreCase))
                        {
                            yield return new LocalPackageInfo(reader.GetId(), reader.GetVersion(), _physicalPath);
                        }
                    }
                }
            }
        }

        private IEnumerable<string> GetPackageLookupPaths(string packageId, NuGetVersion version)
        {
            // Files created by the path resolver. This would take into account the non-side-by-side scenario 
            // and we do not need to match this for id and version.
            var packageFileName = _pathResolver.GetPackageFileName(new PackageIdentity(packageId, version));
            var manifestFileName = Path.ChangeExtension(packageFileName, ".nuspec");
            var filesMatchingFullName = Enumerable.Concat(
                GetPackageFiles(packageFileName),
                GetPackageFiles(manifestFileName));

            if (version != null && version.Version.Revision < 1)
            {
                // If the build or revision number is not set, we need to look for combinations of the format
                // * Foo.1.2.nupkg
                // * Foo.1.2.3.nupkg
                // * Foo.1.2.0.nupkg
                // * Foo.1.2.0.0.nupkg
                // To achieve this, we would look for files named 1.2*.nupkg if both build and revision are 0 and
                // 1.2.3*.nupkg if only the revision is set to 0.
                string partialName = version.Version.Build < 1 ?
                                        String.Join(".", packageId, version.Version.Major, version.Version.Minor) :
                                        String.Join(".", packageId, version.Version.Major, version.Version.Minor, version.Version.Build);
                string partialManifestName = partialName + "*.nuspec";
                partialName += "*" + ".nupkg";

                // Partial names would result is gathering package with matching major and minor but different build and revision. 
                // Attempt to match the version in the path to the version we're interested in.
                var partialNameMatches = GetPackageFiles(partialName).Where(path => FileNameMatchesPattern(packageId, version, path));
                var partialManifestNameMatches = GetPackageFiles(partialManifestName).Where(
                    path => FileNameMatchesPattern(packageId, version, path));
                return Enumerable.Concat(filesMatchingFullName, partialNameMatches).Concat(partialManifestNameMatches);
            }
            return filesMatchingFullName;
        }

        private static bool FileNameMatchesPattern(string packageId, NuGetVersion version, string path)
        {
            var name = Path.GetFileNameWithoutExtension(path);
            SemanticVersion parsedVersion;

            // When matching by pattern, we will always have a version token. Packages without versions would be matched early on by the version-less path resolver 
            // when doing an exact match.
            return name.Length > packageId.Length &&
                   NuGetVersion.TryParse(name.Substring(packageId.Length + 1), out parsedVersion) &&
                   parsedVersion == version;
        }

        private IEnumerable<string> GetPackageFiles(string filter = null)
        {
            filter = filter ?? "*.nupkg";
            //Debug.Assert(
            //    filter.EndsWith(Constants.PackageExtension, StringComparison.OrdinalIgnoreCase) ||
            //    filter.EndsWith(Constants.ManifestExtension, StringComparison.OrdinalIgnoreCase));

            // Check for package files one level deep. We use this at package install time
            // to determine the set of installed packages. Installed packages are copied to 
            // {id}.{version}\{packagefile}.{extension}.
            foreach (var dir in Directory.EnumerateDirectories(_physicalPath))
            {
                foreach (var path in Directory.EnumerateFiles(dir, filter))
                {
                    if (Path.GetFileNameWithoutExtension(path).EndsWith(".symbols", StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    yield return path;
                }
            }

            // Check top level directory
            foreach (var path in Directory.EnumerateFiles(_physicalPath, filter))
            {
                if (Path.GetFileNameWithoutExtension(path).EndsWith(".symbols", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                yield return path;
            }
        }
    }
}