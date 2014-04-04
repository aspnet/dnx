using System;
using System.IO;
using System.IO.Compression;
using Microsoft.Net.Runtime;
using Microsoft.Net.Runtime.Loader.NuGet;

namespace Microsoft.Net.Project.Packing
{
    public class PackPackage
    {
        private readonly NuGetDependencyResolver _nugetDependencyResolver;
        private readonly LibraryDescription _libraryDescription;

        public PackPackage(NuGetDependencyResolver nugetDependencyResolver, LibraryDescription libraryDescription)
        {
            _nugetDependencyResolver = nugetDependencyResolver;
            _libraryDescription = libraryDescription;
        }

        public Library Library { get { return _libraryDescription.Identity; } }

        public string TargetPath { get; private set; }

        public void Emit(PackRoot root)
        {
            var package = _nugetDependencyResolver.FindCandidate(
                _libraryDescription.Identity.Name,
                _libraryDescription.Identity.Version);

            Console.WriteLine("Packing nupkg dependency {0} {1}", package.Id, package.Version);

            var targetName = package.Id + "." + package.Version;
            TargetPath = Path.Combine(root.PackagesPath, targetName);

            if (Directory.Exists(TargetPath))
            {
                Console.WriteLine("  {0} already exists.", TargetPath);
                return;
            }

            Console.WriteLine("  Target {0}", TargetPath);

            var targetNupkgPath = Path.Combine(TargetPath, targetName + ".nupkg");
            using (var sourceStream = package.GetStream())
            {
                using (var archive = new ZipArchive(sourceStream, ZipArchiveMode.Read))
                {
                    PackUtilities.ExtractFiles(archive, TargetPath);
                }
            }
            using (var sourceStream = package.GetStream())
            {
                using (var targetStream = new FileStream(targetNupkgPath, FileMode.Create, FileAccess.Write, FileShare.None))
                {
                    sourceStream.CopyTo(targetStream);
                }
            }
        }
    }
}