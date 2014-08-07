using Microsoft.Framework.PackageManager;
using System;

namespace Microsoft.Framework.Feeds
{
    /// <summary>
    /// Summary description for FeedOptions
    /// </summary>
    public class FeedOptions
    {
        public FeedOptions()
        {
            //
            // TODO: Add constructor logic here
            //
        }

        public string LocalPackages { get; set; }

        public IReport Report { get; set; }
    }
}
