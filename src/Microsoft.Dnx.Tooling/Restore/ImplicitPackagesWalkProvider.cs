using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Versioning;
using System.Threading.Tasks;
using Microsoft.Dnx.Runtime;
using Microsoft.Dnx.Tooling.Restore.RuntimeModel;
using NuGet;

namespace Microsoft.Dnx.Tooling
{
    internal class ImplicitPackagesWalkProvider : IWalkProvider
    {
        // Based on values from the real Microsoft.NETCore.Platforms, but without 'aot' since DNX doesn't support .NET Native
        private static readonly RuntimeFile ImplicitRuntimeFile = new RuntimeFile(
            new RuntimeSpec("base"),
            new RuntimeSpec("any", "base"),
            new RuntimeSpec("win", "any"),
            new RuntimeSpec("win-x86", "win"),
            new RuntimeSpec("win-x64", "win"),
            new RuntimeSpec("win7", "win"),
            new RuntimeSpec("win7-x86", "win7", "win-x86"),
            new RuntimeSpec("win7-x64", "win7", "win-x64"),
            new RuntimeSpec("win8", "win7"),
            new RuntimeSpec("win8-x86", "win8", "win7-x86"),
            new RuntimeSpec("win8-x64", "win8", "win7-x64"),
            new RuntimeSpec("win8-arm", "win8"),
            new RuntimeSpec("win81", "win8"),
            new RuntimeSpec("win81-x86", "win81", "win8-x86"),
            new RuntimeSpec("win81-x64", "win81", "win8-x64"),
            new RuntimeSpec("win81-arm", "win81", "win8-arm"),
            new RuntimeSpec("win10", "win81"),
            new RuntimeSpec("win10-x86", "win10", "win81-x86"),
            new RuntimeSpec("win10-x64", "win10", "win81-x64"),
            new RuntimeSpec("win10-arm", "win10", "win81-arm"));

        public static readonly SemanticVersion ImplicitRuntimePackageVersion = new SemanticVersion("0.0.0");
        public static readonly string ImplicitRuntimePackageId = "Microsoft.NETCore.Platforms";

        public bool IsHttp
        {
            get
            {
                return false;
            }
        }

        public Task CopyToAsync(WalkProviderMatch match, Stream stream)
        {
            // Do nothing, the package is embedded
            return Task.FromResult(0);
        }

        public Task<WalkProviderMatch> FindLibrary(LibraryRange libraryRange, FrameworkName targetFramework, bool includeUnlisted)
        {
            // If the package matches an embedded package, return it
            if (libraryRange.Name.Equals(ImplicitRuntimePackageId))
            {
                return Task.FromResult(new WalkProviderMatch()
                {
                    Provider = this,
                    Library = new LibraryIdentity(ImplicitRuntimePackageId, ImplicitRuntimePackageVersion, isGacOrFrameworkReference: false)
                });
            }
            return Task.FromResult<WalkProviderMatch>(null);
        }

        public Task<IEnumerable<LibraryDependency>> GetDependencies(WalkProviderMatch match, FrameworkName targetFramework)
        {
            return Task.FromResult(Enumerable.Empty<LibraryDependency>());
        }

        public Task<RuntimeFile> GetRuntimes(WalkProviderMatch match, FrameworkName targetFramework)
        {
            if (match.Library.Name.Equals(ImplicitRuntimePackageId) && match.Library.Version.Equals(ImplicitRuntimePackageVersion))
            {
                return Task.FromResult(ImplicitRuntimeFile);
            }
            return Task.FromResult<RuntimeFile>(null);
        }
    }
}