using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NuGet.Frameworks
{
    public class OneWayCompatibilityMappingEntry : IEquatable<OneWayCompatibilityMappingEntry>
    {
        private readonly FrameworkRange _targetFramework;
        private readonly FrameworkRange _supportedFramework;

        /// <summary>
        /// Creates a one way compatibility mapping.
        /// Ex: net -supports-> native
        /// </summary>
        /// <param name="framework">Project framework</param>
        /// <param name="supportedFramework">Framework that is supported by the project framework</param>
        public OneWayCompatibilityMappingEntry(FrameworkRange targetFramework, FrameworkRange supportedFramework)
        {
            _targetFramework = targetFramework;
            _supportedFramework = supportedFramework;
        }

        /// <summary>
        /// Primary framework range or project target framework that supports the SuppportedFrameworkRange
        /// </summary>
        public FrameworkRange TargetFrameworkRange
        {
            get
            {
                return _targetFramework;
            }
        }

        /// <summary>
        /// Framework range that is supported by the TargetFrameworkRange
        /// </summary>
        public FrameworkRange SupportedFrameworkRange
        {
            get
            {
                return _supportedFramework;
            }
        }

        public static CompatibilityMappingComparer Comparer
        {
            get
            {
                return new CompatibilityMappingComparer();
            }
        }

        public bool Equals(OneWayCompatibilityMappingEntry other)
        {
            return Comparer.Equals(this, other);
        }

        public override string ToString()
        {
            return String.Format(CultureInfo.InvariantCulture, "{0} -> {1}", TargetFrameworkRange.ToString(), SupportedFrameworkRange.ToString());
        }
    }
}
