using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using NuGet;
using Xunit;

namespace Microsoft.Dnx.Runtime.Tests
{
    public class ProjectReferenceDependencyResolverFacts
    {
        [Theory]
        [InlineData("net20", "mscorlib,System")]
        [InlineData("net35", "mscorlib,System,System.Core")]
        [InlineData("net40", "mscorlib,System,System.Core,Microsoft.CSharp")]
        [InlineData("net45", "mscorlib,System,System.Core,Microsoft.CSharp")]
        [InlineData("net451", "mscorlib,System,System.Core,Microsoft.CSharp")]
        [InlineData("net452", "mscorlib,System,System.Core,Microsoft.CSharp")]
        [InlineData("net46", "mscorlib,System,System.Core,Microsoft.CSharp")]
        [InlineData("dnx451", "mscorlib,System,System.Core,Microsoft.CSharp")]
        [InlineData("dnx452", "mscorlib,System,System.Core,Microsoft.CSharp")]
        [InlineData("dnx46", "mscorlib,System,System.Core,Microsoft.CSharp")]
        public void GetDescriptionAddsCoreReferences(string shortFrameworkName, string expectedNames)
        {
            string projectJson = @"{ ""frameworks"": { """ + shortFrameworkName + @""": {} } }";
            using (var strm = new MemoryStream(Encoding.UTF8.GetBytes(projectJson)))
            {
                var project = Project.GetProjectFromStream(strm, "TheTestProject", @"C:\TestProject");
                var provider = new ProjectDependencyProvider();
                var expected = expectedNames.Split(',').Select(s => "fx/" + s).ToArray();
                var actual = provider.GetDescription(VersionUtility.ParseFrameworkName(shortFrameworkName), project)
                                     .Dependencies
                                     .Select(d => d.LibraryRange.Name).ToArray();

                Assert.Equal(expected, actual);
            }
        }
    }
}
