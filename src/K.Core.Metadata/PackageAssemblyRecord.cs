using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace K.Core.Metadata
{
    public class PackageAssemblyRecord
    {
        public PackageAssemblyManager PackageAssemblyManager { get; set; }


        const string AssemblyNameVersionSep = ",";
        const string PackageNameVersionSep = ".";

        public String AssemblyName { get; set; }
        public String AssemblyVersion { get; set; }

        public String AssemblyPathIL { get; set; }
        public String AssemblyPathNI { get; set; }

        public bool AssemblyInvalidNISelf { get; set; }
        public bool AssemblyInvalidNIDependencies { get; set; }

        public DateTime AssemblyLastWriteTimeIL { get; set; }
        public DateTime AssemblyLastWriteTimeNI { get; set; }

        public String PackageName { get; set; }
        public String PackageVersion { get; set; }
        public String PackagePath { get; set; }

        public List<String> AssemblyDependencies { get; set; }

        public PackageAssemblyRecord()
        {
            AssemblyDependencies = new List<String>();
            AssemblyInvalidNISelf = false;
            AssemblyInvalidNIDependencies = false;
        }

        public static bool IsMscorlib(string assemblyName)
        {
            if (String.Compare(assemblyName, "mscorlib", StringComparison.OrdinalIgnoreCase) == 0)
            {
                return true;
            }
            else
            {
                return false;
            }
        }

        public bool IsMscorlib()
        {
            return IsMscorlib(AssemblyName);
        }

        public string MakeAssemblyNameVersion()
        {
            return MakeAssemblyNameVersion(AssemblyName, AssemblyVersion);
        }

        public static string MakeAssemblyNameVersion(string AssemblyName, String AssemblyVersion)
        {
            return String.Format("{0}{1} {2}", AssemblyName.ToLower(), AssemblyNameVersionSep, AssemblyVersion);
        }

        public static string MakeAssemblyVersion(ASSEMBLYMETADATA assemblyMetaData)
        {
            return String.Format("{0}.{1}.{2}.{3}",
                                   assemblyMetaData.usMajorVersion,        // Major Version.
                                   assemblyMetaData.usMinorVersion,        // Minor Version.
                                   assemblyMetaData.usBuildNumber,         // Build Number.
                                   assemblyMetaData.usRevisionNumber       // Revision Number.
                                 );
        }

        public static string MakePackageNameVersion(string PackageName, String PackageVersion)
        {
            return String.Format("{0}{1}{2}", PackageName.ToLower(), PackageNameVersionSep, PackageVersion);
        }

        public void FindNativeImage()
        {
            // NI - look next to parentIL
            if ((String.IsNullOrEmpty(AssemblyPathNI)) && (PackageAssemblyManager.PackageAssemblyManagerEnvironment.ProbeForDependantAssemblyNextToParentILFile))
            {
                String localAssemblyPathNI = "";

                PackageAssemblyManager.ProbeAssembly(true, Path.GetDirectoryName(AssemblyPathIL), AssemblyName, out localAssemblyPathNI);
                if (localAssemblyPathNI.EndsWith(".ni.dll") == true)
                {
                    AssemblyPathNI = localAssemblyPathNI;

                    FileInfo fileInfo = new FileInfo(AssemblyPathNI);
                    AssemblyLastWriteTimeNI = fileInfo.LastWriteTime;
                }
            }

            // NI - look in package caches
            if ((String.IsNullOrEmpty(AssemblyPathNI)) && (PackageAssemblyManager.PackageAssemblyManagerEnvironment.ProbeForDependantAssemblyInPackageCachePath))
            {
                String localAssemblyPathNI = "";

                PackageAssemblyManager.ProbeAssemblyInPackageCaches(true, MakeAssemblyNameVersion(AssemblyName, AssemblyVersion), AssemblyName, out localAssemblyPathNI);

                if (AssemblyPathNI.EndsWith(".ni.dll") == true)
                {
                    AssemblyPathNI = localAssemblyPathNI;

                    FileInfo fileInfo = new FileInfo(AssemblyPathNI);
                    AssemblyLastWriteTimeNI = fileInfo.LastWriteTime;
                }
            }

            Console.WriteLine("ParseAssemblies_PhaseX_SourceAssemblyPaths IL {0} {1}",
                AssemblyPathIL,
                AssemblyLastWriteTimeIL);

            Console.WriteLine("ParseAssemblies_PhaseX_SourceAssemblyPaths NI {0} {1}",
                AssemblyPathNI,
                AssemblyLastWriteTimeNI);


            if (AssemblyLastWriteTimeIL > AssemblyLastWriteTimeNI)
            {
                AssemblyInvalidNISelf = true;
            }
        }
    }
}
