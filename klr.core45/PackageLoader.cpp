#include "stdafx.h"
#include "PackageLoader.h"

//##FUTURE - make this dynamic i.e. load from manifest file
const wchar_t* g_rgClrDefaultAssembliesList[][MAX_STR] = 
{
    L"Microsoft.CSharp",
    L"mscorlib.Extensions",
    L"System.Collections",
    L"System.ComponentModel",
    L"System.ComponentModel.EventBasedAsync",
    L"System.Console",
    L"System.Core",
    L"System.Diagnostics.Contracts",
    L"System.Diagnostics.Debug",
    L"System.Diagnostics.Tools",
    L"System",
    L"System.Dynamic.Runtime",
    L"System.Globalization",
    L"System.IO",
    L"System.IO.FileSystem",
    L"System.IO.FileSystem.Primitives",
    L"System.Linq",
    L"System.Linq.Expressions",
    L"System.Linq.Queryable",
    L"System.ObjectModel",
    L"System.Reflection",
    L"System.Reflection.Emit.ILGeneration",
    L"System.Reflection.Emit.Lightweight",
    L"System.Reflection.Extensions",
    L"System.Reflection.Primitives",
    L"System.Resources.ResourceManager",
    L"System.Runtime",
    L"System.Runtime.Extensions",
    L"System.Runtime.InteropServices",
    L"System.Security.Principal",
    L"System.Text.Encoding",
    L"System.Text.Encoding.Extensions",
    L"System.Text.RegularExpressions",
    L"System.Threading",
    L"System.Threading.Tasks",
    L"System.Threading.Thread",
    L"System.Threading.ThreadPool",
    L"System.Threading.Timer",
    L"System.Xml",
    L"System.Xml.Linq",
    L"System.Xml.ReaderWriter",
    L"System.Xml.Serialization",
    L"System.Xml.XDocument",
    L"System.Xml.XmlSerializer",
    L"System.Net.NetworkInformation",
    L"System.Net.Primitives",
    L"System.Net.Requests",
    L"System.Observable",
    L"System.Reflection.Emit",
    L"System.Runtime.Serialization",
    L"System.Runtime.Serialization.Json",
    L"System.Runtime.Serialization.Primitives",
    L"System.Runtime.Serialization.Xml",

    //    L"System.Runtime.InteropServices.WindowsRuntime",
    //    L"System.Runtime.WindowsRuntime",
    //    L"System.ServiceModel.Http",
    //    L"System.ServiceModel.Primitives",
    //    L"System.ServiceModel.Security",
    //    L"System.ServiceModel.Web",
    L"\0"
};


PackageLoader::PackageLoader(void)
{
    niExtension = L".ni.dll";
    ilExtension = L".dll";

    m_rgClrDefaultAssembliesList = nullptr;
    m_rgClrDefaultAssemblies = nullptr;
    m_pwszManifestAssemblyList = nullptr;

}

PackageLoader::~PackageLoader(void)
{
    Dispose();
}

bool PackageLoader::Init(Firmware* pFirmware)
{
    bool fSuccess = true;

    if (pFirmware == nullptr)
    {
        fSuccess = false;
        goto Finished;
    }

    m_pFirmware = pFirmware;

    m_rgClrDefaultAssembliesList = (wchar_t**)&g_rgClrDefaultAssembliesList;

    //##Future
    //fSuccess = NuGetCmdLine_FindEXE();
    //if (!fSuccess) 
    //{
    //    goto Finished;
    //}

Finished:
    return fSuccess;
}

bool PackageLoader::Dispose()
{
    bool fSuccess = true;

    if (m_pwszManifestAssemblyList) 
    {
        delete[] m_pwszManifestAssemblyList;
        m_pwszManifestAssemblyList = NULL;
    }

    if (m_rgClrDefaultAssemblies)
    {
        delete m_rgClrDefaultAssemblies;
        m_rgClrDefaultAssemblies = nullptr;
    }

    return fSuccess;
}

// The default assemblies used in CoreCLR. 
// These are the CoreCLR implementation and facade assemblies.
// The names must not include the .dll extension.

const wchar_t* PackageLoader::GetClrEngineAssemblyList()
{
    std::wstring tpaListAccumulator;

    if (m_rgClrDefaultAssemblies != nullptr)
        goto Finished;

    // Add the assemblies from the trustedAssemblies list
    //for (const wchar_t*  &assembly : m_rgClrDefaultAssembliesList) 
    //for(wchar_t* pAssembly = *m_rgClrDefaultAssembliesList[0], pAssembly != nullptr,)
    for(int index=0;m_rgClrDefaultAssembliesList[index] != nullptr;index++)
    {
        wchar_t* pAssembly = m_rgClrDefaultAssembliesList[index];
        std::wstring assemblyPath;

        //= m_pFirmware->g_wszCLRDirectoryPath
        wchar_t* pwszDirectoryToProbe = m_pFirmware->GetClrHostRuntimeModule()->GetCLRDirectoryPath();

        m_pFirmware->GetPackageLoader()->ProbeAssembly(pwszDirectoryToProbe, 
            pAssembly, 
            assemblyPath);

        if (assemblyPath.length() > 0) {
            tpaListAccumulator += L";";
            tpaListAccumulator += assemblyPath;
        }
    }

    //done
    auto tpaListLength = tpaListAccumulator.length();

    m_rgClrDefaultAssemblies = new wchar_t[tpaListLength + 1];

    tpaListAccumulator._Copy_s(
        m_rgClrDefaultAssemblies, 
        tpaListLength,    // _Dest_size
        tpaListLength);   // _Count

    m_rgClrDefaultAssemblies[tpaListLength] = '\0';

Finished:
    return m_rgClrDefaultAssemblies;
}

const wchar_t* PackageLoader::GetPackagesAssemblyList()
{
    if (m_pwszManifestAssemblyList)
    {
        goto Finished;
    }

Finished:    
    return m_pwszManifestAssemblyList;
}

bool PackageLoader::UseLoadLibraryToGetFinalFilePath(const wchar_t* pwszFilename, wchar_t* pwszFilePath, size_t cchFilePath)
{
    bool fSuccess = true;
    wchar_t wszFilePath[MAX_PATH];
    wchar_t* wszFilePathManagedAssemblyToStart = wszFilePath;
    size_t cchFilename = 0;
    size_t cchFilenameExtensionCheck = 0;
    wszFilePath[0] = '\0';
    HMODULE hModule = 0;

    std::wstring wstrFilename;
    std::wstring ws_ApplicationDirectoryPath;

    if (pwszFilename == nullptr)
    {
        fSuccess = false;
        goto Finished;
    }

    //Prepare out param
    pwszFilePath[0] = L'\0';

    cchFilename = ::wcslen(pwszFilename);
    //if (cchFilename < MAX_PATH + 4) 
    if (cchFilename > MAX_PATH - 4) 
    {
        fSuccess = false;
        goto Finished;
    }
    //backup for common extension length .dll or .exe
    cchFilenameExtensionCheck = cchFilename - 4;

    wstrFilename = pwszFilename;
    TRACE_ODS1(L"Attempting to load: %s", wstrFilename.c_str());

    //ccrun: Have the OS loader discover the location of the managed exe
    //Note: hmmm, this will trigger a file open ... also the paths may not be correct when using symlinks (Antares)
    hModule = ::LoadLibraryExW(wstrFilename.c_str(), NULL, 0);
    if (!hModule) 
    {
        TRACE_ODS1(L"Failed to load: %s", wstrFilename.c_str());
    } 

    // If the loader didn't discover the module, it might require an extension either .exe or .dll
    // Only do this if there was not already an extension.
    if (!hModule && 
        wstrFilename[cchFilenameExtensionCheck] != L'.' &&
        (wstrFilename[cchFilenameExtensionCheck + 1] != L'e' && wstrFilename[cchFilenameExtensionCheck + 1] != L'E') &&
        (wstrFilename[cchFilenameExtensionCheck + 2] != L'x' && wstrFilename[cchFilenameExtensionCheck + 2] != L'X') &&
        (wstrFilename[cchFilenameExtensionCheck + 3] != L'e' && wstrFilename[cchFilenameExtensionCheck + 3] != L'E') ) 
    {
        //reset
        wstrFilename = pwszFilename;
        wstrFilename += L".exe";
        TRACE_ODS1(L"Attempting to load: %s", wstrFilename.c_str());
        hModule = ::LoadLibraryExW(wstrFilename.c_str(), NULL, 0);
    }

    if (!hModule && 
        wstrFilename[cchFilenameExtensionCheck] != L'.' &&
        (wstrFilename[cchFilenameExtensionCheck + 1] != L'd' && wstrFilename[cchFilenameExtensionCheck + 1] != L'D') &&
        (wstrFilename[cchFilenameExtensionCheck + 2] != L'l' && wstrFilename[cchFilenameExtensionCheck + 2] != L'L') &&
        (wstrFilename[cchFilenameExtensionCheck + 3] != L'l' && wstrFilename[cchFilenameExtensionCheck + 3] != L'L') ) 
    {
        //reset
        wstrFilename = pwszFilename;
        wstrFilename += L".dll";
        TRACE_ODS1(L"Attempting to load: %s", wstrFilename.c_str());
        hModule = ::LoadLibraryExW(wstrFilename.c_str(), NULL, 0);
    }

    if (!hModule) 
    {
        TRACE_ODS1(L"Failed to load: %s",  wstrFilename.c_str());
        fSuccess = false;
        goto Finished;
    }

    // If the module was successfully loaded, get the path to where it was found.
    ::GetModuleFileNameW(hModule, wszFilePath, _countof(wszFilePath));
    ws_ApplicationDirectoryPath = wszFilePath;
    TRACE_ODS1(L"Loaded: %s", wszFilePath);

    // Find the "assembly name" part
    //##Future turn into a common function
    for (auto i = (int)::wcslen(wszFilePath); i >= 0; i--) 
    {
        if (wszFilePath[i] == L'\\') 
        {
            wszFilePathManagedAssemblyToStart = wszFilePath + i + 1;
            wszFilePath[i] = L'\0';
            break;
        }
    }
    //@@TODO verify assemblyname was actually found

    ws_ApplicationDirectoryPath[(wszFilePathManagedAssemblyToStart - wszFilePath)] = L'\0';
    TRACE_ODS1(L"ApplicationDirectoryPath: %s", ws_ApplicationDirectoryPath.c_str());
    wcscpy_s(pwszFilePath, cchFilePath,  ws_ApplicationDirectoryPath.c_str());

Finished:

    // Unload the managed user application module
    if (hModule) 
    {
        ::FreeLibrary(hModule);
        hModule = 0;
    }

    return fSuccess;
}

bool PackageLoader::FileExists(const wchar_t* pwszFilepath)
{
    bool fSuccess = true;
    DWORD dwFileAttributes = 0;

    // Changed from file open to get file attribues or CreateFile / get attributes
    // as file open will trigger anti-virus scanner which will scan the whole file 
    // contents over the network
    dwFileAttributes = ::GetFileAttributesW(pwszFilepath);
    if (dwFileAttributes != INVALID_FILE_ATTRIBUTES)
        fSuccess = true;
    else
        fSuccess = false;

    return fSuccess;
}

bool PackageLoader::DoesAssemblyExist(
    const wchar_t*  directoryPath, 
    const wchar_t*  assemblyName, 
    const wchar_t*  extension) 
{
    std::wstring assemblyPath;
    assemblyPath += directoryPath;
    assemblyPath += assemblyName;
    assemblyPath += extension;

    return FileExists(assemblyPath.c_str());
}

// Returns the path to the given assembly or nullptr if not found.
// Tries to find a .ni.dll assembly first, otherwise a .dll
void PackageLoader::ProbeAssembly(
    const wchar_t*  directoryPath, 
    const wchar_t*  assemblyName,
    std::wstring &result
    ) 
{
    /*
    //probe for process platform i.e. x86/AMD64 i.e. x86\Foo.ni.dll
    std::wstring directoryPathNI(directoryPath);
    directoryPathNI += m_pFirmware->GetHostBitness(); // L"X86";
    directoryPathNI += L"\\";

    if (DoesAssemblyExist(directoryPathNI.c_str(), assemblyName, niExtension)) 
    {
    result += directoryPathNI.c_str();
    result += assemblyName;
    result += niExtension;
    //TRACE_ODS1(L"ProbeAssembly %s", result.c_str());
    } 
    else if (DoesAssemblyExist(directoryPath, assemblyName, niExtension)) 
    {
    result += directoryPath;
    result += assemblyName;
    result += niExtension;
    //TRACE_ODS1(L"ProbeAssembly %s", result.c_str());
    }
    else 
    */

    if (DoesAssemblyExist(directoryPath, assemblyName, ilExtension)) 
    {
        result += directoryPath;
        result += assemblyName;
        result += ilExtension;
        //TRACE_ODS1(L"ProbeAssembly %s", result.c_str());
    }
}

bool PackageLoader::ProbeForFileInPackages(
    wchar_t* pwszPackagename, 
    wchar_t* pwszFilename,
    wchar_t* pwszFilepathFound,
    size_t   cchFilepathfound
    )
{
    bool fSuccess = true;

    // Failed to load. Try to load from the well-known location.
    wchar_t wszFilePathExpanded[MAX_PATH];

    if (pwszPackagename == nullptr)
    {
        goto Finished;
    }

    //loop through package cache directories
    for(int packageCache=0;packageCache<g_piKitePackagesPathsCount;packageCache++)
    {
        if (g_prgwszKitePackagesPaths[packageCache] == NULL)
        {
            continue;
        }

        std::wstring packagePath(g_prgwszKitePackagesPaths[packageCache]);

        packagePath += pwszPackagename;
        packagePath += L"\\";

        packagePath += g_pwszKitePackagesCoreCLRPackageLibraryDirectorySegmentDefault;
        packagePath += L"\\";
        packagePath += pwszFilename;

        ::ExpandEnvironmentStringsW(packagePath.c_str(), wszFilePathExpanded, _countof(wszFilePathExpanded));
        fSuccess = FileExists(wszFilePathExpanded);
        if (fSuccess == true) 
        {
            wcscpy_s(pwszFilepathFound,
                cchFilepathfound,
                wszFilePathExpanded
                );
            break;
        }
    } 

Finished:
    return fSuccess;
}

wchar_t* PackageLoader::GetPackageAssemblies()
{
    return nullptr;
}

wchar_t* PackageLoader::ParsePackageManifest(const wchar_t* pwszManifestFilePath) 
{
    wchar_t* pwszManifestAssemblyList = nullptr;
    std::wstring tpaListAccumulator;


    /*
    // Add the host assembly currently running in
    std::wstring hostAssemblyPath;

    ProbeAssembly(m_hostDirectoryPath, managedHostDllName, hostAssemblyPath);
    if (hostAssemblyPath.length() > 0) {
    tpaListAccumulator += hostAssemblyPath;
    }

    // Add the assemblies from the trustedAssemblies list
    for (const wchar_t*  &assembly : trustedAssemblies) 
    {
    std::wstring assemblyPath;
    ProbeAssembly(m_wszCLRDirectoryPath, assembly, assemblyPath);

    if (assemblyPath.length() > 0) {
    tpaListAccumulator += L";";
    tpaListAccumulator += assemblyPath;
    }
    }
    */

    //Load package manifest from per web site or application directory
    //{
    FILE *pFile = ::_wfopen(pwszManifestFilePath, L"r");
    if (pFile == nullptr) 
    {
        TRACE_ODS1(L"Packages file config not found: %s", pwszManifestFilePath);
        goto Finished;
    }

    if (pFile != nullptr) 
    {
        TRACE_ODS1(L"Packages file config found: %s", pwszManifestFilePath);

        while (!::feof(pFile)) 
        {
            int mystringLength = MAX_STR;
            char szManifestLine[MAX_STR];
            std::wstring s_PackageLibAssemblyPath;

            if (::fgets (szManifestLine , mystringLength , pFile) != NULL) 
            {
                int cchwszWideConverted = 0;

                int cchManifestLine = PACKAGE_MANIFEST_LINE_LENGTH_MAX;
                wchar_t wszManifestLine [PACKAGE_MANIFEST_LINE_LENGTH_MAX+1];

                //Win32 Error Code
                cchwszWideConverted = MultiByteToWideChar(
                    CP_ACP,               //__in       UINT CodePage,
                    0,                    //__in       DWORD dwFlags,
                    szManifestLine,       //__in       LPCSTR lpMultiByteStr,
                    -1,                   //__in       int cbMultiByte,
                    wszManifestLine,      //__out_opt  LPWSTR lpWideCharStr,
                    (int)cchManifestLine  //__in       int cchWideChar
                    );

                if (cchwszWideConverted > 0)
                {
                    if (wszManifestLine[0] == L'#' || wszManifestLine[0] == L' ' || wszManifestLine[0] == L'\n' || wszManifestLine[0] == L'\r')
                    {
                        continue;
                    }

                    int len = wcslen(wszManifestLine);
                    if (wszManifestLine[len-1] == L'\n' || wszManifestLine[0] == L'\r') {
                        wszManifestLine[len-1]  = '\0';
                    }

                    if (wszManifestLine[len-2] == L'\n' || wszManifestLine[0] == L'\r') {
                        wszManifestLine[len-2]  = '\0';
                    }

                    int intResultColumnCount = 0;
                    wchar_t wszPackageName[MAX_PATH];
                    wchar_t wszPackageVersion[MAX_PATH];
                    wchar_t wszAssemblyName[MAX_PATH];
                    wchar_t wszAssemblyVersion[MAX_PATH];

                    //Read Line
                    //Bar 3.0.0.0 Bar.dll 3.0.0.0

                    //TRACE_ODS1(L"Packages file config scan: %s", wszManifestLine);

                    //Future: Is there a CRT API to parse a tab separate line ?
                    intResultColumnCount = swscanf_s(
                        wszManifestLine, 
                        L"%s %s %s %s",
                        wszPackageName, 
                        _countof(wszPackageName)-1,        // one less than buffer to keep space for NULL
                        wszPackageVersion, 
                        _countof(wszPackageVersion)-1,     // one less than buffer to keep space for NULL
                        wszAssemblyName, 
                        _countof(wszAssemblyName)-1,       // one less than buffer to keep space for NULL
                        wszAssemblyVersion, 
                        _countof(wszAssemblyVersion)-1        // one less than buffer to keep space for NULL
                        );

                    //Validate column count
                    if (intResultColumnCount != 4) 
                    {
                        TRACE_ODS(L"Packages file config scan FAILED");
                    }

                    if (intResultColumnCount == 4)
                    {
                        bool fPackageCodeModuleFound = false;

                        //@@ Check Override list of substitute packagename and version
                        //@@ should we add m_wszKitePackagesPathApplication to packageCache list ?
                        for(int packageCache=0;packageCache<g_piKitePackagesPathsCount && fPackageCodeModuleFound == false;packageCache++)
                        {
                            if (g_prgwszKitePackagesPaths[packageCache] == NULL)
                            {
                                //TRACE
                                continue;
                            }

                            for(int packageFramework=0;packageFramework < g_cKitePackagesFrameworks && fPackageCodeModuleFound == false; packageFramework++)
                            {
                                if (g_prgwszKitePackagesFrameworks[packageFramework] == NULL)
                                {
                                    //TRACE
                                    continue;
                                }

                                std::wstring packageAssemblyDirectoryPath;
                                std::wstring assemblyPath;
                                std::wstring assemblyName;

                                //@@TODO move to constants
                                wchar_t* packageLibrary = L"lib";
                                wchar_t* packageFrameworkName = (wchar_t*)g_prgwszKitePackagesFrameworks[packageFramework]; //L"kite";

                                packageAssemblyDirectoryPath += g_prgwszKitePackagesPaths[packageCache];

                                //@@ Hoist this up and out then loop
                                //@@ Check Override list of substitute packagename and version

                                std::wstring packageOverrideRuleSourceLookup;

                                packageOverrideRuleSourceLookup += wszPackageName;
                                packageOverrideRuleSourceLookup += L".";
                                packageOverrideRuleSourceLookup += wszPackageVersion;

                                wchar_t wszPackageTargetOverride[MAX_STR];

                                bool fTargetFound = Lookup_PackageOverrideRule(
                                    (LPWSTR)packageOverrideRuleSourceLookup.c_str(), 
                                    wszPackageTargetOverride, 
                                    _countof(wszPackageTargetOverride));
                                if (fTargetFound != NULL)
                                {
                                    packageAssemblyDirectoryPath += wszPackageTargetOverride;
                                    packageAssemblyDirectoryPath += L"\\";
                                }
                                else
                                {
                                    packageAssemblyDirectoryPath += wszPackageName;
                                    packageAssemblyDirectoryPath += L".";
                                    packageAssemblyDirectoryPath += wszPackageVersion;
                                    packageAssemblyDirectoryPath += L"\\";
                                }

                                packageAssemblyDirectoryPath += packageLibrary;
                                packageAssemblyDirectoryPath += L"\\";
                                packageAssemblyDirectoryPath += packageFrameworkName;
                                packageAssemblyDirectoryPath += L"\\";

                                assemblyName += wszAssemblyName;

                                //Generate path like: 
                                //D:\dev\kite\samples\packages\Bar.3.0.0.0\lib\kite

                                ProbeAssembly(packageAssemblyDirectoryPath.c_str(), assemblyName.c_str(), assemblyPath);
                                if (assemblyPath.length() > 0) 
                                {
                                    /*
                                    TRACE_ODS3(L"ProbeAssembly Dir=%s AsmName=%s AsmPath=%s", 
                                    packageAssemblyDirectoryPath.c_str(),
                                    assemblyName.c_str(),
                                    assemblyPath.c_str());
                                    */

                                    TRACE_ODS1(L"ProbeAssembly %s", 
                                        assemblyPath.c_str());


                                    tpaListAccumulator += L";";
                                    tpaListAccumulator += assemblyPath;
                                    fPackageCodeModuleFound = true;
                                    break;
                                }
                                else
                                {
                                    /*
                                    TRACE_ODS3(L"ProbeAssembly Dir=%s AsmName=%s AsmPath=%s", 
                                    packageAssemblyDirectoryPath.c_str(),
                                    assemblyName.c_str(),
                                    assemblyPath.c_str());
                                    */
                                }
                            }
                        }

                        /*
                        if (fPackageCodeModuleFound == false)
                        {
                        //@@ Note we could push this code to a NuGet Service on the machien (DmitryR's idea)
                        //Try Download

                        //@@11 
                        if (TryDownloadPackage(wszPackageName, wszPackageVersion, g_wszKitePackagesPathDownload) == true)
                        {
                        //Probe
                        }
                        }
                        */

                    }
                } 
                else 
                {
                    TRACE_ODS1(L"MultiByteToWideChar FAILED %d", (int)::GetLastError() );
                }
            }
        }

        if (pFile != NULL)
        {
            ::fclose(pFile);
            pFile = NULL;
        }
    }

    //done
    auto tpaListLength = tpaListAccumulator.length();
    pwszManifestAssemblyList = new wchar_t[tpaListLength + 1];

    tpaListAccumulator._Copy_s(
        pwszManifestAssemblyList, 
        tpaListLength,    // _Dest_size
        tpaListLength);   // _Count

    pwszManifestAssemblyList[tpaListLength] = '\0';

Finished:
    return pwszManifestAssemblyList;
}

wchar_t* PackageLoader::ParsePackageManifestFromDirectory(wchar_t* pwszDirectoryPath)
{
    std::wstring s_PackagesFileListConfig;

    s_PackagesFileListConfig += pwszDirectoryPath;
    s_PackagesFileListConfig += g_pwszPackageManifestFileName;

    return ParsePackageManifest(s_PackagesFileListConfig.c_str());
}

bool PackageLoader::Load_OverrideRules()
{
    bool fSuccess = false;

    //Load package override rules
    std::wstring s_PackagesOverrideRulesConfig;

    s_PackagesOverrideRulesConfig += g_wszKitePackageOverrideRulesPath;
    s_PackagesOverrideRulesConfig += L"\\";
    s_PackagesOverrideRulesConfig += g_pwszPackageOverrideRulesFileName;

    FILE *pFile = ::_wfopen(s_PackagesOverrideRulesConfig.c_str(), L"r");
    if (pFile == nullptr) 
    {
        TRACE_ODS1(L"Packages Override Rules config not found: %s", s_PackagesOverrideRulesConfig.c_str());
        //@@ return ??
    }

    if (pFile != nullptr) 
    {
        TRACE_ODS1(L"Packages Override Rulesconfig found: %s", s_PackagesOverrideRulesConfig.c_str());

        while (!::feof(pFile)) 
        {
            int mystringLength = MAX_STR;
            char szManifestLine[MAX_STR];
            std::wstring s_PackageLibAssemblyPath;

            if (::fgets (szManifestLine , mystringLength , pFile) != NULL) 
            {
                int cchwszWideConverted = 0;

                int cchManifestLine = PACKAGE_MANIFEST_LINE_LENGTH_MAX;
                wchar_t wszManifestLine [PACKAGE_MANIFEST_LINE_LENGTH_MAX+1];

                //Win32 Error Code
                cchwszWideConverted = MultiByteToWideChar(
                    CP_ACP,               //__in       UINT CodePage,
                    0,                    //__in       DWORD dwFlags,
                    szManifestLine,       //__in       LPCSTR lpMultiByteStr,
                    -1,                   //__in       int cbMultiByte,
                    wszManifestLine,      //__out_opt  LPWSTR lpWideCharStr,
                    (int)cchManifestLine  //__in       int cchWideChar
                    );

                if (cchwszWideConverted > 0)
                {
                    if (wszManifestLine[0] == L'#' || wszManifestLine[0] == L' ' || wszManifestLine[0] == L'\n' || wszManifestLine[0] == L'\r')
                    {
                        continue;
                    }

                    int len = wcslen(wszManifestLine);
                    if (wszManifestLine[len-1] == L'\n' || wszManifestLine[0] == L'\r') {
                        wszManifestLine[len-1]  = '\0';
                    }

                    if (wszManifestLine[len-2] == L'\n' || wszManifestLine[0] == L'\r') {
                        wszManifestLine[len-2]  = '\0';
                    }

                    int intResultColumnCount = 0;
                    wchar_t wszPackageNameSource[MAX_PATH];
                    wchar_t wszPackageVersionSource[MAX_PATH];
                    wchar_t wszPackageNameTarget[MAX_PATH];
                    wchar_t wszPackageVersionTarget[MAX_PATH];

                    //Read Line

                    //@@ TODO add range support
                    //Bar 1.1.0.0-1.2.0.0 Bar 1.3.0.0

                    //Support
                    //Bar 1.1.0.0 Bar 1.3.0.0

                    //TRACE_ODS1(L"Packages config scan: %s", wszManifestLine);

                    //Future: Is there a CRT API to parse a tab separate line ?
                    intResultColumnCount = swscanf_s(
                        wszManifestLine, 
                        L"%s %s %s %s",
                        wszPackageNameSource, 
                        _countof(wszPackageNameSource)-1,        // one less than buffer to keep space for NULL
                        wszPackageVersionSource, 
                        _countof(wszPackageVersionSource)-1,     // one less than buffer to keep space for NULL
                        wszPackageNameTarget, 
                        _countof(wszPackageNameTarget)-1,       // one less than buffer to keep space for NULL
                        wszPackageVersionTarget, 
                        _countof(wszPackageVersionTarget)-1        // one less than buffer to keep space for NULL
                        );

                    if (intResultColumnCount != 4) 
                    {
                        TRACE_ODS(L"Packages config parse FAILED");
                    }

                    if (intResultColumnCount == 4)
                    {
                        bool fPackageCodeModuleFound = false;

                        std::wstring packageSource;
                        std::wstring packageTarget;

                        packageSource += wszPackageNameSource;
                        packageSource += L".";
                        packageSource += wszPackageVersionSource;

                        packageTarget += wszPackageNameTarget;
                        packageTarget += L".";
                        packageTarget += wszPackageVersionTarget;

                        TRACE_ODS1(L"Packages config scan: %s", wszManifestLine);

                        m_mapPackageOverrideRules.insert(std::pair<wstring,wstring>(packageSource,packageTarget));
                    }
                } 
                else 
                {
                    TRACE_ODS1(L"MultiByteToWideChar FAILED %d", (int)::GetLastError() );
                }
            }
        }

        if (pFile != NULL)
        {
            ::fclose(pFile);
            pFile = NULL;
        }

        fSuccess = true;

        TRACE_ODS1(L"PackageOverrideRules count:%d", (int)m_mapPackageOverrideRules.size() );
    }

    return fSuccess;
}

//@@TODO
bool PackageLoader::Lookup_PackageOverrideRule(LPWSTR pwszPackageOverrideRuleSourceLookup, LPWSTR pwszPackageTargetOverride, size_t cchPackageTargetOverride)
{
    //Override rule lookup
    std::wstring packageSource(pwszPackageOverrideRuleSourceLookup);
    std::map<wstring,wstring>::iterator itTargetLookup;

    itTargetLookup = m_mapPackageOverrideRules.find(packageSource);

    if (itTargetLookup == m_mapPackageOverrideRules.end())
    {
        //TRACE_ODS1(L"no override rule found for %s", packageSource);
        return false;
    }

    wstring packageTarget = itTargetLookup->second;

    TRACE_ODS1(L"override rule from %s", packageSource);
    TRACE_ODS1(L"override rule to %s", packageTarget);

    ::wcsncpy_s(pwszPackageTargetOverride,
        cchPackageTargetOverride,
        packageTarget.c_str(),
        wcslen(packageTarget.c_str())
        );

    return true;
}

//@@ if package version does not exist then down load - CreateProcess -> NuGet.exe 
//@@ consider option to also run nuget.exe install %CD%\packages.config -OutputDirectory %FOO%\packages
bool PackageLoader::NuGetCmdLine_FindEXE()
{
    bool fSuccess = FALSE;
    wchar_t nuGetCmdLinePackagePath[MAX_PATH];

    for(int packageCache=0;packageCache<g_piKitePackagesPathsCount;packageCache++)
    {
        std::wstring cmdLinePath(g_prgwszKitePackagesPaths[packageCache]);

        cmdLinePath += g_pwszKitePackagesNuGetCmdLinePackageNameDefault;
        cmdLinePath += L"\\";
        cmdLinePath += g_pwszKitePackagesNuGetCmdLineToolsDirectorySegmentDefault;
        cmdLinePath += g_pwszKitePackagesNuGetCmdLineToolName;

        ::ExpandEnvironmentStringsW(cmdLinePath.c_str(), nuGetCmdLinePackagePath, MAX_PATH);

        if (::GetFileAttributesW(nuGetCmdLinePackagePath) != INVALID_FILE_ATTRIBUTES)
        {
            wcscpy_s(g_wszKitePackagesNuGetCmdLinePackagePath,
                _countof(g_wszCoreCLRPackageDirectoryName),
                cmdLinePath.c_str()
                );
            fSuccess = TRUE;

            TRACE_ODS1(L"NuGetCmdLine_FindEXE Found %s", g_wszKitePackagesNuGetCmdLinePackagePath);
            break;
        }
        else
        {
            TRACE_ODS1(L"NuGetCmdLine_FindEXE NotFound %s", cmdLinePath.c_str());
        }
    }

    if (fSuccess == FALSE)
    {
        g_wszCoreCLRPackageDirectoryName[0] = L'\0';
    }

    return fSuccess;
}

//Loader
bool PackageLoader::TryDownloadPackage(LPWSTR pwszPackageName, LPWSTR pwszPackageVersion, LPWSTR g_wszKitePackagesPathDownload)
{
    bool fSuccess = false;
    LONG lRet = NO_ERROR;
    HRESULT hr = S_OK;
    PROCESS_INFORMATION pi = {};
    STARTUPINFOW si = {};
    DWORD dwWaitResult  = 0;
    DWORD dwProcessExitCode = 500;
    DWORD dwWaitForProcessMilliseconds = g_dwKite_PackageDownload_WaitForProcessDefault;
    DWORD dwStartProcessTime = 0;

    WCHAR wszCommand[MAX_PATH*3]; 
    wszCommand[0] = L'\0';

    if (g_wszKitePackagesNuGetCmdLinePackagePath[0] == L'0')
    {
        lRet = ERROR_INVALID_PARAMETER;
        goto Finished;
    }

    ZeroMemory(&pi, sizeof(pi));
    ZeroMemory(&si, sizeof(si));
    si.cb = sizeof(si);
    si.dwFlags = STARTF_USESTDHANDLES;  
    si.hStdInput = GetStdHandle(STD_INPUT_HANDLE);
    si.hStdOutput = GetStdHandle(STD_OUTPUT_HANDLE);
    si.hStdError = GetStdHandle(STD_ERROR_HANDLE);

    // Defense CheckCheck
    if ((pwszPackageName == NULL)       || 
        (pwszPackageVersion == NULL)    || 
        (g_wszKitePackagesPathDownload == NULL)
        )
    {
        lRet = ERROR_INVALID_PARAMETER;
        goto Finished;
    }

    //Note: Template: NuGet.exe install packageNameOrPackagesConfig -Version <version> -OutputDirectory <directory>

    LPWSTR pwszNuGetInstallPackageArgumentTemplate = L" install %s -Version %s -OutputDirectory %s";

    //Prepare CommandLine
    hr = StringCchPrintfW(
        wszCommand,
        _countof(wszCommand), 
        pwszNuGetInstallPackageArgumentTemplate,
        pwszPackageName, 
        pwszPackageVersion,
        g_wszKitePackagesPathDownload);
    if (FAILED(hr))
    {
        //lRet = WIN32_FROM_HRESULT(hr);
        lRet = hr;
        goto Finished;
    }

#ifdef HOST_TRACE_MESSAGE_ENABLED
    {
        TRACE_ODS2(L"CreateProcessW::NUGet.b AppName:%s CmdLine:%s", 
            g_wszKitePackagesNuGetCmdLinePackagePath, 
            wszCommand);
    }
#endif

    dwStartProcessTime = GetTickCount();
    if (CreateProcessW(
        g_wszKitePackagesNuGetCmdLinePackagePath, //LPCWSTR pszApplicationName,
        wszCommand,             //LPWSTR pszCommandLine,
        NULL,                   //LPSECURITY_ATTRIBUTES pProcessAttributes,
        NULL,                   //LPSECURITY_ATTRIBUTES pThreadAttributes,
        FALSE,                  //BOOL bInheritHandles,
        0,                      //DWORD dwCreationFlags,
        NULL,                   //LPVOID pEnvironment,
        NULL,                   //LPCWSTR pszCurrentDirectory,
        &si,                    //LPSTARTUPINFOW pStartupInfo,
        &pi                     //LPPROCESS_INFORMATION pProcessInformation
        ) == FALSE)
    {
        lRet = GetLastError();

        {
#ifdef HOST_TRACE_MESSAGE_ENABLED            
            TRACE_ODS3(L"TryDownloadPackage CreateProcessW::NuGet.f: GetLastError=0x%08x AppName:%s CmdLine:%s", 
                lRet,
                g_wszKitePackagesNuGetCmdLinePackagePath, 
                wszCommand
                );
#endif
        }

        goto Finished;
    }

    //Wait NNN seconds until process finishes
    dwWaitResult = WaitForSingleObject(pi.hProcess, dwWaitForProcessMilliseconds); 
    if ((dwWaitResult == WAIT_TIMEOUT) || (dwWaitResult == WAIT_FAILED))
    {
        TRACE_ODS1(L"NuGet.exe waiting thread timeout: WT: %d", dwWaitForProcessMilliseconds);
        TerminateProcess(pi.hProcess, ~0u);
        lRet = ERROR_INVALID_PARAMETER;
        goto Finished;
    }

    //Cleanup
    GetExitCodeProcess(pi.hProcess, &dwProcessExitCode);
    if (dwProcessExitCode != 0) 
    {
        TRACE_ODS1(L"NuGet.exe failed: ExitCode=%d", dwProcessExitCode);
        lRet = dwProcessExitCode;
        goto Finished;
    }

#ifdef HOST_TRACE_MESSAGE_ENABLED            
    //        if (g_fFilecache_AutoUpdateManifest_Trace == TRUE)
    {
        TRACE_ODS2(L"CreateProcess: NuGet.exe: ExecTime:%d ExitCode=%d", GetTickCount() - dwStartProcessTime, dwProcessExitCode);
    }
#endif

Finished:
    //Cleanup
    if (pi.hProcess != INVALID_HANDLE_VALUE)
    {
        CloseHandle(pi.hProcess);
    }

    if (pi.hThread != INVALID_HANDLE_VALUE)
    {
        CloseHandle(pi.hThread);
    }

    if (lRet == NO_ERROR)
    {
        fSuccess = TRUE;
    }
    else
    {
        fSuccess = FALSE;
    }

    return fSuccess;
}


//@@ClrHostRuntimeModule::LoadCoreCLRModule()
//@@Move to PackagerLoader
//@@@@
/*
//@@TODO switch to: 
//@@TODO bool PackageOverrideRuleLookup(LPWSTR pwszPackageOverrideRuleSourceLookup, LPWSTR pwszPackageTargetOverride, size_t cchPackageTargetOverride)

//Override rule lookup
std::wstring packageSource(g_wszKitePackagesCoreCLRPackageName);
std::map<wstring,wstring>::iterator itTargetLookup;

itTargetLookup = m_mapPackageOverrideRules.find(packageSource);

if (itTargetLookup == m_mapPackageOverrideRules.end())
{
TRACE_ODS1(L"no override rule found for %s", packageSource);
}
else
{
wstring packageTarget = itTargetLookup->second;

TRACE_ODS1(L"override rule from %s", packageSource);
TRACE_ODS1(L"override rule to %s", packageTarget);

::wcsncpy_s(g_wszKitePackagesCoreCLRPackageName,
_countof(g_wszKitePackagesCoreCLRPackageName),
packageTarget.c_str(),
wcslen(packageTarget.c_str())
);
}
*/

//@@Move to PackagerLoader

// First try to load CoreCLR from the directory that kite is in
//@@ do we want to continue looking for CoreCLR next to host?
//@@ maybe we should just look in a package ?
//m_hCLRModule = TryLoadCLRModule(m_pFirmware->GetHostDirectoryPath());

