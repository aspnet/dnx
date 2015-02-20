using NuGet.Versioning;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NuGet.Versioning.Test
{
    public class GitMetadataComparer : IVersionComparer
    {
        public bool Equals(SimpleVersion x, SimpleVersion y)
        {
            return Compare(x, y) == 0;
        }

        public int GetHashCode(SimpleVersion obj)
        {
            NuGetVersion version = obj as NuGetVersion;

            return String.Format(CultureInfo.InvariantCulture, "{0}.{1}.{2}-{3}GIT{4}",
                version.Major, version.Minor, version.Patch, version.Release, GetCommitFromMetadata(version.Metadata)).GetHashCode();
        }

        public int Compare(SimpleVersion x, SimpleVersion y)
        {
            NuGetVersion versionX = x as NuGetVersion;
            NuGetVersion versionY = y as NuGetVersion;

            // compare without metadata
            int result = VersionComparer.VersionRelease.Compare(x, y);

            if (result != 0)
                return result;

            // compare git commits, form: buildmachine-gitcommit
            return GitCommitOrder(GetCommitFromMetadata(versionX.Metadata)).CompareTo(GitCommitOrder(GetCommitFromMetadata(versionY.Metadata)));
        }

        /// <summary>
        /// Basic git commit order provider
        /// </summary>
        private static int GitCommitOrder(string hash)
        {
            switch (hash)
            {
                case "dbf5ec0":
                    return 10;
                case "dcb46c8":
                    return 9;
                case "901463b":
                    return 8;
                case "cc5438c":
                    return 7;
                case "d9375a6":
                    return 6;
                case "0ed1eb0":
                    return 5;
                case "c2ff710":
                    return 4;
                case "0428afe":
                    return 3;
                case "77acab0":
                    return 2;
                case "a5c5ff9":
                    return 1;
            }

            return 0;
        }

        private static string GetCommitFromMetadata(string s)
        {
            return s.Split('-')[1];
        }
    }
}
