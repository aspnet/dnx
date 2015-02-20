using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NuGet.Frameworks
{
    /// <summary>
    /// A case insensitive compare of the framework, version, and profile
    /// </summary>
    public class NuGetFrameworkFullComparer : IEqualityComparer<NuGetFramework>
    {
        public bool Equals(NuGetFramework x, NuGetFramework y)
        {
            if (Object.ReferenceEquals(x, y))
            {
                return true;
            }

            if (Object.ReferenceEquals(x, null) || Object.ReferenceEquals(y, null))
            {
                return false;
            }

            return StringComparer.OrdinalIgnoreCase.Equals(x.Framework, y.Framework)
                && x.Version == y.Version
                && StringComparer.OrdinalIgnoreCase.Equals(x.Profile, y.Profile)
                && StringComparer.OrdinalIgnoreCase.Equals(x.Platform, y.Platform)
                && x.PlatformVersion == y.PlatformVersion;
        }

        public int GetHashCode(NuGetFramework obj)
        {
            return obj.ToString().ToLowerInvariant().GetHashCode();
        }
    }
}
