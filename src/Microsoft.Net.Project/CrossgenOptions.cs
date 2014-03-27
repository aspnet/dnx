using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Microsoft.Net.Project
{
    public class CrossgenOptions
    {
        public string CrossgenPath { get; set; }

        public string RuntimePath { get; set; }

        public IEnumerable<string> InputPaths { get; set; }
        
        public bool Symbols { get; set; }
    }
}
