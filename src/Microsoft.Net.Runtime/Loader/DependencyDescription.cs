using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Microsoft.Net.Runtime.Loader
{
    public class DependencyDescription
    {
        public Dependency Identity { get; set; }
        public IEnumerable<Dependency> Dependencies { get; set; }
    }
}
