using NuGet.Frameworks;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Xunit;

namespace NuGet.Test
{
    public class FrameworkExpanderTests
    {
        [Fact]
        public void FrameworkExpander_Indirect()
        {
            NuGetFramework framework = NuGetFramework.Parse("win9");
            NuGetFramework indirect = new NuGetFramework(".NETCore", new Version(4, 5), "Windows", new Version(8, 0));

            FrameworkExpander expander = new FrameworkExpander();
            var expanded = expander.Expand(framework).ToArray();

            Assert.True(expanded.Contains(indirect, NuGetFramework.Comparer));
        }

        [Fact]
        public void FrameworkExpander_Basic()
        {
            NuGetFramework framework = NuGetFramework.Parse("net45");

            FrameworkExpander expander = new FrameworkExpander();
            var expanded = expander.Expand(framework).ToArray();

            Assert.Equal(4, expanded.Length);
            Assert.Equal(".NETFramework, Version=v4.5, Profile=Client", expanded[0].ToString());
            Assert.Equal(".NETFramework, Version=v4.5, Profile=Full", expanded[1].ToString());
            Assert.Equal(".NETCore, Version=v4.5", expanded[2].ToString());
            Assert.Equal("native, Version=v0.0", expanded[3].ToString());
        }

        [Fact]
        public void FrameworkExpander_Win()
        {
            NuGetFramework framework = NuGetFramework.Parse("win");

            FrameworkExpander expander = new FrameworkExpander();
            var expanded = expander.Expand(framework).ToArray();

            Assert.Equal(2, expanded.Length);
            Assert.Equal("Windows, Version=v8.0", expanded[0].ToString());
            Assert.Equal(".NETCore, Version=v4.5, Platform=Windows, PlatformVersion=v8.0", expanded[1].ToString());
        }

        [Fact]
        public void FrameworkExpander_NetCore45()
        {
            NuGetFramework framework = NuGetFramework.Parse("netcore45");

            FrameworkExpander expander = new FrameworkExpander();
            var expanded = expander.Expand(framework).ToArray();

            Assert.Equal(1, expanded.Length);
            Assert.Equal("native, Version=v0.0", expanded[0].ToString());
        }
    }
}
