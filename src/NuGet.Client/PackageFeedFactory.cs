using System;
using System.IO;
using System.Linq;

namespace NuGet.Client
{
    public static class PackageFeedFactory
    {
        public static IPackageFeed CreateFeed(string source,
                                       string userName,
                                       string password,
                                       bool noCache,
                                       bool ignoreFailedSources,
                                       ILogger logger)
        {
            var uri = new Uri(source);

            if (uri.IsFile)
            {
                return CreatePackageFolderFromPath(source, ignoreFailedSources, logger);
            }
            else
            {
                // TODO: Support v3 feeds
                // TODO: temporarily ignore NuGet v3 feeds
                if (source.EndsWith(".json"))
                {
                    return null;
                }

                // TODO: How do we flowing options to the source
                return new NuGetv2Feed(
                    source,
                    userName,
                    password,
                    noCache,
                    ignoreFailedSources,
                    logger);
            }
        }

        private static IPackageFeed CreatePackageFolderFromPath(string path, bool ignoreFailedSources, ILogger logger)
        {
            // Try to sniff and determine which logic to use
            Func<string, bool> containsNupkg = dir => Directory.Exists(dir) &&
                Directory.EnumerateFiles(dir, "*.nupkg")
                .Where(x => !Path.GetFileNameWithoutExtension(x).EndsWith(".symbols"))
                .Any();

            if (Directory.Exists(path) &&
                (containsNupkg(path) || Directory.EnumerateDirectories(path).Any(x => containsNupkg(x))))
            {
                return new NuGetv2PackageFolder(path, logger);
            }
            else
            {
                return new NuGetv3PackageFolder(path, ignoreFailedSources, logger);
            }
        }
    }
}