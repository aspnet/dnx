using System.Collections.Generic;
using System.Linq;
using Microsoft.Dnx.Tooling.Restore.RuntimeModel;
using Newtonsoft.Json.Linq;
using Xunit;

namespace Microsoft.Dnx.Tooling.Tests
{
    public class RuntimeRestoreFacts
    {
        private const string SystemNetPrimitivesSample = @"{
  ""runtimes"": {
    ""win7"": {
      ""System.Net.Primitives"": {
        ""runtime.win7.System.Net.Primitives"": ""4.0.11""
      }
    },
    ""osx.10.10"": {
      ""System.Net.Primitives"": {
        ""runtime.osx.10.10.System.Net.Primitives"": ""4.0.11""
      }
    },
    ""unix"": {
      ""System.Net.Primitives"": {
        ""runtime.unix.System.Net.Primitives"": ""4.0.11""
      }
    }
  }
}";

        [Theory]
        [InlineData(SystemNetPrimitivesSample, "osx.10.10-x64", "System.Net.Primitives", "runtime.osx.10.10.System.Net.Primitives", "4.0.11")]
        [InlineData(SystemNetPrimitivesSample, "ubuntu.14.04-x64", "System.Net.Primitives", "runtime.unix.System.Net.Primitives", "4.0.11")]
        [InlineData(SystemNetPrimitivesSample, "win10-arm", "System.Net.Primitives", "runtime.win7.System.Net.Primitives", "4.0.11")]
        public void FindRuntimeDependencies_FindsMostSpecificMatch(string json, string inputRuntime, string packageId, string expectedPackageName, string expectedPackageVersion)
        {
            var runtimeJson = ParseRuntimeFile(SystemNetPrimitivesSample);
            var files = new List<RuntimeFile> {
                runtimeJson
            };

            var effectiveDependencies = new Dictionary<string, DependencySpec>();
            var allRuntimeNames = new HashSet<string>();

            RestoreCommand.FindRuntimeDependencies(
                inputRuntime,
                files,
                effectiveDependencies,
                allRuntimeNames);

            Assert.True(effectiveDependencies.ContainsKey(packageId));
            var actualPackage = effectiveDependencies[packageId].Implementations.Values.Single();
            Assert.Equal(expectedPackageName, actualPackage.Name);
            Assert.Equal(expectedPackageVersion, actualPackage.Version);
        }

        private RuntimeFile ParseRuntimeFile(string json)
        {
            return new RuntimeFileFormatter().ReadRuntimeFile(JObject.Parse(json));
        }
    }
}
