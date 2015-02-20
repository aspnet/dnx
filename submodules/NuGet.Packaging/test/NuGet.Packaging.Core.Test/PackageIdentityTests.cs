using NuGet.Packaging.Core;
using NuGet.Versioning;
using Xunit;

namespace NuGet.Packaging.Core.Test
{
    public class PackageIdentityTests
    {
        [Fact]
        public void TestToString()
        {
            var packageIdentity = new PackageIdentity("packageA", new NuGetVersion("1.0.0"));
            Assert.Equal("packageA.1.0.0", packageIdentity.ToString());

            var formattedString = string.Format("This is package '{0}'", packageIdentity);
            Assert.Equal("This is package 'packageA.1.0.0'", formattedString);
        }
    }
}
