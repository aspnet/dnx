#include "stdafx.h"
#include "Firmware.h"

//@@FUTURE move into Firmware class
wchar_t* g_pwszHostBitness_EnvVarName = L"PROCESSOR_ARCHITECTURE";
wchar_t* g_pwszKiteSystemTrace_EnvVarName = L"K.S.Trace";

bool g_fKiteSystemTraceDefault = false;

wchar_t* g_pwszKitePackageOverrideRulesPath_EnvVarName = L"K.P.OverrideRules";
wchar_t* g_pwszKitePackageOverrideRulesPathDefault = L"";
wchar_t g_wszKitePackageOverrideRulesPath[MAX_PATH];

wchar_t* g_pwszPackageOverrideRulesFileName = L"packages.OverrideRules.config";

// List of PackageCachePathRoots
wchar_t* g_pwszKitePackagesPathSearch_EnvVarName = L"K.P.Path";
wchar_t* g_pwszKitePackagesPathsDefault = L"";
const int g_cKitePackagesPathSearchMax = 12;
int g_piKitePackagesPathsCount;
LPWSTR* g_prgwszKitePackagesPaths;

wchar_t* g_pwszKitePackagesPathDownload_EnvVarName = L"K.P.Download";
wchar_t* g_pwszKitePackagesPathDownloadDefault = L"";
wchar_t g_wszKitePackagesPathDownload[MAX_PATH];

wchar_t* g_prgwszKitePackagesFrameworks[] = { L"k", L"kite", L"portable", L"net45", L"net40",  NULL};
int g_cKitePackagesFrameworks = 5;

wchar_t* g_pwszKitePackagesNuGetCmdLinePackageName_EnvVarName = L"K.P.NuGetCmdLine";
wchar_t* g_pwszKitePackagesNuGetCmdLinePackageNameDefault = L"NuGet.CommandLine.2.2.1";
wchar_t g_wszKitePackagesNuGetCmdLinePackageName[MAX_STR];

DWORD g_dwKite_PackageDownload_WaitForProcessDefault;
wchar_t* g_pwszKitePackagesNuGetCmdLineToolsDirectorySegmentDefault = L"tools\\";

wchar_t* g_pwszKitePackagesNuGetCmdLineToolName = L"NuGet.exe";
wchar_t g_wszKitePackagesNuGetCmdLinePackagePath[MAX_PATH];

//-------------------------------
// Package Manifests
wchar_t* g_pwszPackageManifestFileName = L"packages.filelist.config";
const DWORD g_cchPackageManifestLineLengthMax = 255; //;MAX_STR;

//-------------------------------
//CoreCLR
wchar_t* g_pwszKitePackagesCoreCLRPackageName_EnvVarName = L"K.P.CoreCLR";
wchar_t* g_pwszKitePackagesCoreCLRPackageNameDefault = L"CoreCLR.1.0.0.0";
wchar_t g_wszKitePackagesCoreCLRPackageName[MAX_PATH];

wchar_t* g_pwszKitePackagesCoreCLRPackageLibraryDirectorySegmentDefault = L"lib\\kite";

WCHAR g_wszCoreCLRPackageDirectoryName[MAX_PATH];

Firmware::Firmware(void)
{
    //Native Host - this module
    m_wszHostBitness[0] = L'\0';
    m_wszHostPath[0] = L'\0';
    m_wszHostDirectoryPath[0] = L'\0';
    m_pwszHostExeName = nullptr;

    //CLR Module
    wcscpy_s(m_wszCLRModuleName, _countof(m_wszCLRModuleName), L"CoreCLR.dll");

    //Default - Managed Host i.e. C# AppDomainManager
    wcscpy_s(m_wszManagedHostAssemblyFileNameDefault, L"K.Core.Host.dll");
    wcscpy_s(m_wszManagedHostAssemblyNameDefault, L"K.Core.Host,Version=1.0.0.0");
    wcscpy_s(m_wszManagedHostTypeDefault, L"K.Core.Host.DomainManager");
    wcscpy_s(m_wszManagedHostMethodNameStartupDefault,  L"HostStartup");
    wcscpy_s(m_wszManagedHostMethodNameMainDefault,     L"HostMain");
    wcscpy_s(m_wszManagedHostMethodNameShutdownDefault, L"HostShutdown");

    //Default - User Application
    m_wszApplicationTypeName[0] = L'\0';
    m_wszApplicationAssembly[0] = L'\0';
    m_wszApplicationDirectoryPath[0] = L'\0';
    m_wszApplicationDirectoryProbes[0] = L'\0';
    m_wszApplicationPackagesPath[0] = L'\0';

    //Internals
    m_pPackageLoader = nullptr;
    m_pClrHostRuntimeModule = nullptr;

    m_fVerboseTrace = false;
    m_fWaitForDebugger = false;
    m_fWaitForDomainShutdown = true;

    m_argc = 0;
    m_argv = nullptr;
    m_iExitCode = 0;
}

Firmware::~Firmware(void)
{
    Dispose();
}

bool Firmware::Init()
{
    bool fSuccess = true;

    fSuccess = ReadEnvironmentVariables();
    if (!fSuccess)
        goto Finished;

    fSuccess = InitHostPath();
    if (!fSuccess)
        goto Finished;

Finished:
    return fSuccess;
}

bool Firmware::Dispose()
{
    bool fSuccess = true;

    //safely delete objects 
    //set pointers to null

    if (m_pClrHostRuntimeModule)
    {
        m_pClrHostRuntimeModule->Dispose();
        delete m_pClrHostRuntimeModule;
        m_pClrHostRuntimeModule = nullptr;
    }

    if (m_pPackageLoader)
    {
        m_pPackageLoader->Dispose();
        delete m_pPackageLoader;
        m_pPackageLoader = nullptr;
    }

    return fSuccess;
}

wchar_t* Firmware::GetNamedValue(wchar_t pwszKey)
{
    //@@TODO - lookup table
    return nullptr;
}

bool Firmware::SetNamedValue(wchar_t pwszKey, wchar_t pwszValue)
{
    //@@TODO - lookup table
    return true;
}

wchar_t* Firmware::GetCLRModuleName()
{
    return m_wszCLRModuleName;
}

ClrHostRuntimeModule* Firmware::GetClrHostRuntimeModule()
{
    return m_pClrHostRuntimeModule;
}

bool Firmware::SetClrHostRuntimeModule(ClrHostRuntimeModule* pClrHostRuntimeModule)
{
    bool fSuccess = true;

    if (pClrHostRuntimeModule == nullptr)
    {
        fSuccess = false;
        goto Finished;
    }

    m_pClrHostRuntimeModule = pClrHostRuntimeModule;

Finished:
    return fSuccess;
}

PackageLoader* Firmware::GetPackageLoader()
{
    return m_pPackageLoader;
}

bool Firmware::SetPackageLoader(PackageLoader* pPackageLoader)
{
    bool fSuccess = true;

    if (pPackageLoader == nullptr)
    {
        fSuccess = false;
        goto Finished;
    }

    m_pPackageLoader = pPackageLoader;

Finished:
    return fSuccess;
}

wchar_t* Firmware::GetHostDirectoryPath()
{
    return m_wszHostDirectoryPath;
}

// Returns the path to the host module directory
const wchar_t*  Firmware::GetHostPath() 
{
    return m_wszHostPath;
}

// Returns the path to the host module
const wchar_t*  Firmware::GetHostExeName() 
{
    return m_pwszHostExeName;
}

bool Firmware::InitHostPath()
{
    bool fSuccess = true;
    int lastBackslashIndex=0;
    DWORD thisModuleLength = 0;

    // Discover the path to this process's module. All other files are expected to be in the same directory.
    thisModuleLength = ::GetModuleFileNameW(::GetModuleHandleW(nullptr), m_wszHostPath, MAX_PATH);

    // Search for the last backslash in the host path.
    for (lastBackslashIndex = thisModuleLength-1; lastBackslashIndex >= 0; lastBackslashIndex--) {
        if (m_wszHostPath[lastBackslashIndex] == L'\\') {
            break;
        }
    }

    // Copy the directory path
    ::wcsncpy_s(m_wszHostDirectoryPath, m_wszHostPath, lastBackslashIndex + 1);

    // Save the exe name
    m_pwszHostExeName = m_wszHostPath + lastBackslashIndex + 1;

    TRACE_ODS1(L"Host directory: %s", m_wszHostDirectoryPath );

    return fSuccess;
}

wchar_t* Firmware::GetManagedHostAssemblyFileNameDefault()
{
    return m_wszManagedHostAssemblyFileNameDefault;
}

wchar_t* Firmware::GetManagedHostAssemblyNameDefault()
{
    return m_wszManagedHostAssemblyNameDefault;
}

wchar_t* Firmware::GetManagedHostTypeDefault()
{
    return m_wszManagedHostTypeDefault;
}

wchar_t* Firmware::GetManagedHostMethodNameStartupDefault()
{
    return m_wszManagedHostMethodNameStartupDefault;
}

wchar_t* Firmware::GetManagedHostMethodNameMainDefault()
{
    return m_wszManagedHostMethodNameMainDefault;
}

wchar_t* Firmware::GetManagedHostMethodNameShutdownDefault()
{
    return m_wszManagedHostMethodNameShutdownDefault;
}

wchar_t* Firmware::GetApplicationTypeName()
{
    return m_wszApplicationTypeName;
}

wchar_t* Firmware::GetApplicationAssembly()
{
    return m_wszApplicationAssembly;
}

wchar_t* Firmware::GetApplicationDirectoryPath()
{
    return m_wszApplicationDirectoryPath;
}

int Firmware::GetApplicationArgc()
{
    return m_ApplicationArgc;
}

wchar_t** Firmware::GetApplicationArgv()
{
    return m_ApplicationArgv;
}

bool Firmware::GetVerboseTrace()
{
    return m_fVerboseTrace;
}

bool Firmware::GetWaitForDebugger()
{
    return m_fWaitForDebugger;
}

bool Firmware::GetWaitForDomainShutdown()
{
    return m_fWaitForDomainShutdown;
}

void Firmware::SetExitCode(int exitCode)
{
    m_iExitCode = exitCode;
}

int Firmware::GetExitCode()
{
    return m_iExitCode;
}

wchar_t* Firmware::GetHostBitness()
{
    return m_wszHostBitness;
}

bool Firmware::ShowUsage()
{
    ::wprintf(
        L"Runs Project K executables on CoreCLR\r\n"
        L"\r\n"
        L"USAGE: kstart [---switches] [application|.dll|.exe] [application args]\r\n"
        L"\r\n"
        L"        [application|.dll|.exe] is a managed image built for CoreCLR\r\n"
        L"\r\n"
        L"        ---help            This information\r\n"
        L"          /?\r\n"
        L"\r\n"
        L"        ---traceVerbose    Causes verbose output to be written to the console\r\n"
        L"          /tv\r\n"
        L"\r\n"
        L"        ---waitForDebugger Causes kite to wait for a debugger to attach before launching Managed.exe\r\n"
        L"          /wd\r\n"
        L"\r\n"
        L"        ---HostDomainManagerAssembly    K.Core.Host,Version=1.0.0.0           \r\n"
        L"          /hdma\r\n"
        L"\r\n"
        L"        ---HostDomainManagerType        K.Core.Host.DomainManager             \r\n"
        L"          /hdmt\r\n"
        L"\r\n"
        L"        ---applicationtype              Foo.Bar or Application\r\n"
        L"        ---apptype\r\n"
        L"          /apptype\r\n"
        L"\r\n"
        L"        ---application                  User Application Module. Can be .dll or .exe\r\n"
        L"        ---app\r\n"
        L"           /app\r\n"
        L"\r\n"
        L"        ---applicationroot              c:\\Bar\\ or c:\\blah\\sites\\Bar\\wwwroot\\ or \\\\srv1\\blah\\sites\\Bar\\wwwroot\\ \r\n"
        L"        ---approot\r\n"
        L"          /approot\r\n"
        //L"\r\n"
        //L"        ---primePackages causes all packages specific in manifest to be downloaded, if packages don't exist in one of the package caches\r\n"
        //L"\r\n"
        L"\r\n"
        );

    return true;
}

bool Firmware::ProcessCommmandLine(const int argc, const wchar_t* argv[])
{
    bool fSuccess = true;
    bool helpRequested = false;
    // Parse the options from the command line
    int newArgc = argc - 1;
    const wchar_t* *newArgv = argv + 1;

    auto stringsEqual = [](const wchar_t*  const a, const wchar_t*  const b) -> bool 
    {
        return ::_wcsicmp(a, b) == 0;
    };

    auto tryParseOptionBool = [&](const wchar_t* arg) -> bool 
    {
        if (   stringsEqual(arg, L"/tv") 
            || stringsEqual(arg, L"---tv")  
            || stringsEqual(arg, L"---traceVerbose")  
            ) 
        {
            m_fVerboseTrace = true;
            return true;
        } 
        else if ( stringsEqual(arg, L"/wd") 
            || stringsEqual(arg, L"---wd") 
            || stringsEqual(arg, L"---waitForDebugger") 
            ) 
        {
            m_fWaitForDebugger = true;
            return true;
        } 
        else if ( stringsEqual(arg, L"/?") 
            || stringsEqual(arg, L"-?") 
            || stringsEqual(arg, L"---help") 
            ) 
        {
            helpRequested = true;
            return true;
        } 
        //Managed Host i.e. C# AppDomain Manager
        else if ( stringsEqual(arg, L"/hdma") 
            || stringsEqual(arg, L"---hdma") 
            || stringsEqual(arg, L"---HostDomainManagerAssembly") 
            ) 
        {
            newArgc--;
            newArgv++;

            wcscpy_s(m_wszManagedHostAssemblyNameDefault, 
                        _countof(m_wszManagedHostAssemblyNameDefault),
                        *newArgv);
            return true;
        } 

        else if ( stringsEqual(arg, L"/hdmt") 
            || stringsEqual(arg, L"---hdmt") 
            || stringsEqual(arg, L"---HostDomainManagerType") 
            ) 
        {
            newArgc--;
            newArgv++;

            wcscpy_s(m_wszManagedHostTypeDefault, 
                        _countof(m_wszManagedHostTypeDefault),
                        *newArgv);
            return true;
        }

        // Application Type Name
        else if ( stringsEqual(arg, L"/apptype") 
            || stringsEqual(arg, L"---apptype") 
            || stringsEqual(arg, L"---applicationtype") 
            ) 
        {
            newArgc--;
            newArgv++;

            wcscpy_s(m_wszApplicationTypeName, 
                        _countof(m_wszApplicationTypeName),
                        *newArgv);

            return true;
        } 

        // Application DLL path
        else if ( stringsEqual(arg, L"/app") 
            || stringsEqual(arg, L"---app") 
            || stringsEqual(arg, L"---application") 
            ) 
        {
            newArgc--;
            newArgv++;

            wcscpy_s(m_wszApplicationAssembly, 
                        _countof(m_wszApplicationAssembly),
                        *newArgv);

            return true;
        } 

        // Application DLL path
        else if ( stringsEqual(arg, L"/approot") 
            || stringsEqual(arg, L"---approot") 
            || stringsEqual(arg, L"---applicationroot") 
            ) 
        {
            newArgc--;
            newArgv++;

            wcscpy_s(m_wszApplicationDirectoryPath, 
                        _countof(m_wszApplicationDirectoryPath),
                        *newArgv);

            return true;
        } 
        else 
        {
            //Check for application assembly
            if (wcslen(m_wszApplicationAssembly) == 0)
            {
                ::wcscpy_s(m_wszApplicationAssembly,_countof(m_wszApplicationAssembly), *newArgv);

                newArgc--;
                newArgv++;
            }

            newArgc = newArgc;
            return false;
        }
    };

    //Loop through command line params
    while (newArgc > 0 && tryParseOptionBool(newArgv[0])) 
    {
        newArgc--;
        newArgv++;
    }

    if ( 
           (helpRequested)
        || (wcslen(m_wszApplicationAssembly) == 0)
        || (argc < 2) //?? Why - is this legacy from ccrun ??
        ) 
    {
        ShowUsage();
        fSuccess = false;
        return fSuccess;
    }

    //Remaing argc, argv are for User Application
    m_ApplicationArgc = newArgc;
    m_ApplicationArgv = (wchar_t**)newArgv;

    return fSuccess;
}


// Firmware Settings, Loader
bool Firmware::ReadEnvironmentVariables()
{
    bool fSuccess = true;

    //KiteSystemTrace
    g_fKiteSystemTrace = g_fKiteSystemTraceDefault;

    WCHAR wszKiteSystemTrace_EnvVar[MAX_PATH] = {};
    DWORD dwCchKiteSystemTrace_EnvVar = 0;

    dwCchKiteSystemTrace_EnvVar = GetEnvironmentVariableW(
        g_pwszKiteSystemTrace_EnvVarName,
        wszKiteSystemTrace_EnvVar,
        _countof(wszKiteSystemTrace_EnvVar)
        );
    if (dwCchKiteSystemTrace_EnvVar == 0)
    {
        g_fKiteSystemTrace = FALSE;

        DWORD dwError = ::GetLastError();
        TRACE_ODS_HR(L"GetEnvironmentVariableW KitePackagesPathDownload FAILED", dwError);
    }
    else
    {
        TRACE_ODS2(L"ReadEnvVar %s=%s", g_pwszKiteSystemTrace_EnvVarName, wszKiteSystemTrace_EnvVar);

        LPWSTR pwszEnable = L"1";

        if (CompareStringOrdinal(
            wszKiteSystemTrace_EnvVar, 
            wcslen(wszKiteSystemTrace_EnvVar), 
            pwszEnable, 
            wcslen(pwszEnable), 
            TRUE) == CSTR_EQUAL)
        {
            g_fKiteSystemTrace = TRUE;
        }
        else
        {
            g_fKiteSystemTrace = FALSE;
        }
    }

    //HostBitness
    DWORD dwCcHostBitness_EnvVar = 0;
    dwCcHostBitness_EnvVar = GetEnvironmentVariableW(
        g_pwszHostBitness_EnvVarName,
        m_wszHostBitness,
        _countof(m_wszHostBitness)
        );
    if (dwCcHostBitness_EnvVar == 0)
    {
        TRACE_ODS_HR(L"GetEnvironmentVariableW PROCESSOR_ARCHITECTURE FAILED", ::GetLastError());
        fSuccess = false;
        goto Finished;
    }

    TRACE_ODS2(L"ReadEnvVar %s=%s", g_pwszHostBitness_EnvVarName, m_wszHostBitness);

    //Override Rules
    WCHAR wszKitePackageOverrideRulesPath_EnvVar[MAX_PATH];
    DWORD dwCchKitePackageOverrideRulesPath_EnvVar = 0;

    dwCchKitePackageOverrideRulesPath_EnvVar = GetEnvironmentVariableW(
        g_pwszKitePackageOverrideRulesPath_EnvVarName,
        wszKitePackageOverrideRulesPath_EnvVar,
        _countof(wszKitePackageOverrideRulesPath_EnvVar)
        );

    if (dwCchKitePackageOverrideRulesPath_EnvVar == 0)
    {
        DWORD dwError = ::GetLastError();

        TRACE_ODS_HR(L"GetEnvironmentVariableW KitePackageOverrideRulesPath FAILED", dwError);
        ::ExitProcess(dwError);
    }

    TRACE_ODS2(L"ReadEnvVar %s=%s", g_pwszKitePackageOverrideRulesPath_EnvVarName, wszKitePackageOverrideRulesPath_EnvVar);
    if (dwCchKitePackageOverrideRulesPath_EnvVar > 0)
    {
        ::wcsncpy_s(g_wszKitePackageOverrideRulesPath,
            _countof(g_wszKitePackageOverrideRulesPath),
            wszKitePackageOverrideRulesPath_EnvVar,
            wcslen(wszKitePackageOverrideRulesPath_EnvVar)
            );
    }

    //Path
    WCHAR wszKitePackagesPathSearch_EnvVar[MAX_PATH*g_cKitePackagesPathSearchMax];
    DWORD dwCchKitePackagesPathSearch_EnvVar = 0;

    dwCchKitePackagesPathSearch_EnvVar = GetEnvironmentVariableW(
        g_pwszKitePackagesPathSearch_EnvVarName,
        wszKitePackagesPathSearch_EnvVar,
        _countof(wszKitePackagesPathSearch_EnvVar)
        );

    if (dwCchKitePackagesPathSearch_EnvVar == 0)
    {
        DWORD dwError = ::GetLastError();

        TRACE_ODS_HR(L"GetEnvironmentVariableW KitePackagesPathSearch FAILED", dwError);
        ::ExitProcess(dwError);
    }

    TRACE_ODS2(L"ReadEnvVar %s=%s", g_pwszKitePackagesPathSearch_EnvVarName, wszKitePackagesPathSearch_EnvVar);
    if (dwCchKitePackagesPathSearch_EnvVar > 0)
    {
        WCHAR wszSeps[]   = L";";
        LPWSTR token1 = NULL;
        LPWSTR next_token1 = NULL;

        int packageCaches = 0;
        int dwBufferSize = sizeof(LPWSTR) * g_cKitePackagesPathSearchMax;

        g_prgwszKitePackagesPaths = (LPWSTR *)malloc(dwBufferSize);
        if (g_prgwszKitePackagesPaths == NULL)
        {
            fSuccess = false;
            goto Finished;
        }

        memset(g_prgwszKitePackagesPaths, 0, dwBufferSize);

        // Establish string and get the first token:
        token1 = wcstok_s(wszKitePackagesPathSearch_EnvVar, wszSeps, &next_token1);

        // While there are tokens in the buffer
        while (token1 != NULL)
        {
            if (token1 != NULL)
            {
                //check for empty string
                if (token1[0] != 0)
                {
                    g_prgwszKitePackagesPaths[packageCaches] = new WCHAR[MAX_PATH];

                    ::wcsncpy_s(g_prgwszKitePackagesPaths[packageCaches], 
                        MAX_PATH, 
                        token1,
                        wcslen(token1)
                        );

                    TRACE_ODS2(L"\tPackageCache Path %d %s", packageCaches, g_prgwszKitePackagesPaths[packageCaches]);

                    packageCaches++;
                }

                // Get next token:
                if (next_token1 < (wszKitePackagesPathSearch_EnvVar + dwCchKitePackagesPathSearch_EnvVar) )
                {
                    token1 = wcstok_s( NULL, wszSeps, &next_token1);
                }
                else 
                {
                    break;
                }
            }
        }

        g_piKitePackagesPathsCount = packageCaches;
        TRACE_ODS1(L"\tPackageCache Path Count %d", g_piKitePackagesPathsCount);
    }

    WCHAR wszKitePackagesPathDownload_EnvVar[MAX_PATH];
    DWORD dwCchKitePackagesPathDownload_EnvVar = 0;

    dwCchKitePackagesPathDownload_EnvVar = GetEnvironmentVariableW(
        g_pwszKitePackagesPathDownload_EnvVarName,
        wszKitePackagesPathDownload_EnvVar,
        _countof(wszKitePackagesPathDownload_EnvVar)
        );

    if (dwCchKitePackagesPathDownload_EnvVar == 0)
    {
        DWORD dwError = ::GetLastError();

        TRACE_ODS_HR(L"GetEnvironmentVariableW KitePackagesPathDownload FAILED", dwError);
        ::ExitProcess(dwError);
    }

    TRACE_ODS2(L"ReadEnvVar %s=%s", g_pwszKitePackagesPathDownload_EnvVarName, wszKitePackagesPathDownload_EnvVar);
    if (dwCchKitePackagesPathDownload_EnvVar > 0)
    {
        ::wcsncpy_s(g_wszKitePackagesPathDownload,
            _countof(g_wszKitePackagesPathDownload),
            wszKitePackagesPathDownload_EnvVar,
            wcslen(wszKitePackagesPathDownload_EnvVar)
            );
    }

    //SET Kite.Packages.NuGetCmdLine.PackageName=NuGet.CommandLine.2.2.1
    WCHAR wszKitePackagesNuGetCmdLinePackageName_EnvVar[MAX_PATH];
    DWORD dwCchKitePackagesNuGetCmdLinePackageName_EnvVar = 0;

    dwCchKitePackagesNuGetCmdLinePackageName_EnvVar = GetEnvironmentVariableW(
        g_pwszKitePackagesNuGetCmdLinePackageName_EnvVarName,
        wszKitePackagesNuGetCmdLinePackageName_EnvVar,
        _countof(wszKitePackagesNuGetCmdLinePackageName_EnvVar)
        );

    if (dwCchKitePackagesNuGetCmdLinePackageName_EnvVar == 0)
    {
        DWORD dwError = ::GetLastError();

        TRACE_ODS_HR(L"GetEnvironmentVariableW KitePackagesNuGetCmdLinePackageName FAILED", dwError);
        //::ExitProcess(dwError);

        ::wcsncpy_s(g_wszKitePackagesNuGetCmdLinePackageName,
            _countof(g_wszKitePackagesNuGetCmdLinePackageName),
            g_pwszKitePackagesNuGetCmdLinePackageNameDefault,
            wcslen(g_pwszKitePackagesNuGetCmdLinePackageNameDefault)
            );
    }

    TRACE_ODS2(L"ReadEnvVar %s=%s", g_pwszKitePackagesNuGetCmdLinePackageName_EnvVarName, wszKitePackagesNuGetCmdLinePackageName_EnvVar);
    if (dwCchKitePackagesNuGetCmdLinePackageName_EnvVar > 0)
    {
        ::wcsncpy_s(g_wszKitePackagesNuGetCmdLinePackageName,
            _countof(g_wszKitePackagesNuGetCmdLinePackageName),
            wszKitePackagesNuGetCmdLinePackageName_EnvVar,
            wcslen(wszKitePackagesNuGetCmdLinePackageName_EnvVar)
            );
    }

    //-------------------------------
    //CoreCLR
    //SET Kite.Packages.CoreCLR.PackageName=CoreCLR.1.0.0.0
    WCHAR wszKitePackagesCoreCLRPackageName_EnvVar[MAX_PATH];
    DWORD dwCchKitePackagesCoreCLRPackageName_EnvVar = 0;

    dwCchKitePackagesCoreCLRPackageName_EnvVar = GetEnvironmentVariableW(
        g_pwszKitePackagesCoreCLRPackageName_EnvVarName,
        wszKitePackagesCoreCLRPackageName_EnvVar,
        _countof(wszKitePackagesCoreCLRPackageName_EnvVar)
        );

    if (dwCchKitePackagesCoreCLRPackageName_EnvVar == 0)
    {
        DWORD dwError = ::GetLastError();

        TRACE_ODS_HR(L"GetEnvironmentVariableW KitePackagesCoreCLRPackageName FAILED", dwError);
        //::ExitProcess(dwError);

        ::wcsncpy_s(g_wszKitePackagesCoreCLRPackageName,
            _countof(g_wszKitePackagesCoreCLRPackageName),
            g_pwszKitePackagesCoreCLRPackageNameDefault,
            wcslen(g_pwszKitePackagesCoreCLRPackageNameDefault)
            );
    }

    TRACE_ODS2(L"ReadEnvVar %s=%s", g_pwszKitePackagesCoreCLRPackageName_EnvVarName, wszKitePackagesCoreCLRPackageName_EnvVar);
    if (dwCchKitePackagesCoreCLRPackageName_EnvVar > 0)
    {
        ::wcsncpy_s(g_wszKitePackagesCoreCLRPackageName,
            _countof(g_wszKitePackagesCoreCLRPackageName),
            wszKitePackagesCoreCLRPackageName_EnvVar,
            wcslen(wszKitePackagesCoreCLRPackageName_EnvVar)
            );
    }

Finished:
    return fSuccess;
}

bool Firmware::Startup()
{
    bool fSuccess = true;
    PackageLoader* pPackageLoader = nullptr;
    ClrHostRuntimeModule* pClrHostRuntimeModule = nullptr;

    //Create PackageLoader
    pPackageLoader = new PackageLoader();
    if (pPackageLoader == nullptr)
    {
        fSuccess = false;
        goto Finished;
    }

    //Create ClrHostRuntimeModule
    fSuccess = pPackageLoader->Init(this);
    if (fSuccess == false)
        goto Finished;

    fSuccess = SetPackageLoader(pPackageLoader);
    if (fSuccess == false)
        goto Finished;

    pClrHostRuntimeModule = new ClrHostRuntimeModule ();
    if (pClrHostRuntimeModule == nullptr)
    {
        fSuccess = false;
        goto Finished;
    }

    fSuccess = pClrHostRuntimeModule->Init(this);
    if (fSuccess == false)
        goto Finished;

    fSuccess = SetClrHostRuntimeModule(pClrHostRuntimeModule);
    if (fSuccess == false)
        goto Finished;

    fSuccess = pClrHostRuntimeModule->Start();
    if (fSuccess == false)
        goto Finished;

Finished:
    if (fSuccess == false)
    {
    }

    return fSuccess;
}

//Used by EXE ProcessMain
bool Firmware::Execute()
{
    bool fSuccess = true;

    fSuccess = GetClrHostRuntimeModule()->Execute();

    return fSuccess;
}

bool Firmware::Shutdown()
{
    bool fSuccess = true;

    //Tell Domain to Shutdown
    fSuccess = GetClrHostRuntimeModule()->Shutdown();

    return fSuccess;
}
