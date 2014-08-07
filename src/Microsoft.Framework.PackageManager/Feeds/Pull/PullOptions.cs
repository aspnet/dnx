using Microsoft.Framework.Feeds;
using System;

namespace Microsoft.Framework.PackageManager.Feeds.Pull
{
    /// <summary>
    /// Summary description for PullOptions
    /// </summary>
    public class PullOptions : FeedOptions
    {
        public string RemotePackages { get; set; }
    }
}