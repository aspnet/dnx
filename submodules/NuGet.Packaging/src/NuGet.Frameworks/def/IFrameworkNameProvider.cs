using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NuGet.Frameworks
{
    public interface IFrameworkNameProvider
    {
        /// <summary>
        /// Returns the official framework identifier for an alias or short name.
        /// </summary>
        bool TryGetIdentifier(string identifierShortName, out string identifier);

        /// <summary>
        /// Gives the short name used for folders in NuGet
        /// </summary>
        bool TryGetShortIdentifier(string identifier, out string identifierShortName);

        /// <summary>
        /// Get the official profile name from the short name.
        /// </summary>
        bool TryGetProfile(string frameworkIdentifier, string profileShortName, out string profile);

        /// <summary>
        /// Returns the shortened version of the profile name.
        /// </summary>
        bool TryGetShortProfile(string frameworkIdentifier, string profile, out string profileShortName);

        /// <summary>
        /// Parses a version string using single digit rules if no dots exist
        /// </summary>
        bool TryGetVersion(string versionString, out Version version);

        /// <summary>
        /// Returns a shortened version. If all digits are single digits no dots will be used.
        /// </summary>
        string GetVersionString(Version version);

        /// <summary>
        /// Looks up the portable profile number based on the framework list.
        /// </summary>
        bool TryGetPortableProfile(IEnumerable<NuGetFramework> supportedFrameworks, out int profileNumber);

        /// <summary>
        /// Returns the frameworks based on a portable profile number.
        /// </summary>
        bool TryGetPortableFrameworks(int profile, out IEnumerable<NuGetFramework> frameworks);

        /// <summary>
        /// Returns the frameworks based on a portable profile number.
        /// </summary>
        bool TryGetPortableFrameworks(int profile, bool includeOptional, out IEnumerable<NuGetFramework> frameworks);

        /// <summary>
        /// Returns the frameworks based on a profile string.
        /// Profile can be either the number in format: Profile=7, or the shortened NuGet version: net45+win8
        /// </summary>
        bool TryGetPortableFrameworks(string profile, bool includeOptional, out IEnumerable<NuGetFramework> frameworks);

        /// <summary>
        /// Parses a shortened portable framework profile list.
        /// Ex: net45+win8
        /// </summary>
        bool TryGetPortableFrameworks(string shortPortableProfiles, out IEnumerable<NuGetFramework> frameworks);

        /// <summary>
        /// Returns a list of all possible substitutions where the framework name
        /// have equivalents.
        /// Ex: sl3 -> wp8
        /// </summary>
        bool TryGetEquivalentFrameworks(NuGetFramework framework, out IEnumerable<NuGetFramework> frameworks);

        /// <summary>
        /// Gives all substitutions for a framework range.
        /// </summary>
        bool TryGetEquivalentFrameworks(FrameworkRange range, out IEnumerable<NuGetFramework> frameworks);

        /// <summary>
        /// Returns ranges of frameworks that are known to be supported by the given framework.
        /// Ex: net45 -> native
        /// </summary>
        bool TryGetCompatibilityMappings(NuGetFramework framework, out IEnumerable<FrameworkRange> supportedFrameworkRanges);

        /// <summary>
        /// Returns all sub sets of the given framework.
        /// Ex: .NETFramework -> .NETCore
        /// These will have the same version, but a different framework
        /// </summary>
        bool TryGetSubSetFrameworks(string frameworkIdentifier, out IEnumerable<string> subSetFrameworkIdentifiers);
    }
}
