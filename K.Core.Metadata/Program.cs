using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using K.Core.Metadata;

namespace K.Core.Metadata
{
    class Program
    {
        public static void Main(string[] args)
        {
            PackageAssemblyManager pam = new PackageAssemblyManager();
            pam.StartViaCommandLine(args);
        }
    }
}