// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Versioning;
using Microsoft.Extensions.Internal;
using NuGet.Resources;

namespace NuGet
{
    /// <summary>
    /// Represent one profile of the .NET Portable library
    /// </summary>
    public class NetPortableProfile : IEquatable<NetPortableProfile>
    {
        private string _customProfile;

        /// <summary>
        /// Creates a portable profile with the given name and supported frameworks.
        /// </summary>
        public NetPortableProfile(string name, IEnumerable<FrameworkName> supportedFrameworks, IEnumerable<FrameworkName> optionalFrameworks = null)
            // This zero version is compatible with the existing behavior, which used 
            // the string "v0.0" as the version for constructed instances of this class always.
            : this("v0.0", name, supportedFrameworks, optionalFrameworks)
        {
        }

        // NOTE: this is a new constructor provided, which passes in the framework version 
        // of the given profile in addition to the name. 
        // The existing behavior was to pass "v0.0" as the framework version, so 
        // that's what the fixed parameter is in the backwards-compatible constructor above.
        /// <summary>
        /// Creates a portable profile for the given framework version, profile name and 
        /// supported frameworks.
        /// </summary>
        /// <param name="frameworkDirectory">.NET framework version that the profile belongs to, like "v4.0".</param>
        /// <param name="name">Name of the portable profile, like "win+net45".</param>
        /// <param name="supportedFrameworks">Supported frameworks.</param>
        public NetPortableProfile(string frameworkDirectory, string name, IEnumerable<FrameworkName> supportedFrameworks, IEnumerable<FrameworkName> optionalFrameworks)
        {
            if (String.IsNullOrEmpty(frameworkDirectory))
            {
                throw new ArgumentNullException(nameof(frameworkDirectory));
            }
            if (String.IsNullOrEmpty(name))
            {
                throw new ArgumentNullException(nameof(name));
            }

            if (supportedFrameworks == null)
            {
                throw new ArgumentNullException(nameof(supportedFrameworks));
            }

            var frameworks = supportedFrameworks.ToList();
            if (frameworks.Any(f => f == null))
            {
                throw new ArgumentException(NuGetResources.SupportedFrameworkIsNull, nameof(supportedFrameworks));
            }

            if (frameworks.Count == 0)
            {
                throw new ArgumentOutOfRangeException(nameof(supportedFrameworks));
            }

            Name = name;
            SupportedFrameworks = new ReadOnlyHashSet<FrameworkName>(frameworks);
            OptionalFrameworks = new ReadOnlyHashSet<FrameworkName>(optionalFrameworks ?? Enumerable.Empty<FrameworkName>());
            FrameworkDirectory = frameworkDirectory;
            FrameworkVersion = new DirectoryInfo(frameworkDirectory).Name.TrimStart('v');
            FrameworkName = new FrameworkName(".NETPortable", Version.Parse(FrameworkVersion), Name);
        }

        /// <summary>
        /// Gets the profile name.
        /// </summary>
        public string Name { get; private set; }

        public FrameworkName FrameworkName { get; set; }

        /// <summary>
        /// Gets the framework version that this profile belongs to.
        /// </summary>
        public string FrameworkDirectory { get; private set; }

        public string FrameworkVersion { get; private set; }

        public ISet<FrameworkName> SupportedFrameworks { get; private set; }

        public ISet<FrameworkName> OptionalFrameworks { get; private set; }

        public bool Equals(NetPortableProfile other)
        {
            // NOTE: equality and hashcode does not change when you add Version, since 
            // no two profiles across framework versions have the same name.
            return Name.Equals(other.Name, StringComparison.OrdinalIgnoreCase) &&
                   SupportedFrameworks.SetEquals(other.SupportedFrameworks) &&
                   OptionalFrameworks.SetEquals(other.OptionalFrameworks);
        }

        public override int GetHashCode()
        {
            var combiner = new HashCodeCombiner();
            combiner.Add(Name);
            combiner.Add(SupportedFrameworks);
            combiner.Add(OptionalFrameworks);
            return combiner.CombinedHash;
        }

        /// <summary>
        /// Returns the string that represents all supported frameworks by this profile, separated by the + sign.
        /// </summary>
        /// <example>
        /// sl4+net45+windows8
        /// </example>
        public string CustomProfileString
        {
            get
            {
                if (_customProfile == null)
                {
                    _customProfile = String.Join("+", SupportedFrameworks.Concat(OptionalFrameworks).Select(f => VersionUtility.GetShortFrameworkName(f)));
                }

                return _customProfile;
            }
        }

        public bool IsCompatibleWith(NetPortableProfile other)
        {
            if (other == null)
            {
                throw new ArgumentNullException(nameof(other));
            }

            return other.SupportedFrameworks.All(
                projectFramework => this.SupportedFrameworks.Any(
                    packageFramework => VersionUtility.IsCompatible(projectFramework, packageFramework)));
        }

        public bool IsCompatibleWith(FrameworkName framework)
        {
            if (framework == null)
            {
                throw new ArgumentNullException(nameof(framework));
            }

            return SupportedFrameworks.Any(f => VersionUtility.IsCompatible(framework, f));
        }

        /// <summary>
        /// Attempt to parse a profile string into an instance of <see cref="NetPortableProfile"/>.
        /// The profile string can be either ProfileXXX or sl4+net45+wp7
        /// </summary>
        public static NetPortableProfile Parse(string profileValue)
        {
            if (String.IsNullOrEmpty(profileValue))
            {
                throw new ArgumentNullException(nameof(profileValue));
            }

            // Previously, only the full "ProfileXXX" long .NET name could be used for this method.
            // This was inconsistent with the way the "custom profile string" (like "sl4+net45+wp7")
            // was supported in other places. By fixing the way the profile table indexes the cached 
            // profiles, we can now indeed access by either naming, so we don't need the old check 
            // for the string starting with "Profile".
            var result = NetPortableProfileTable.GetProfile(profileValue);
            if (result != null)
            {
                return result;
            }

            if (profileValue.StartsWith("Profile", StringComparison.OrdinalIgnoreCase))
            {
                // This can happen if profileValue is an unrecognized profile, or
                // for some rare cases, the Portable Profile files are missing on disk. 
                return null;
            }

            VersionUtility.ValidatePortableFrameworkProfilePart(profileValue);

            var supportedFrameworks = profileValue.Split(new [] {'+'}, StringSplitOptions.RemoveEmptyEntries)
                                                  .Select(VersionUtility.ParseFrameworkName);

            return new NetPortableProfile(profileValue, supportedFrameworks);
        }
    }
}
