using System.IO;
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
            var library = lockFile.Targets[0].Libraries[0];
            Assert.Equal("WindowsAzure.ServiceBus", library.Name);
            Assert.Equal(SemanticVersion.Parse("2.6.7"), library.Version);
            Assert.Equal(1, library.Dependencies.Count);
            var dependency = library.Dependencies[0];
            Assert.Equal(dependency.Id, "Microsoft.WindowsAzure.ConfigurationManager");
            Assert.Null(dependency.VersionSpec);
        }
    }
}
