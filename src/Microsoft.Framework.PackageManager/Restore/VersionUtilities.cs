using NuGet;
using System;
using System.Collections.Generic;

namespace Microsoft.Framework.PackageManager
{
    public static class VersionUtilities
    {
        public static bool ShouldUseConsidering(
            SemanticVersion current,
            SemanticVersion considering, 
            SemanticVersion ideal)
        {
            if (considering == null)
            {
                // skip nulls
                return false;
            }
            if (!considering.EqualsSnapshot(ideal) && considering < ideal)
            {
                // don't use anything that's less than the requested version
                return false;
            }
            if (current == null)
            {
                // always use version when it's the first valid
                return true;
            }
            if (current.EqualsSnapshot(ideal) &&
                considering.EqualsSnapshot(ideal))
            {
                // favor higher version when they both match a snapshot patter
                return current < considering;
            }
            else
            {
                // otherwise favor lower version
                return current > considering;
            }
        }
    }
}
