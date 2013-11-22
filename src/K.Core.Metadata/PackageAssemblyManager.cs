

//@@ TODO check timestamps of IL vs NI image of dependencies vs self ?

//@@ TODO check timestamps of IL vs NI image of self

//@@ TODO only take highest version of package + assembly

// need to build a list of packages and package dependencies
// build a sorted list of packages and versions (Descending)
//

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Runtime.InteropServices;
using System.Security;
using Microsoft.Win32.SafeHandles;

using mdToken = System.Int32;
using HCORENUM = System.IntPtr;

namespace K.Core.Metadata
{
    public class PackageAssemblyManager
    {
        public PackageAssemblyEnvironment PackageAssemblyManagerEnvironment = new PackageAssemblyEnvironment();

        const int MAX_PATH = 256;

        static Guid MetaDataDispenserGUID;
        static Guid IMetaDataDispenserGUID;
        static Guid IMetaDataAssemblyImportGUID;

        string ProcessArchitecture = null;
        string[] PackagesPathSearch = null;

        string PackageManifestFileName = "packages.filelist.config";
        Dictionary<String, String> PackageManifestDictionary = new Dictionary<
                                                        String,         //AssemblyName+AssemblyVersion
                                                        String          //PackageName+PackageVersion
                                                        >();

        string[] PackagesFrameworks = { "kite", "net45", "net40", "portable" };


        bool fCollectWarnings = true;
        List<string> CollectedWarnings = new List<string>();

        bool fCollectErrors = true;
        List<string> CollectedErrors = new List<string>();



        List<string> InputAssemblyFilePaths = new List<string>();

        Dictionary<String, PackageAssemblyRecord> CommonAssemblyDictionary = new Dictionary<
                                                        String,  //AssemblyName+AssemblyVersion
                                                        PackageAssemblyRecord
                                                        >();


        public bool LoadPackageManifest(string PackageManifestFileName)
        {
            Console.WriteLine("LoadPackageManifest {0}", PackageManifestFileName);
            char[] seps = new char[] { ' ' };
            if (!File.Exists(PackageManifestFileName))
            {
                return false;
            }

            var lines = File.ReadLines(PackageManifestFileName);
            foreach (var line in lines)
            {
                if ((String.IsNullOrEmpty(line)) || (line[0] == '#') || (line[0] == '/'))
                {
                    continue;

                }
                string[] parts = line.Split(seps);

                if (parts.Length != 4)
                {
                    continue;
                }

                string packageName = parts[0];
                string packageVersion = parts[1];
                string packageNameVersionKey = PackageAssemblyRecord.MakePackageNameVersion(packageName, packageVersion);

                string assemblyName = parts[2];
                string assemblyVersion = parts[3];

                string assemblyNameVersionKey = PackageAssemblyRecord.MakeAssemblyNameVersion(assemblyName, assemblyVersion);

                if (PackageManifestDictionary.ContainsKey(assemblyNameVersionKey) == false)
                {
                    PackageManifestDictionary.Add(assemblyNameVersionKey, packageNameVersionKey);
                    Console.WriteLine("KDepend.LoadPackageManifest K:{0} V:{1}", assemblyNameVersionKey, packageNameVersionKey);
                }
            }

            Console.WriteLine("KDepend.LoadPackageManifest Count: {0}", PackageManifestDictionary.Count);

            return true; ;
        }

        public bool LoadPackageManifests()
        {
            bool fReturn = true;

            foreach (string packagePathSearch in PackagesPathSearch)
            {
                string packagePathSearchPackageManifest = Path.Combine(packagePathSearch, PackageManifestFileName);
                LoadPackageManifest(packagePathSearchPackageManifest);
            }

            LoadPackageManifest(PackageManifestFileName);

            return fReturn;
        }

        //Helpers

        public bool DoesAssemblyExist(
            string directoryPath,
            string assemblyName,
            string extension)
        {
            if (String.IsNullOrEmpty(directoryPath))
            {
                return false;
            }

            string assemblyPath = Path.Combine(directoryPath, assemblyName + extension);

            if (File.Exists(assemblyPath.ToString()))
            {
                //Console.WriteLine("DoesAssemblyExist T {0}", assemblyPath.ToString());
                return true;
            }
            else
            {
                //Console.WriteLine("DoesAssemblyExist F {0}", assemblyPath.ToString());
                return false;
            }
        }

        public void ProbeAssembly(bool LookForNI, string DirectoryPath, string AssemblyName, out string AssemblyPath)
        {
            var niExtension = ".ni.dll";
            var ilExtension = ".dll";
            string localResult = "";

            //prime - out params
            AssemblyPath = "";

            if (String.IsNullOrEmpty(DirectoryPath))
            {
                return;
            }

            //probe for process platform i.e. x86 or AMD64 i.e. x86\Foo.ni.dll
            String DirectoryPathNI = Path.Combine(DirectoryPath, ProcessArchitecture);

            if ((LookForNI) && (DoesAssemblyExist(DirectoryPathNI, AssemblyName, niExtension)))
            {
                localResult = Path.Combine(DirectoryPathNI, AssemblyName + niExtension);
                AssemblyPath = localResult;
            }
            else if ((LookForNI) && (DoesAssemblyExist(DirectoryPath, AssemblyName, niExtension)))
            {
                localResult = Path.Combine(DirectoryPath, AssemblyName + niExtension);
                AssemblyPath = localResult;
            }
            else if (DoesAssemblyExist(DirectoryPath, AssemblyName, ilExtension))
            {
                localResult = Path.Combine(DirectoryPath, AssemblyName + ilExtension);
                AssemblyPath = localResult;
            }
        }

        public void ProbeAssemblyInPackageCaches(bool LookForNI, string AssemblyNameVersionKey, string AssemblyName, out string AssemblyPath)
        {
            bool FoundAssembly = false;
            AssemblyPath = "";

            /*
            Console.WriteLine("ProbeAssemblyInPackageCaches-A {0} {1}", 
                            LookForNI.ToString(),
                            AssemblyNameVersionKey
                        );
            */

            if (PackageManifestDictionary.ContainsKey(AssemblyNameVersionKey) == true)
            {
                string packageNameVersion = PackageManifestDictionary[AssemblyNameVersionKey];

                foreach (string PackageCachePath in PackagesPathSearch)
                {
                    foreach (string PackageFramework in PackagesFrameworks)
                    {
                        string directoryPath = Path.Combine(PackageCachePath, packageNameVersion);
                        if (Directory.Exists(directoryPath) == false)
                            continue;

                        directoryPath = Path.Combine(directoryPath, "lib");
                        directoryPath = Path.Combine(directoryPath, PackageFramework);
                        if (Directory.Exists(directoryPath) == false)
                            continue;

                        string tempAssemblyPath;

                        ProbeAssembly(LookForNI, directoryPath, AssemblyName, out tempAssemblyPath);
                        /*
                        Console.WriteLine("ProbeAssemblyInPackageCaches-B {0} {1} {2} {3}", 
                                        LookForNI.ToString(),
                                        directoryPath,
                                        AssemblyName,
                                        tempAssemblyPath
                                    );
                        */

                        if (
                            ((LookForNI == false) && (tempAssemblyPath.EndsWith(".dll") == true))
                            ||
                            ((LookForNI == true) && (tempAssemblyPath.EndsWith(".ni.dll") == true))
                            )
                        {
                            AssemblyPath = tempAssemblyPath;
                            FoundAssembly = true;

                            //Console.WriteLine("ProbeAssemblyInPackageCaches-C AssemblyPath {0}", AssemblyPath);

                            return;
                        }
                        else
                        {
                            //Console.WriteLine("ProbeAssemblyInPackageCaches-D FAIL");
                        }
                    }
                    if (FoundAssembly == true)
                    {
                        break;
                    }
                }
            }
        }


        // Phase 1 - given a list of assemblies
        //              Find their immediate dependencies and store in a list
        //
        // Phase 2 - given the Phase 1 list of asemblies
        //              check if their dependencies (phase 2 list) have NI image
        //

        // Phase X - run coregen.exe for each assembly
        //              pass full paths to NI images for each dependency
        //

        public Dictionary<String, PackageAssemblyRecord> AssemblyDictionary_Clone(Dictionary<String, PackageAssemblyRecord> sourceAssemblyDictionary)
        {
            var cloneAssemblyDictionary = new Dictionary<String, PackageAssemblyRecord>(sourceAssemblyDictionary.Count);

            foreach (var sourceAssemblyItem in sourceAssemblyDictionary)
            {
                cloneAssemblyDictionary.Add(sourceAssemblyItem.Key, sourceAssemblyItem.Value);
            }

            return cloneAssemblyDictionary;
        }

        public int ParseAssemblies_PackageAssemblyRecord_FindAssemblyPathIL(PackageAssemblyRecord assemblyRecord)
        {

            return 0;
        }

        public int ParseAssemblies_PackageAssemblyRecord_FindAssemblyPathNI(PackageAssemblyRecord assemblyRecord)
        {

            return 0;
        }

        public int ParseAssemblies_PackageAssemblyRecord_FindDependencies(
                                            IMetaDataDispenser metaDataDispenser,
                                            IMetaDataAssemblyImport assemblyImport,
                                            PackageAssemblyRecord assemblyRecord,
                                            Dictionary<String, PackageAssemblyRecord> DependencyAssemblyDictionary
        )
        {
            int newDependenciesFound = 0;

            //Console.WriteLine("ParseAssemblies_PackageAssemblyRecord_FindDependencies {0}", PackageAssemblyRecord.MakeAssemblyNameVersion(assemblyRecord.AssemblyName, assemblyRecord.AssemblyVersion));

            //Dependencies
            HCORENUM corEnum = new HCORENUM();
            for (; ; )
            {
                int cTokens;
                mdToken refToken;
                assemblyImport.EnumAssemblyRefs(ref corEnum, out refToken, 1, out cTokens);
                if (cTokens == 0)
                    break;

                int chName;

                ASSEMBLYMETADATA assemblyMetaData;

                StringBuilder assemblyNameBuilder = new StringBuilder(MAX_PATH);
                String assemblyName;

                assemblyImport.GetAssemblyRefProps(refToken,
                            IntPtr.Zero,
                            IntPtr.Zero,
                            assemblyNameBuilder,
                            assemblyNameBuilder.Capacity,
                            out chName,
                            out assemblyMetaData,   //IntPtr.Zero, //IntPtr to struct ASSEMBLYMETADATA
                            IntPtr.Zero,
                            IntPtr.Zero,
                            IntPtr.Zero);
                assemblyName = assemblyNameBuilder.ToString();

                string assemblyVersion = PackageAssemblyRecord.MakeAssemblyVersion(assemblyMetaData);

                Console.WriteLine("\tDependency: {0} Version: {1}", assemblyName, assemblyVersion);

                string assemblyNameVersionKey = PackageAssemblyRecord.MakeAssemblyNameVersion(assemblyName, assemblyVersion);

                //add to list of dependencies
                if (assemblyRecord.AssemblyDependencies.Contains(assemblyNameVersionKey) == false)
                {
                    assemblyRecord.AssemblyDependencies.Add(assemblyNameVersionKey);
                    newDependenciesFound++;
                }

                PackageAssemblyRecord assemblyRecordInner = null;

                if (DependencyAssemblyDictionary.ContainsKey(assemblyNameVersionKey) == true)
                {
                    assemblyRecordInner = DependencyAssemblyDictionary[assemblyNameVersionKey];
                }

                if (DependencyAssemblyDictionary.ContainsKey(assemblyNameVersionKey) == false)
                {
                    assemblyRecordInner = new PackageAssemblyRecord();
                    assemblyRecordInner.AssemblyName = assemblyName;
                    assemblyRecordInner.AssemblyVersion = assemblyVersion;
                    assemblyRecordInner.AssemblyPathIL = "";
                    assemblyRecordInner.AssemblyPathNI = "";

                    DependencyAssemblyDictionary.Add(assemblyNameVersionKey, assemblyRecordInner);
                    newDependenciesFound++;
                }

                //@@probe for paths

                // IL - look next to parentIL
                if ((String.IsNullOrEmpty(assemblyRecordInner.AssemblyPathIL)) && (PackageAssemblyManagerEnvironment.ProbeForDependantAssemblyNextToParentILFile))
                {
                    String AssemblyPathIL = "";

                    ProbeAssembly(false, Path.GetDirectoryName(assemblyRecord.AssemblyPathIL), assemblyName, out AssemblyPathIL);
                    if (AssemblyPathIL.EndsWith(".dll") == true)
                    {
                        assemblyRecordInner.AssemblyPathIL = AssemblyPathIL;

                        FileInfo fileInfo = new FileInfo(assemblyRecordInner.AssemblyPathIL);
                        assemblyRecordInner.AssemblyLastWriteTimeIL = fileInfo.LastWriteTime;
                    }
                }

                // IL - look in package caches
                if ((String.IsNullOrEmpty(assemblyRecordInner.AssemblyPathIL)) && (PackageAssemblyManagerEnvironment.ProbeForDependantAssemblyInPackageCachePath))
                {
                    String AssemblyPathIL = "";

                    ProbeAssemblyInPackageCaches(false, assemblyNameVersionKey, assemblyName, out AssemblyPathIL);
                    if (AssemblyPathIL.EndsWith(".dll") == true)
                    {
                        assemblyRecordInner.AssemblyPathIL = AssemblyPathIL;

                        FileInfo fileInfo = new FileInfo(assemblyRecordInner.AssemblyPathIL);
                        assemblyRecordInner.AssemblyLastWriteTimeIL = fileInfo.LastWriteTime;
                    }
                }

                // NI - look next to parentIL
                if ((String.IsNullOrEmpty(assemblyRecordInner.AssemblyPathNI)) && (PackageAssemblyManagerEnvironment.ProbeForDependantAssemblyNextToParentILFile))
                {
                    String AssemblyPathNI = "";

                    ProbeAssembly(true, Path.GetDirectoryName(assemblyRecord.AssemblyPathIL), assemblyName, out AssemblyPathNI);
                    if (AssemblyPathNI.EndsWith(".ni.dll") == true)
                    {
                        assemblyRecordInner.AssemblyPathNI = AssemblyPathNI;

                        FileInfo fileInfo = new FileInfo(assemblyRecordInner.AssemblyPathNI);
                        assemblyRecordInner.AssemblyLastWriteTimeNI = fileInfo.LastWriteTime;
                    }
                }

                // NI - look in package caches
                if ((String.IsNullOrEmpty(assemblyRecordInner.AssemblyPathNI)) && (PackageAssemblyManagerEnvironment.ProbeForDependantAssemblyInPackageCachePath))
                {
                    String AssemblyPathNI = "";

                    ProbeAssemblyInPackageCaches(true, assemblyNameVersionKey, assemblyName, out AssemblyPathNI);
                    if (AssemblyPathNI.EndsWith(".ni.dll") == true)
                    {
                        assemblyRecordInner.AssemblyPathNI = AssemblyPathNI;

                        FileInfo fileInfo = new FileInfo(assemblyRecordInner.AssemblyPathNI);
                        assemblyRecordInner.AssemblyLastWriteTimeNI = fileInfo.LastWriteTime;
                    }
                }

                //check timestamps


                //check self
                if (assemblyRecordInner.AssemblyLastWriteTimeIL > assemblyRecordInner.AssemblyLastWriteTimeNI)
                {
                    assemblyRecordInner.AssemblyInvalidNISelf = true;
                }

                //check parent
                if (assemblyRecordInner.AssemblyLastWriteTimeIL > assemblyRecord.AssemblyLastWriteTimeIL)
                {
                    assemblyRecord.AssemblyInvalidNIDependencies = true;
                }

                if (assemblyRecordInner.AssemblyLastWriteTimeNI > assemblyRecord.AssemblyLastWriteTimeNI)
                {
                    assemblyRecord.AssemblyInvalidNIDependencies = true;
                }

                //Console.WriteLine();
            }

            assemblyImport.CloseEnum(corEnum);

            return newDependenciesFound;
        }

        public int ParseAssemblies_PhaseX_SourceAssemblyPaths(
                            List<string> InputAssemblyFilePaths,
                            Dictionary<String, PackageAssemblyRecord> DependencyAssemblyDictionary
                            )
        {
            int newDependenciesFound = 0;

            Console.WriteLine();
            Console.WriteLine("ParseAssemblies_PhaseX_SourceAssemblyPaths");

            foreach (var inputAssemblyItem in InputAssemblyFilePaths)
            {
                string assemblyPath = inputAssemblyItem;
                PackageAssemblyRecord assemblyRecord = null;
                IMetaDataDispenser metaDataDispenser = null;
                IMetaDataAssemblyImport assemblyImport = null;

                // Check for partial or relative i.e. "foo.dll", ".\foo.dll" , "..\..\foo.dll"
                // Future: use GetFinalPath ?

                if (String.IsNullOrEmpty(Path.GetDirectoryName(assemblyPath)))
                {
                    Console.WriteLine("Partial Assembly Paths are not supported {0}", assemblyPath);
                    continue;
                }

                if (File.Exists(assemblyPath) == false)
                {
                    Console.WriteLine("X-NOT FOUND {0}", assemblyPath);
                    continue;
                }

                metaDataDispenser = NativeMethods.MetaDataGetDispenser(ref MetaDataDispenserGUID,
                                            ref IMetaDataDispenserGUID);

                assemblyImport = metaDataDispenser.OpenScope(assemblyPath,
                                           CorOpenFlags.ReadOnly,
                                           ref IMetaDataAssemblyImportGUID);

                {
                    int chName;
                    ASSEMBLYMETADATA assemblyMetaData;

                    StringBuilder assemblyNameBuilder = new StringBuilder(MAX_PATH);
                    String assemblyName;

                    assemblyImport.GetAssemblyProps(assemblyImport.GetAssemblyFromScope(),
                                                    IntPtr.Zero,
                                                    IntPtr.Zero,
                                                    IntPtr.Zero,
                                                    assemblyNameBuilder,
                                                    assemblyNameBuilder.Capacity,
                                                    out chName,
                                                    out assemblyMetaData, //IntPtr.Zero, 
                                                    IntPtr.Zero);
                    assemblyName = assemblyNameBuilder.ToString();

                    string assemblyVersion = PackageAssemblyRecord.MakeAssemblyVersion(assemblyMetaData);

                    Console.WriteLine("");
                    Console.WriteLine("Assembly: {0} Version {1} AssemblyPath: {2}", assemblyName, assemblyVersion, assemblyPath);

                    string assemblyNameVersionKey = PackageAssemblyRecord.MakeAssemblyNameVersion(assemblyName, assemblyVersion);

                    if (DependencyAssemblyDictionary.ContainsKey(assemblyNameVersionKey) == true)
                    {
                        assemblyRecord = DependencyAssemblyDictionary[assemblyNameVersionKey];
                        Console.WriteLine("{0} Already Exists in Collection !!!!", assemblyNameVersionKey);
                    }
                    else
                    {
                        //add self            
                        assemblyRecord = new PackageAssemblyRecord();
                        assemblyRecord.AssemblyName = assemblyName;
                        assemblyRecord.AssemblyVersion = assemblyVersion;

                        assemblyRecord.AssemblyPathIL = assemblyPath;

                        FileInfo fileInfo = new FileInfo(assemblyRecord.AssemblyPathIL);
                        assemblyRecord.AssemblyLastWriteTimeIL = fileInfo.LastWriteTime;

                        assemblyRecord.AssemblyPathNI = "";

                        DependencyAssemblyDictionary.Add(assemblyNameVersionKey, assemblyRecord);
                        newDependenciesFound++;
                    }

                    //probe for paths

                    // NI - look next to parentIL
                    if ((String.IsNullOrEmpty(assemblyRecord.AssemblyPathNI)) && (PackageAssemblyManagerEnvironment.ProbeForDependantAssemblyNextToParentILFile))
                    {
                        String AssemblyPathNI = "";

                        ProbeAssembly(true, Path.GetDirectoryName(assemblyPath), assemblyName, out AssemblyPathNI);
                        if (AssemblyPathNI.EndsWith(".ni.dll") == true)
                        {
                            assemblyRecord.AssemblyPathNI = AssemblyPathNI;

                            FileInfo fileInfo = new FileInfo(assemblyRecord.AssemblyPathNI);
                            assemblyRecord.AssemblyLastWriteTimeNI = fileInfo.LastWriteTime;
                        }
                    }

                    // NI - look in package caches
                    if ((String.IsNullOrEmpty(assemblyRecord.AssemblyPathNI)) && (PackageAssemblyManagerEnvironment.ProbeForDependantAssemblyInPackageCachePath))
                    {
                        String AssemblyPathNI = "";

                        ProbeAssemblyInPackageCaches(true, assemblyNameVersionKey, assemblyName, out AssemblyPathNI);
                        if (AssemblyPathNI.EndsWith(".ni.dll") == true)
                        {
                            assemblyRecord.AssemblyPathNI = AssemblyPathNI;

                            FileInfo fileInfo = new FileInfo(assemblyRecord.AssemblyPathNI);
                            assemblyRecord.AssemblyLastWriteTimeNI = fileInfo.LastWriteTime;
                        }
                    }

                    Console.WriteLine("ParseAssemblies_PhaseX_SourceAssemblyPaths IL {0} {1}",
                        assemblyRecord.AssemblyPathIL,
                        assemblyRecord.AssemblyLastWriteTimeIL);

                    Console.WriteLine("ParseAssemblies_PhaseX_SourceAssemblyPaths NI {0} {1}",
                        assemblyRecord.AssemblyPathNI,
                        assemblyRecord.AssemblyLastWriteTimeNI);


                    if (assemblyRecord.AssemblyLastWriteTimeIL > assemblyRecord.AssemblyLastWriteTimeNI)
                    {
                        assemblyRecord.AssemblyInvalidNISelf = true;
                    }
                }

                //Dependencies
                newDependenciesFound = ParseAssemblies_PackageAssemblyRecord_FindDependencies(
                                            metaDataDispenser,
                                            assemblyImport,
                                            assemblyRecord,
                                            DependencyAssemblyDictionary
                                            );
            }

            return newDependenciesFound;
        }

        public int ParseAssemblies_PhaseY(
                            Dictionary<String, PackageAssemblyRecord> processAssemblyDictionary,
                            Dictionary<String, PackageAssemblyRecord> CommonAssemblyDictionary
                            )
        {
            int newDependenciesFound = 0;
            int errorsInPackageAssemblyRecordsMissingDependenciesSubItems = 0;
            //int errorsInPackageAssemblyRecordsMissingDependenciesRecords = 0;

            Console.WriteLine();
            Console.WriteLine("ParseAssemblies_PhaseY");

            foreach (var assemblyItem in processAssemblyDictionary)
            {
                PackageAssemblyRecord assemblyRecord = assemblyItem.Value;
                string assemblyPath = assemblyRecord.AssemblyPathIL;

                if (File.Exists(assemblyPath) == false)
                {
                    Console.WriteLine("Y-NOT FOUND {0} {1}", assemblyRecord.AssemblyName, assemblyPath);
                    continue;
                }

                IMetaDataDispenser metaDataDispenser = NativeMethods.MetaDataGetDispenser(ref MetaDataDispenserGUID, ref IMetaDataDispenserGUID);

                IMetaDataAssemblyImport assemblyImport = metaDataDispenser.OpenScope(assemblyPath,
                                                                       CorOpenFlags.ReadOnly,
                                                                       ref IMetaDataAssemblyImportGUID);

                //Dependencies
                newDependenciesFound = ParseAssemblies_PackageAssemblyRecord_FindDependencies(
                                            metaDataDispenser,
                                            assemblyImport,
                                            assemblyRecord,
                                            CommonAssemblyDictionary
                                            );

                if (String.Compare(assemblyRecord.AssemblyName, "mscorlib", StringComparison.OrdinalIgnoreCase) != 0)
                {
                    if (assemblyRecord.AssemblyDependencies.Count == 0)
                    {
                        errorsInPackageAssemblyRecordsMissingDependenciesSubItems++;
                    }
                }
            }

            return newDependenciesFound;
        }

        /*
                    @@ /App_Paths
                    @@
                    Directory.Exists == false
                    Directory.CreateDirectory "%CD%\PROCESS_PLATFORM" 
        */

        public int GenerateNIForAssembly(PackageAssemblyRecord assemblyRecord, Dictionary<String, PackageAssemblyRecord> CommonAssemblyDictionary)
        {
            int newFile = 0;
            //String coreCLRPath = "";
            String coreGenPath = "";
            String coreGenTemplate = " /fulltrust /in {0} /out {1} ";
            String coreGenTemplateTrustedPlatformAssemblies = " /Trusted_Platform_Assemblies {0} ";
            String coreGenCmdLine;

            //@@-p0 read from environment
            //coreGenPath = @"D:\dev\kite\external\SDK\CoreCLR10\binary_release\tools\i386\fre\coregen.exe";
            //coreCLRPath = @"D:\dev\kite\samples\packages_kite_system\CoreCLR.1.0.0.0\lib\kite\coreclr.dll";

            coreGenPath = @"d:\dev\kite\external\sdk\CoreCLR_K\tools\i386\fre\crossgen.exe";

            StringBuilder trustedPlatformAssemblies = new StringBuilder();

            if (assemblyRecord.IsMscorlib() == true)
            {
                trustedPlatformAssemblies.Append(assemblyRecord.AssemblyPathIL);
            }
            else
            {
                //@@ rework to use package relative paths

                trustedPlatformAssemblies.Append(@"d:\dev\kite\package_caches\packages_k\CoreCLR.1.0.0.0\lib\kite\mscorlib.ni.dll");
                trustedPlatformAssemblies.Append(@";");
                trustedPlatformAssemblies.Append(@"d:\dev\kite\package_caches\packages_k\CoreCLR.1.0.0.0\lib\kite\System.ni.dll");
                trustedPlatformAssemblies.Append(@";");
                trustedPlatformAssemblies.Append(@"d:\dev\kite\package_caches\packages_k\CoreCLR.1.0.0.0\lib\kite\System.Core.ni.dll");
                trustedPlatformAssemblies.Append(@";");
                trustedPlatformAssemblies.Append(@"d:\dev\kite\package_caches\packages_k\CoreCLR.1.0.0.0\lib\kite\System.Threading.Tasks.ni.dll");
                trustedPlatformAssemblies.Append(@";");
                trustedPlatformAssemblies.Append(@"d:\dev\kite\package_caches\packages_k\coreclr.1.0.0.0\lib\kite\System.Runtime.ni.dll");
                trustedPlatformAssemblies.Append(@";");
                trustedPlatformAssemblies.Append(@"d:\dev\kite\package_caches\packages_k\coreclr.1.0.0.0\lib\kite\System.Text.Encoding.ni.dll");

                if (assemblyRecord.AssemblyDependencies.Count == 0)
                {
                    //@@ warning
                }

                foreach (var dependencyAssemblyNameVersionKey in assemblyRecord.AssemblyDependencies)
                {
                    if (CommonAssemblyDictionary.ContainsKey(dependencyAssemblyNameVersionKey))
                    {
                        var depPackageAssemblyRecord = CommonAssemblyDictionary[dependencyAssemblyNameVersionKey];

                        if (String.IsNullOrEmpty(depPackageAssemblyRecord.AssemblyPathNI))
                        {
                            //@@ Error
                            return 0;
                        }
                        else
                        {
                            if (trustedPlatformAssemblies.Length > 0)
                                trustedPlatformAssemblies.Append(";");

                            trustedPlatformAssemblies.Append(depPackageAssemblyRecord.AssemblyPathNI);
                        }

                    }
                    else
                    {
                        //@@ warning
                    }
                }
            }

            string tempAssemblyPathNI = "";
            string tempAssemblyFileNI = assemblyRecord.AssemblyName + ".ni.dll";

            //@@ add flag and support for X86\
            tempAssemblyPathNI = Path.Combine(
                                    Path.GetDirectoryName(assemblyRecord.AssemblyPathIL),
                                    tempAssemblyFileNI);

            coreGenCmdLine = String.Format(coreGenTemplate,
                                                assemblyRecord.AssemblyPathIL,
                                                tempAssemblyPathNI
                                        );

            if (trustedPlatformAssemblies.Length > 0)
            {
                coreGenCmdLine += String.Format(coreGenTemplateTrustedPlatformAssemblies,
                                        trustedPlatformAssemblies.ToString()
                                        );
            }

            Console.WriteLine("{0} {1}",
                        coreGenPath,
                        coreGenCmdLine
                        );

            {
                const uint NORMAL_PRIORITY_CLASS = 0x0020;

                bool retValue;
                string Application = coreGenPath;
                string CommandLine = coreGenCmdLine;
                PROCESS_INFORMATION pInfo = new PROCESS_INFORMATION();
                STARTUPINFO sInfo = new STARTUPINFO();
                SECURITY_ATTRIBUTES pSec = new SECURITY_ATTRIBUTES();
                SECURITY_ATTRIBUTES tSec = new SECURITY_ATTRIBUTES();
                pSec.nLength = Marshal.SizeOf(pSec);
                tSec.nLength = Marshal.SizeOf(tSec);

                retValue = NativeMethods.CreateProcess(
                                    Application,
                                    CommandLine,
                                    ref pSec, ref tSec, false, NORMAL_PRIORITY_CLASS,
                                    IntPtr.Zero, null, ref sInfo, out pInfo
                               );

                Console.WriteLine("Process ID (PID): " + pInfo.dwProcessId);
                Console.WriteLine("Process Handle : " + pInfo.hProcess);

                if (retValue)
                {
                    //@@TODO get from defaults section
                    int dwTimeout = 30000;
                    ProcessWaitHandle processWaitHandle = new ProcessWaitHandle(pInfo.hProcess);

                    processWaitHandle.WaitOne(dwTimeout);
                }
            }

            if (File.Exists(tempAssemblyPathNI))
            {
                newFile = 1;
                assemblyRecord.AssemblyPathNI = tempAssemblyPathNI;

                FileInfo fileInfo = new FileInfo(assemblyRecord.AssemblyPathNI);
                assemblyRecord.AssemblyLastWriteTimeNI = fileInfo.LastWriteTime;
            }

            return newFile;
        }

        public int ParseAssemblies_PhaseZ_GenerateNI(
                            Dictionary<String, PackageAssemblyRecord> processAssemblyDictionary,
                            Dictionary<String, PackageAssemblyRecord> CommonAssemblyDictionary
                            )
        {
            int newFiles = 0;

            Console.WriteLine("");
            Console.WriteLine("ParseAssemblies_PhaseZ_GenerateNI");

            foreach (var assemblyItem in processAssemblyDictionary)
            {
                PackageAssemblyRecord assemblyRecord = assemblyItem.Value;

                if (String.IsNullOrEmpty(assemblyRecord.AssemblyPathIL) == true)
                {
                    /*                
                        fCollectWarnings
                        fCollectErrors
                        CollectedWarnings
                        CollectedErrors
                    */

                    Console.WriteLine("AssemblyPathIL is Empty");

                    continue;
                }

                if (String.IsNullOrEmpty(assemblyRecord.AssemblyPathNI) == true)
                {
                    /*                
                        fCollectWarnings
                        fCollectErrors
                        CollectedWarnings
                        CollectedErrors
                    */

                    Console.WriteLine("AssemblyPathNI is Empty");

                    //continue;
                }

                //if no flag then do additional tests

                //@@@if (assemblyRecord.AssemblyInvalidNI == false)
                {
                    //Already have an NI?
                    if (String.IsNullOrEmpty(assemblyRecord.AssemblyPathNI) == false)
                    {
                        //check last write timestamp of NI > IL ?
                        if (assemblyRecord.AssemblyLastWriteTimeNI >= assemblyRecord.AssemblyLastWriteTimeIL)
                        {
                            Console.WriteLine("NI TimeStamp is current: {0}", assemblyRecord.AssemblyPathNI);
                            continue;
                        }
                    }
                }

                //@@ need to check which root dependencies needs to be re-generated before self regenerates

                Console.WriteLine("GenerateNIForAssembly IL {0} {1}",
                    assemblyRecord.AssemblyPathIL,
                    assemblyRecord.AssemblyLastWriteTimeIL);

                Console.WriteLine("GenerateNIForAssembly NI {0} {1}",
                    assemblyRecord.AssemblyPathNI,
                    assemblyRecord.AssemblyLastWriteTimeNI);

                newFiles += GenerateNIForAssembly(assemblyRecord, CommonAssemblyDictionary);
            }

            return newFiles;
        }

        public void DisplayAssembliesAndDependencies(Dictionary<String, PackageAssemblyRecord> AssemblyDictionary)
        {
            Console.WriteLine("");
            Console.WriteLine("DisplayAssembliesAndDependencies");

            foreach (var item in AssemblyDictionary)
            {
                var assemblyRecord = item.Value;
                Console.WriteLine(" {0}  \n\tVersion {1} \n\tIL {2} \n\tNI {3}",
                                    item.Key,
                                    assemblyRecord.AssemblyVersion,
                                    assemblyRecord.AssemblyPathIL,
                                    assemblyRecord.AssemblyPathNI
                                    );

                Console.WriteLine("\tDependencies");
                foreach (var dependencyInnerAssemblyName in assemblyRecord.AssemblyDependencies)
                {
                    PackageAssemblyRecord assemblyRecordInner;
                    Console.WriteLine("\t\t{0}", dependencyInnerAssemblyName);
                    if (AssemblyDictionary.ContainsKey(dependencyInnerAssemblyName))
                    {
                        assemblyRecordInner = AssemblyDictionary[dependencyInnerAssemblyName];
                        Console.WriteLine("\t\t\tIL {0} \n\t\t\tNI {1}",
                                            assemblyRecordInner.AssemblyPathIL,
                                            assemblyRecordInner.AssemblyPathNI
                                            );
                    }
                }
                Console.WriteLine("");
            }
            Console.WriteLine("");
        }

        public void ProcessConstants()
        {
            unchecked
            {
                MetaDataDispenserGUID = new Guid((int)0xE5CB7A31, 0x7512, 0x11D2, 0x89, 0xCE, 0x00, 0x80, 0xC7, 0x92, 0xE5, 0xD8);
                IMetaDataDispenserGUID = new Guid((int)0x809C652E, 0x7396, 0x11D2, 0x97, 0x71, 0x00, 0xA0, 0xC9, 0xB4, 0xD5, 0x0C);
                IMetaDataAssemblyImportGUID = new Guid((int)0xEE62470B, (short)0xE94B, 0x424e, 0x9B, 0x7C, 0x2F, 0x00, 0xC9, 0x24, 0x9F, 0x93);
            }
        }

        public bool ProcessEnvironmentVariables()
        {
            bool fSuccess = true;
            int iGetLastError = 0;
            string processArchitectureEnvVarName = "PROCESSOR_ARCHITECTURE";
            StringBuilder processArchitectureEnvVar = new StringBuilder(MAX_PATH);
            uint processArchitectureEnvVarCapacity = 0;

            processArchitectureEnvVarCapacity = NativeMethods.GetEnvironmentVariable(processArchitectureEnvVarName,
                                            processArchitectureEnvVar,
                                            (uint)processArchitectureEnvVar.Capacity);
            iGetLastError = Marshal.GetLastWin32Error();
            if ((processArchitectureEnvVarCapacity == 0) || (processArchitectureEnvVarCapacity > processArchitectureEnvVar.Capacity))
            {
                return false;
            }

            ProcessArchitecture = processArchitectureEnvVar.ToString();
            Console.WriteLine("{0}={1}", processArchitectureEnvVarName, ProcessArchitecture);

            //@@ TODO hoist to constants section
            string kitePackagesPathSearchEnvVarName = "K.P.Path";
            StringBuilder kitePackagesPathSearchEnvVar = new StringBuilder(MAX_PATH * 12);
            uint kitePackagesPathSearchEnvVarCapacity = 0;
            char[] kitePackagesPathSearchEnvVarSeps = { ';' };

            kitePackagesPathSearchEnvVarCapacity = NativeMethods.GetEnvironmentVariable(kitePackagesPathSearchEnvVarName,
                                            kitePackagesPathSearchEnvVar,
                                            (uint)kitePackagesPathSearchEnvVar.Capacity);
            iGetLastError = Marshal.GetLastWin32Error();
            if ((kitePackagesPathSearchEnvVarCapacity == 0) || (kitePackagesPathSearchEnvVarCapacity > kitePackagesPathSearchEnvVar.Capacity))
            {
                return false;
            }

            PackagesPathSearch = kitePackagesPathSearchEnvVar.ToString().Split(kitePackagesPathSearchEnvVarSeps);

            foreach (string env in PackagesPathSearch)
            {
                Console.WriteLine("env: {0}", env);
            }

            return fSuccess;
        }

        public void ShowUsage()
        {
            Console.WriteLine("");
            Console.WriteLine(" /? Help");
            Console.WriteLine("");
            Console.WriteLine(" /pm <package manifest file>.");
            Console.WriteLine("");
            /*
            Console.WriteLine(" /p <list of package cache paths seperated by semi colons>");
            Console.WriteLine(" /p @<response file with list of package cache paths seperated by new lines>");
            */
            Console.WriteLine("");
            Console.WriteLine(" /i <list of fully qualified assembly file paths seperated by semi colons>");
            Console.WriteLine(" /i @<response file with list of assembly file paths seperated by new lines>");
            Console.WriteLine("");
            Console.WriteLine(" /gni Generate NI Images");
            Console.WriteLine("");
            Console.WriteLine(" /gnitp Target Processor i.e. x86, AMD64, ARM");
            Console.WriteLine("");
            Console.WriteLine(" /bi build package cache indexes");
            Console.WriteLine("");
            Console.WriteLine("");
        }

        public bool ParseCommandLineArguments(string[] args)
        {
            if (args.Length == 0)
            {
                ShowUsage();
                return false;
            }

            for (var argIndex = 0; argIndex < args.Length; argIndex++)
            {
                var arg = args[argIndex];

                Console.WriteLine(arg);

                switch (arg.ToLower())
                {
                    case "/?":
                    case "-?":
                    case "/help":
                    case "--help":
                    case "help":
                        {
                            ShowUsage();
                            return false;
                        }

                    case "-pm":
                    case "/pm":
                        {
                            String nextArg;

                            if (argIndex + 1 > args.Length)
                            {
                                ShowUsage();
                                return false;
                            }

                            argIndex++;
                            nextArg = args[argIndex];

                            if (!LoadPackageManifest(nextArg))
                            {
                                return false;
                            }
                            break;
                        }

                    //Build list of initial input Assemblies from cmdline arguments
                    case "-i":
                    case "/i":
                        {
                            String[] assemblyPaths;
                            String nextArg;

                            if (argIndex + 1 > args.Length)
                            {
                                ShowUsage();
                                return false;
                            }

                            argIndex++;
                            nextArg = args[argIndex];

                            StringBuilder firstCharFileName = new StringBuilder();
                            firstCharFileName.Append(nextArg[0]);

                            if (String.Compare(firstCharFileName.ToString(), "@") == 0)
                            {
                                //@@Move to constants section
                                char[] seps = new char[] { '\r', '\n', ';' };

                                string fileName = nextArg.Substring(1);

                                string fileContents = File.ReadAllText(fileName);
                                assemblyPaths = fileContents.Split(seps, StringSplitOptions.RemoveEmptyEntries);
                            }
                            else
                            {
                                assemblyPaths = nextArg.Split(';');
                            }

                            foreach (var assemblyPath in assemblyPaths)
                            {
                                if (String.IsNullOrEmpty(assemblyPath))
                                {
                                    continue;
                                }

                                if (String.IsNullOrEmpty(Path.GetDirectoryName(assemblyPath)))
                                {
                                    ShowUsage();
                                    return false;
                                }

                                InputAssemblyFilePaths.Add(assemblyPath);
                            }

                            break;
                        }

                    case "-gni":
                    case "/gni":
                        {
                            PackageAssemblyManagerEnvironment.CoreGenEnabled = true;
                            break;
                        }
                    case "-gni-":
                    case "/gni-":
                        {
                            PackageAssemblyManagerEnvironment.CoreGenEnabled = false;
                            break;
                        }

                }
            }

            return true;
        }

        public void StartViaCommandLine(string[] args)
        {
            Console.WriteLine("");

            ProcessConstants();

            if (!ProcessEnvironmentVariables())
            {
                return;
            }

            if (!ParseCommandLineArguments(args))
            {
                return;
            }

            LoadPackageManifests();

            //GoalSeek
            ParseAssemblies_PhaseX_SourceAssemblyPaths(
                                InputAssemblyFilePaths,
                                CommonAssemblyDictionary);

            {
                int maxTries = 12;
                int countTries = 0;
                int newDependencies = -1;
                while ((newDependencies != 0) && (countTries < maxTries))
                {
                    //Clone
                    var processAssemblyDictionary = AssemblyDictionary_Clone(CommonAssemblyDictionary);

                    newDependencies = ParseAssemblies_PhaseY(processAssemblyDictionary, CommonAssemblyDictionary);
                    countTries++;
                    Console.WriteLine("Retry {0} ParseAssemblies_PhaseY newDependencies {1}", countTries, newDependencies);
                }
            }

            if (PackageAssemblyManagerEnvironment.CoreGenEnabled)
            {
                int maxTries = 12;
                int countTries = 0;
                int newFiles = -1;

                //GoalSeek
                while ((newFiles != 0) && (countTries < maxTries))
                {
                    //Clone
                    var processAssemblyDictionary = AssemblyDictionary_Clone(CommonAssemblyDictionary);

                    newFiles = ParseAssemblies_PhaseZ_GenerateNI(processAssemblyDictionary, CommonAssemblyDictionary);
                    countTries++;
                    Console.WriteLine("Retry {0} ParseAssemblies_PhaseZ_GenerateNI newDependencies {1}", countTries, newFiles);
                }
            }

            if (PackageAssemblyManagerEnvironment.DisplayAssembliesAndDependencies)
            {
                DisplayAssembliesAndDependencies(CommonAssemblyDictionary);
            }

        }
    }

}