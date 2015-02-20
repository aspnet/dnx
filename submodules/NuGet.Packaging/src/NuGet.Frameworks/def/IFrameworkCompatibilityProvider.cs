using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NuGet.Frameworks
{
    public interface IFrameworkCompatibilityProvider
    {
        /// <summary>
        /// Ex: IsCompatible(net45, net40) -> true
        /// Ex: IsCompatible(net40, net45) -> false
        /// </summary>
        /// <param name="framework">Project target framework</param>
        /// <param name="other">Library framework that is going to be installed</param>
        /// <returns>True if framework supports other</returns>
        bool IsCompatible(NuGetFramework framework, NuGetFramework other);
    }
}
