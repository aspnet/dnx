using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Microsoft.Net.Project
{
    public class CrossgenOptions
    {
        // Is this correct?
        public string OutputPath { get; set; }

        public string CrossgenPath { get; set; }

        public IEnumerable<string> InputPaths { get; set; }
    }
}
