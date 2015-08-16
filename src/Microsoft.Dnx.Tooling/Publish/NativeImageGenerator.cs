using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Dnx.Project;
using NuGet;

namespace Microsoft.Dnx.Tooling.Publish
{
    /// <summary>
    /// Generate native image for packages
    /// </summary>
    public class NativeImageGenerator
    {
        public bool BuildNativeImages(PublishRoot root)
        {
            var success = true;

            // REVIEW: Does does doing this for multiple runtimes make sense?
            foreach (var runtime in root.Runtimes)
            {
                var runtimeBin = Path.Combine(runtime.TargetPath, "bin");

                var options = new CrossgenOptions()
                {
                    CrossgenPath = Path.Combine(runtimeBin, "crossgen.exe"),
                    InputPaths = ResolveOutputAssemblies(root),
                    RuntimePath = runtimeBin,
                    Symbols = false
                };

                var crossgenManager = new CrossgenManager(options);
                success &= crossgenManager.GenerateNativeImages();

                if (!success)
                {
                    return false;
                }
            }
            return success;
        }

        /// <summary>
        /// This is a helper method for looking up directories that directly contains assemblies that would be loaded
        /// given the published runtime framework. We should run crossgen on these folders
        /// </summary>
        private IEnumerable<string> ResolveOutputAssemblies(PublishRoot root)
        {
            var resolver = new DefaultPackagePathResolver(root.TargetPackagesPath);

            if (root.LockFile == null)
            {
                return Enumerable.Empty<string>();
            }

            var directories = new HashSet<string>();

            foreach (var target in root.LockFile.Targets)
            {
                foreach (var library in target.Libraries)
                {
                    var packagesDir = resolver.GetInstallPath(library.Name, library.Version);

                    foreach (var path in library.RuntimeAssemblies)
                    {
                        var assemblyPath = CombinePath(packagesDir, path);
                        directories.Add(Path.GetDirectoryName(assemblyPath));
                    }
                }
            }

            return directories;
        }

        private static string CombinePath(string path1, string path2)
        {
            return Path.Combine(path1, path2.Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar));
        }

        /// <summary>
        /// This is the factory method to instantiate a PackNativeManager, if parameters are in invalid state and native
        /// generation cannot be performed, it would return null
        /// </summary>
        public static NativeImageGenerator Create(PublishOptions options, PublishRoot root, IEnumerable<DependencyContext> contexts)
        {
            if (!options.Runtimes.Any())
            {
                options.Reports.Information.WriteLine(
                    "Please provide target CoreCLR runtimes using --runtime flags".Yellow());
                return null;
            }

            foreach (var runtime in root.Runtimes)
            {
                var frameworkName = runtime.Framework;
                // NOTE: !IsDesktop == IsCore and only Core packages can be crossgened at least for now
                if (VersionUtility.IsDesktop(frameworkName))
                {
                    options.Reports.Information.WriteLine(
                        "Native image generation is only supported for .NET Core flavors.".Yellow());
                    return null;
                }
            }

            var duplicates = options.Runtimes
                .GroupBy(r => CrossgenManager.ResolveProcessorArchitecture(r))
                .Where(g => g.Count() > 1);

            if (duplicates.Any())
            {
                var message = "The following runtimes will result in output conflicts. Please provide distinct runtime flavor for each processor architecture:\n"
                    + string.Join("\n", duplicates.Select(
                        g => string.Format("Architecture: {0}\nRuntimes: {1}", g.Key, string.Join(", ", g))));
                options.Reports.Information.WriteLine(message.Yellow());
                return null;
            }

            return new NativeImageGenerator();
        }
    }
}
