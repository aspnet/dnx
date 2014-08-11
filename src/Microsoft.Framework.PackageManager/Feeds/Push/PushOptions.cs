using Microsoft.Framework.Feeds;
using System;

namespace Microsoft.Framework.PackageManager.Feeds.Push
{
    /// <summary>
    /// Summary description for PushOptions
    /// </summary>
    public class PushOptions : FeedOptions
    {
        public string RemotePackages { get; set; }

        public string RemoteKey { get; set; }
    }
}