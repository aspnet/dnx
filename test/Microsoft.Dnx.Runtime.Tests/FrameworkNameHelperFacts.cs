using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Versioning;
using System.Threading.Tasks;
using Microsoft.Dnx.Runtime.Helpers;
using Xunit;

namespace Microsoft.Dnx.Runtime.Tests
{
    public class FrameworkNameHelperFacts
    {
        [Theory]

        // Short names
        [InlineData("dnx451", "DNX,Version=v4.5.1")]
        [InlineData("dnxcore50", "DNXCore,Version=v5.0")]
        [InlineData("net451", ".NETFramework,Version=v4.5.1")]

        // With profiles
        [InlineData("net40-client", ".NETFramework,Version=v4.0,Profile=Client")]

        // Full names
        [InlineData(".NETFramework,Version=v4.5.1", ".NETFramework,Version=v4.5.1")]
        [InlineData(".NETPortable,Version=v4.5,Profile=Profile123", ".NETPortable,Version=v4.5,Profile=Profile123")]

        // Portable names
        [InlineData("portable-Profile123", ".NETPortable,Version=v0.0,Profile=Profile123")]
        [InlineData("portable-Profile49", ".NETPortable,Version=v0.0,Profile=Profile49")]
        [InlineData("portable45-Profile49", ".NETPortable,Version=v4.5,Profile=Profile49")]

        // We only support full profile names.
        [InlineData("portable-net45+wp8", "Unsupported,Version=v0.0")]
        public void CorrectlyParsesFrameworkNames(string input, string fullName)
        {
            Assert.Equal(
                new FrameworkName(fullName),
                FrameworkNameHelper.ParseFrameworkName(input));
        }
    }
}
