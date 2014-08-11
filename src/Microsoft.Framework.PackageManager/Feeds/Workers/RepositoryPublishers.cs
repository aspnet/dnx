using System;

namespace Microsoft.Framework.PackageManager.Feeds.Workers
{
    /// <summary>
    /// Summary description for RepositoryPublishers
    /// </summary>
    public static class RepositoryPublishers
    {
        public static IRepositoryPublisher Create(
            string path,
            string accessKey,
            IReport report)
        {
            Uri uri;
            if (Uri.TryCreate(path, UriKind.Absolute, out uri))
            {
                if (uri.Scheme == "https")
                {
                    return new AzureStoragePackages(path, accessKey)
                    {
                        Report = report
                    };
                }
            }
            return new FileSystemRepositoryPublisher(path)
            {
                Report = report
            };
        }
    }
}