using System;

namespace Microsoft.Framework.PackageManager.Feeds.Pull
{
    /// <summary>
    /// Summary description for PullCommand
    /// </summary>
    public class PullCommand
    {
        public PullCommand(PullOptions options)
        {
            Options = options;
        }

        public PullOptions Options { get; private set; }

        public bool Execute()
        {
            return false;
        }
    }
}