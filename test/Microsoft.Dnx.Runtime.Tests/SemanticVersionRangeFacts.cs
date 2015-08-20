using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using NuGet;
using Xunit;

namespace Microsoft.Dnx.Runtime.Tests
{
    public class SemanticVersionRangeFacts
    {
        [Fact]
        public void VersionRangeToString()
        {
            var range = new SemanticVersionRange
            {
                MinVersion = SemanticVersion.Create("1.0.0-beta"),
                MaxVersion = SemanticVersion.Create("1.0.0-beta")
            };

            Assert.Equal("1.0.0-beta", range.ToString());
        }
    }
}
