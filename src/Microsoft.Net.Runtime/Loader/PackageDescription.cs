using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Microsoft.Net.Runtime.Loader
{
    public class PackageDescription
    {
        public PackageReference Identity { get; set; }
        public IEnumerable<PackageReference> Dependencies { get; set; }
    }
}
