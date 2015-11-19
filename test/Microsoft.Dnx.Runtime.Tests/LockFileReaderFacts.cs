using System.IO;
using System.Runtime.Versioning;
using System.Text;
using NuGet;
using Xunit;

namespace Microsoft.Dnx.Runtime.Tests
{
    public class LockFileReaderFacts
    {
        [Fact]
        public void NullDependencyVersionsAreParsed()
        {
            var lockFileData = @"{
  ""locked"": false,
  ""version"": 1,
  ""targets"": {
        "".NETFramework,Version=v4.5"": {
            ""SomeProject/1.0.0"": {
                ""type"": ""project"",
                ""framework"": "".NETFramework,Version=v4.5""
            },
            ""WindowsAzure.ServiceBus/2.6.7"": {
                ""dependencies"": {
                    ""Microsoft.WindowsAzure.ConfigurationManager"": null
                },
                ""frameworkAssemblies"": [
                  ""System.ServiceModel"",
                  ""System.Xml"",
                  ""System.Runtime.Serialization""
                ],
                ""compile"": {
                  ""lib/net40-full/Microsoft.ServiceBus.dll"": {}
                },
                ""runtime"": {
                  ""lib/net40-full/Microsoft.ServiceBus.dll"": {}
                }
            }
        }
  },
  ""libraries"": {
    ""WindowsAzure.ServiceBus/2.6.7"": {
      ""sha512"": ""AhQ4nya0Pu0tGev/Geqt5+yBTI+ov66ginMHCm+HqmXezTIOSfBu7HOI5RuvmiQqM99AeTuASD6gMz+zWueHNQ=="",
      ""files"": [
        ""WindowsAzure.ServiceBus.2.6.7.nupkg"",
        ""WindowsAzure.ServiceBus.2.6.7.nupkg.sha512"",
        ""WindowsAzure.ServiceBus.nuspec"",
        ""content/app.config.install.xdt"",
        ""content/web.config.install.xdt"",
        ""lib/net40-full/Microsoft.ServiceBus.dll"",
        ""lib/net40-full/Microsoft.ServiceBus.xml""
      ]
    }
  },
  ""projectFileDependencyGroups"": {
    """": [
      ""SomeProject "",
      ""WindowsAzure.ServiceBus >= 2.6.7""
    ],
    "".NETFramework,Version=v4.5"": []
  }
}";

            var reader = new LockFileReader();

            var stream = new MemoryStream(Encoding.UTF8.GetBytes(lockFileData));
            var lockFile = reader.Read(stream);

            Assert.False(lockFile.Islocked);
            Assert.Equal(1, lockFile.Targets.Count);
            var library1 = lockFile.Targets[0].Libraries[0];
            Assert.Equal("SomeProject", library1.Name);
            Assert.Equal("project", library1.Type);
            Assert.Equal(new FrameworkName(".NETFramework,Version=v4.5"), library1.TargetFramework);
            Assert.Equal(SemanticVersion.Parse("1.0.0"), library1.Version);
            var library2 = lockFile.Targets[0].Libraries[1];
            Assert.Equal("WindowsAzure.ServiceBus", library2.Name);
            Assert.Null(library2.Type);
            Assert.Equal(SemanticVersion.Parse("2.6.7"), library2.Version);
            Assert.Equal(1, library2.Dependencies.Count);
            var dependency = library2.Dependencies[0];
            Assert.Equal(dependency.Id, "Microsoft.WindowsAzure.ConfigurationManager");
            Assert.Null(dependency.VersionSpec);
        }
    }
}
