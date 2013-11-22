using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace K.Core.Metadata
{
    public class PackageAssemblyEnvironment
    {
        public bool CoreGenEnabled { get; set; }

        public bool ProbeForDependantAssemblyNextToParentILFile { get; set; }
        public bool ProbeForDependantAssemblyInPackageCachePath { get; set; }
        public bool DisplayAssembliesAndDependencies { get; set; }

        public PackageAssemblyEnvironment()
        {
            CoreGenEnabled = true; // = false; 

            ProbeForDependantAssemblyNextToParentILFile = true;
            ProbeForDependantAssemblyInPackageCachePath = true;

            DisplayAssembliesAndDependencies = true;
        }
    }

}
