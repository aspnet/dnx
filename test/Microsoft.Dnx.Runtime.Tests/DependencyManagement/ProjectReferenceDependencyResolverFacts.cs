using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Versioning;
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
            var resolver = new TestResolver(shortFrameworkName);
            var provider = new ProjectReferenceDependencyProvider(resolver);
            Assert.Equal(
                expectedNames.Split(',').Select(s => "fx/" + s).ToArray(),
                provider.GetDescription(new LibraryRange(TestResolver.ProjectName, frameworkReference: false), VersionUtility.ParseFrameworkName(shortFrameworkName))
                    .Dependencies
                    .Select(d => d.LibraryRange.Name).ToArray());
        }

        private class TestResolver : IProjectResolver
        {
            public static readonly string ProjectName = "TheTestProject";
            public IEnumerable<string> SearchPaths
            {
                get
                {
                    yield break;
                }
            }

            public Project Project { get; }

            public TestResolver(string frameworkShortName)
            {
                string projectJson = @"{ ""frameworks"": { """ + frameworkShortName + @""": {} } }";
                using(var strm = new MemoryStream(Encoding.UTF8.GetBytes(projectJson)))
                {
                    Project = Project.GetProjectFromStream(strm, ProjectName, @"C:\TestProject");
                }
            }

            public bool TryResolveProject(string name, out Project project)
            {
                if(name.Equals(ProjectName))
                {
                    project = Project;
                    return true;
                }
                project = null;
                return false;
            }
        }
    }
}
