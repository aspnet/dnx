using System;
using System.Collections.Generic;
using System.Linq;

namespace Microsoft.Dnx.Tooling.Publish.Bundling
{
    public static class BundlerFactory
    {
        static readonly IDictionary<string, Func<string, IPublishBundler>> Bundlers = new Dictionary<string, Func<string, IPublishBundler>>(StringComparer.OrdinalIgnoreCase);

        static BundlerFactory()
        {
            Bundlers["nupkg"] = CreateNuGetPackage;
        }

        public static string[] SupportedFormats => Bundlers.Keys.ToArray();

        public static bool IsSupportedFormat(string format)
        {
            return Bundlers.ContainsKey(format);
        }

        public static IPublishBundler CreateNuGetPackage(string outputDirectory)
        {
            return new NuGetPublishBundler(outputDirectory);
        }

        public static IPublishBundler Create(string format, string outputDirectory)
        {
            Func<string, IPublishBundler> bundler;
            if (!Bundlers.TryGetValue(format, out bundler))
            {
                throw new ArgumentException($"Unknown bundler format: \"{format}\". Supported formats: {string.Join(", ", SupportedFormats)}");
            }
            return bundler(outputDirectory);
        }
    }
}