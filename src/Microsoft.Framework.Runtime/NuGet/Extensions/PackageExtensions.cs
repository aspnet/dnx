using System;

namespace NuGet
{
    public static class PackageExtensions
    {
        public static bool IsReleaseVersion(this IPackageName packageMetadata)
        {
            return String.IsNullOrEmpty(packageMetadata.Version.SpecialVersion);
        }

        public static string GetFullName(this IPackageName package)
        {
            return package.Id + " " + package.Version;
        }
    }
}
