using NuGet.Frameworks;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NuGet.Packaging
{
    /// <summary>
    /// A group of items/files from a nupkg with the same target framework.
    /// </summary>
    public class FrameworkSpecificGroup
    {
        private readonly NuGetFramework _targetFramework;
        private readonly IEnumerable<string> _items;

        public FrameworkSpecificGroup(string targetFramework, IEnumerable<string> items)
        {
            if (items == null)
            {
                throw new ArgumentException("items");
            }

            if (String.IsNullOrEmpty(targetFramework))
            {
                _targetFramework = NuGetFramework.AnyFramework;
            }
            else
            {
                _targetFramework = NuGetFramework.Parse(targetFramework);
            }

            _items = items;
        }

        public FrameworkSpecificGroup(NuGetFramework targetFramework, IEnumerable<string> items)
        {
            if (targetFramework == null)
            {
                throw new ArgumentException("framework");
            }

            if (items == null)
            {
                throw new ArgumentException("items");
            }

            _targetFramework = targetFramework;
            _items = items;
        }

        /// <summary>
        /// Group target framework
        /// </summary>
        public NuGetFramework TargetFramework
        {
            get
            {
                return _targetFramework;
            }
        }

        /// <summary>
        /// Item relative paths
        /// </summary>
        public IEnumerable<string> Items
        {
            get
            {
                return _items;
            }
        }
    }
}
