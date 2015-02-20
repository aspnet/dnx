using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NuGet.Resolver
{
    public enum DependencyBehavior
    {
        Ignore,
        Lowest,
        HighestPatch,
        HighestMinor,
        Highest
    }
}
