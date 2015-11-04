namespace Microsoft.Extensions.PlatformAbstractions.Internal
{
    internal static partial class VersionHelper
    {
        public static string GetOSXVersion()
        {
            // Get the version of the kernel from uname
            var kernelVersion = UnameData.version;
            
            // Get the major version number
            var splat = kernelVersion.Split('.');
            if (splat.Length < 1)
            {
                return string.Empty;
            }
            var majorVersion = splat[0];
            
            int parsedMajor = 0;
            if (!int.TryParse(majorVersion, out parsedMajor))
            {
                return string.Empty;
            }
            
            // Pre 5.0 Darwin versions are unsupported
            if (parsedMajor < 5)
            {
                return string.Empty;
            }
            
            // OS X Minor version is (Darwin Version - 4)
            var osxVersion = parsedMajor - 4;
            return $"10.{osxVersion.ToString()}";
        }
    }
}
