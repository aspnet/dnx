using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NuGet.Frameworks
{
    /// <summary>
    /// An inclusive range of frameworks
    /// </summary>
    public class FrameworkRange
    {
        private readonly NuGetFramework _minFramework;
        private readonly NuGetFramework _maxFramework;

        public FrameworkRange(NuGetFramework min, NuGetFramework max)
        {
            if (min == null)
            {
                throw new ArgumentException("min");
            }

            if (max == null)
            {
                throw new ArgumentException("max");
            }

            if (!SameExceptForVersion(min, max))
            {
                throw new Exception("Frameworks must have the same identifier, profile, and platform");
            }

            _minFramework = min;
            _maxFramework = max;
        }

        public NuGetFramework Min
        {
            get
            {
                return _minFramework;
            }
        }

        public NuGetFramework Max
        {
            get
            {
                return _maxFramework;
            }
        }

        public string FrameworkIdentifier
        {
            get
            {
                return Min.Framework;
            }
        }

        public bool Satisfies(NuGetFramework framework)
        {
            return SameExceptForVersion(_minFramework, framework)
                && _minFramework.Version <= framework.Version
                && _maxFramework.Version >= framework.Version;

            // TODO: platform version check?
        }


        // TODO: should the range be 2D and work on both framework and platform versions?
        private static bool SameExceptForVersion(NuGetFramework x, NuGetFramework y)
        {
            return StringComparer.OrdinalIgnoreCase.Equals(x.Framework, y.Framework)
                && (StringComparer.OrdinalIgnoreCase.Equals(x.Profile, y.Profile))
                && (StringComparer.OrdinalIgnoreCase.Equals(x.Platform, y.Platform));
        }

        public override string ToString()
        {
            return String.Format(CultureInfo.InvariantCulture, "[{0}, {1}]", Min.ToString(), Max.ToString());
        }
    }
}
