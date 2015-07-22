using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Versioning;
using Microsoft.Dnx.Project;
using Microsoft.Dnx.Runtime;
using NuGet;

namespace Microsoft.Dnx.Tooling.Publish
{
    /// <summary>
    /// Generate native image for packages
    /// </summary>
    public class NativeImageGenerator
    {
        private readonly IDictionary<FrameworkName, NuGetDependencyResolver> _resolverLookup;

        private NativeImageGenerator(IDictionary<FrameworkName, NuGetDependencyResolver> resolverLookup)
        {
            _resolverLookup = resolverLookup;
        }

        public bool BuildNativeImages(PublishRoot root)
        {
            var success = true;
            foreach (var runtime in root.Runtimes)
            {
                NuGetDependencyResolver resolver;
                if (!_resolverLookup.TryGetValue(runtime.Framework, out resolver))
                {
                    throw new InvalidOperationException("No matching framework is found for " + runtime.Framework);
                }

                var runtimeBin = Path.Combine(runtime.TargetPath, "bin");

                var options = new CrossgenOptions()
                {
                    CrossgenPath = Path.Combine(runtimeBin, "crossgen.exe"),
                    InputPaths = ResolveOutputAssemblies(root, resolver),
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
        private IEnumerable<string> ResolveOutputAssemblies(PublishRoot root, NuGetDependencyResolver resolver)
        {
            var outputPathsMap = root.Packages
                .ToDictionary(
                    pkg => pkg.Library,
                    pkg => pkg.TargetPath
                );

            var result = new HashSet<string>();
            var libraryNotInOutput = new List<LibraryIdentity>();
            var missingOutputFolder = new List<string>();

            foreach (var dependency in resolver.PackageAssemblyLookup.Values)
            {
                var libId = dependency.Library.Identity;
                var libPath = dependency.Library.Path;
                var assemblyDir = Path.GetDirectoryName(dependency.Path);
                var assemblySelection = assemblyDir.Substring(libPath.Length);
                string outputLibLocation;
                if (!outputPathsMap.TryGetValue(libId, out outputLibLocation))
                {
                    libraryNotInOutput.Add(libId);
                    continue;
                }
                var output = outputLibLocation + assemblySelection;

                if (!Directory.Exists(output))
                {
                    missingOutputFolder.Add(output);
                }
                else
                {
                    result.Add(output);
                }
            }

            if (libraryNotInOutput.Any())
            {
                throw new InvalidOperationException(string.Format("Library {0} cannot be found in the published output.", string.Join(", ", libraryNotInOutput)));
            }

            if (missingOutputFolder.Any())
            {
                throw new InvalidOperationException("Published output does not contain directory:\n" + string.Join("\n", missingOutputFolder));
            }

            return result;
        }

        /// <summary>
        /// This is the factory method to instantiate a PackNativeManager, if parameters are in invalid state and native
        /// generation cannot be performed, it would return null
        /// </summary>
        public static NativeImageGenerator Create(PublishOptions options, PublishRoot root, IEnumerable<DependencyContext> contexts)
        {
            if (options.Runtimes.Count() == 0)
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

            var contextMap = contexts.ToDictionary(
                context => context.FrameworkName,
                context => context.NuGetDependencyResolver
            );

            return new NativeImageGenerator(contextMap);
        }
    }
}
