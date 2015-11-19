using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Versioning;
using System.Threading.Tasks;
using Microsoft.Dnx.Runtime;
using Microsoft.Extensions.PlatformAbstractions;
using Microsoft.Dnx.Tooling.Restore.RuntimeModel;
using NuGet;

namespace Microsoft.Dnx.Tooling
{
    internal class ImplicitPackagesWalkProvider : IWalkProvider
    {
        // Based on values from the real Microsoft.NETCore.Platforms, but without 'aot' since DNX doesn't support .NET Native
        // These are designed to be an in-memory equivalent to a runtime.json.
        // Each "RuntimeSpec" defines a RID. The first parameter to the constructor is the name of the RID and the
        // remaining parameters are the ordered list of RIDs that runtime imports. For example, the following constructor:
        //
        //      new RuntimeSpec("win10-x86", "win10", "win81-x86")
        //
        // Is equivalent to the following segment in runtime.json:
        //
        //      {
        //          "win10-x86": {
        //              "#import": [ "win10", "win81-x86" ]
        //          }
        //      }
        //
        internal static readonly RuntimeFile ImplicitRuntimeFile = new RuntimeFile(
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
            new RuntimeSpec("win10-arm", "win10", "win81-arm"),

            new RuntimeSpec("unix", "any"),
            new RuntimeSpec("unix-x64", "unix"),

            new RuntimeSpec("osx", "unix"),
            new RuntimeSpec("osx-x64", "osx", "unix-x64"),

            // Mac OS X Yosemite
            new RuntimeSpec("osx.10.10", "osx"),
            new RuntimeSpec("osx.10.10-x64", "osx.10.10", "osx-x64"),

            // Mac OS X El Capitan
            new RuntimeSpec("osx.10.11", "osx.10.10"),
            new RuntimeSpec("osx.10.11-x64", "osx.10.11", "osx.10.10-x64"),

            new RuntimeSpec("linux", "unix"),
            new RuntimeSpec("linux-x64", "linux", "unix-x64"),

            new RuntimeSpec("centos", "linux"),
            new RuntimeSpec("centos-x64", "centos", "linux-x64"),

            new RuntimeSpec("centos.7.1", "centos"),
            new RuntimeSpec("centos.7.1-x64", "centos.7.1", "centos-x64"),

            // CentOS identifies itself as "7", but the BCL packages use "7.1" so we need this weird reversal mapping
            new RuntimeSpec("centos.7", "centos.7.1"),
            new RuntimeSpec("centos.7-x64", "centos.7", "centos.7.1-x64"),

            new RuntimeSpec("ubuntu", "linux"),
            new RuntimeSpec("ubuntu-x64", "ubuntu", "linux-x64"),

            new RuntimeSpec("ubuntu.14.04", "ubuntu"),
            new RuntimeSpec("ubuntu.14.04-x64", "ubuntu.14.04", "ubuntu-x64"),


            // Informally supported RIDS: These are RIDs we believe work, but do not formally support

            // Ubuntu 14.10 exists and is compatible with 14.04
            new RuntimeSpec("ubuntu.14.10", "ubuntu.14.04"),
            new RuntimeSpec("ubuntu.14.10-x64", "ubuntu.14.10", "ubuntu.14.04-x64"),

            // Ubuntu 15.04 is believed to be compatible
            new RuntimeSpec("ubuntu.15.04", "ubuntu.14.10"),
            new RuntimeSpec("ubuntu.15.04-x64", "ubuntu.15.04", "ubuntu.14.10-x64"),

            // Ubuntu 15.10 is not compatible. It upgraded icu to 55
            
            // Linux Mint 17.x is compatible with Ubuntu 14.04
            new RuntimeSpec("linuxmint.17", "ubuntu.14.04"),
            new RuntimeSpec("linuxmint.17-x64", "linuxmint.17", "ubuntu.14.04-x64"),
            new RuntimeSpec("linuxmint.17.1", "linuxmint.17"),
            new RuntimeSpec("linuxmint.17.1-x64", "linuxmint.17-x64"),
            new RuntimeSpec("linuxmint.17.2", "linuxmint.17.1"),
            new RuntimeSpec("linuxmint.17.2-x64", "linuxmint.17.2", "linuxmint.17.1-x64"),
            new RuntimeSpec("linuxmint.17.3", "linuxmint.17.2"),
            new RuntimeSpec("linuxmint.17.3-x64", "linuxmint.17.3", "linuxmint.17.2-x64"));

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
            if (libraryRange.Name.Equals(ImplicitRuntimePackageConstants.ImplicitRuntimePackageId))
            {
                return Task.FromResult(new WalkProviderMatch()
                {
                    Provider = this,
                    LibraryType = LibraryTypes.Implicit,
                    Library = new LibraryIdentity(
                        ImplicitRuntimePackageConstants.ImplicitRuntimePackageId,
                        ImplicitRuntimePackageConstants.ImplicitRuntimePackageVersion, 
                        isGacOrFrameworkReference: false)
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
            if (match.Library.Name.Equals(ImplicitRuntimePackageConstants.ImplicitRuntimePackageId) && 
                match.Library.Version.Equals(ImplicitRuntimePackageConstants.ImplicitRuntimePackageVersion))
            {
                return Task.FromResult(ImplicitRuntimeFile);
            }
            return Task.FromResult<RuntimeFile>(null);
        }
    }
}
