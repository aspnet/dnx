using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NuGet.Frameworks
{
    public interface IPortableFrameworkMappings
    {
        /// <summary>
        /// Ex: 5 -> net4, win8
        /// </summary>
        IEnumerable<KeyValuePair<int, NuGetFramework[]>> ProfileFrameworks { get; }

        /// <summary>
        /// Additional optional frameworks supported in a portable profile.
        /// Ex: 5 -> MonoAndroid1+MonoTouch1
        /// </summary>
        IEnumerable<KeyValuePair<int, NuGetFramework[]>> ProfileOptionalFrameworks { get; }
    }
}
