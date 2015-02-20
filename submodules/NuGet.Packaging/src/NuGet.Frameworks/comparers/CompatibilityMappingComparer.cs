using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NuGet.Frameworks
{
    public class CompatibilityMappingComparer : IEqualityComparer<OneWayCompatibilityMappingEntry>
    {
        public bool Equals(OneWayCompatibilityMappingEntry x, OneWayCompatibilityMappingEntry y)
        {
            if (Object.ReferenceEquals(x, y))
            {
                return true;
            }

            if (Object.ReferenceEquals(x, null) || Object.ReferenceEquals(y, null))
            {
                return false;
            }

            // TODO: improve this
            return x.GetHashCode() == y.GetHashCode();
        }

        public int GetHashCode(OneWayCompatibilityMappingEntry obj)
        {
            return obj.ToString().ToLowerInvariant().GetHashCode();
        }
    }
}
