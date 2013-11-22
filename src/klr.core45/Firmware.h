#pragma once

#pragma once

//@@FUTURE move into Firmware class
extern wchar_t* g_pwszHostBitness_EnvVarName;
extern wchar_t* g_pwszKiteSystemTrace_EnvVarName;

extern wchar_t* g_pwszKitePackageOverrideRulesPath_EnvVarName;
extern wchar_t* g_pwszKitePackageOverrideRulesPathDefault;
extern wchar_t g_wszKitePackageOverrideRulesPath[MAX_PATH];

extern wchar_t* g_pwszPackageOverrideRulesFileName;

// List of PackageCachePathRoots
extern wchar_t* g_pwszKitePackagesPathSearch_EnvVarName;
extern wchar_t* g_pwszKitePackagesPathsDefault;
extern const int g_cKitePackagesPathSearchMax;
extern int g_piKitePackagesPathsCount;
extern LPWSTR* g_prgwszKitePackagesPaths;

extern wchar_t* g_pwszKitePackagesPathDownload_EnvVarName;
extern wchar_t* g_pwszKitePackagesPathDownloadDefault;
extern wchar_t g_wszKitePackagesPathDownload[MAX_PATH];

extern wchar_t* g_prgwszKitePackagesFrameworks[];
extern int g_cKitePackagesFrameworks;

extern wchar_t* g_pwszKitePackagesNuGetCmdLinePackageName_EnvVarName;
extern wchar_t* g_pwszKitePackagesNuGetCmdLinePackageNameDefault;
extern wchar_t g_wszKitePackagesNuGetCmdLinePackageName[MAX_STR];

extern DWORD g_dwKite_PackageDownload_WaitForProcessDefault;
extern wchar_t* g_pwszKitePackagesNuGetCmdLineToolsDirectorySegmentDefault;

extern wchar_t* g_pwszKitePackagesNuGetCmdLineToolName;
extern wchar_t g_wszKitePackagesNuGetCmdLinePackagePath[MAX_PATH];

//-------------------------------
// Package Manifests
extern wchar_t* g_pwszPackageManifestFileName;
extern const DWORD g_cchPackageManifestLineLengthMax;
#define PACKAGE_MANIFEST_LINE_LENGTH_MAX (MAX_STR)

//-------------------------------
//CoreCLR
extern wchar_t* g_pwszKitePackagesCoreCLRPackageName_EnvVarName;
extern wchar_t* g_pwszKitePackagesCoreCLRPackageNameDefault;
extern wchar_t g_wszKitePackagesCoreCLRPackageName[MAX_PATH];

extern wchar_t* g_pwszKitePackagesCoreCLRPackageLibraryDirectorySegmentDefault;

extern WCHAR g_wszCoreCLRPackageDirectoryName[MAX_PATH];

class Firmware
{
private:
    bool m_fVerboseTrace;
    bool m_fWaitForDebugger;
    bool m_fWaitForDomainShutdown;

    //Native Host - this module
    wchar_t m_wszHostBitness[MAX_STR];

    // The path to this module
    wchar_t m_wszHostPath[MAX_PATH];

    // The path to the directory containing this host module
    wchar_t m_wszHostDirectoryPath[MAX_PATH];

    // The name of this module, without the path
    wchar_t* m_pwszHostExeName;

    //CLR
    wchar_t m_wszCLRModuleName[MAX_STR];

    //Default - Managed Host i.e. C# AppDomain Manager
    wchar_t m_wszManagedHostAssemblyFileNameDefault[MAX_STR];
    wchar_t m_wszManagedHostAssemblyNameDefault[MAX_STR];
    wchar_t m_wszManagedHostTypeDefault[MAX_STR];
    wchar_t m_wszManagedHostMethodNameStartupDefault[MAX_STR];
    wchar_t m_wszManagedHostMethodNameMainDefault[MAX_STR];
    wchar_t m_wszManagedHostMethodNameShutdownDefault[MAX_STR];

    //Default - User Application
    wchar_t m_wszApplicationTypeName[MAX_STR];

    // The path to the directory containing the User Application
    wchar_t m_wszApplicationAssembly[MAX_STR];

    wchar_t m_wszApplicationDirectoryPath[MAX_PATH];
    wchar_t m_wszApplicationDirectoryProbes[MAX_PATH]; // {/bin}
    wchar_t m_wszApplicationPackagesPath[MAX_PATH]; // {/bin/Packages}

    //Default Application Command Line
    int         m_ApplicationArgc;
    wchar_t**   m_ApplicationArgv;

    //Host Command Line
    int         m_argc;
    wchar_t**   m_argv;

    //Host Process - ExitCode
    int         m_iExitCode;

    //Internal
    Firmware* m_pFirmware;
    ClrHostRuntimeModule* m_pClrHostRuntimeModule;
    PackageLoader* m_pPackageLoader;

    //Environment Variables

public:
    ~Firmware(void);
    Firmware(void);

    bool Init();
    bool Dispose();

    wchar_t* GetNamedValue(wchar_t pwszKey);
    bool SetNamedValue(wchar_t pwszKey, wchar_t pwszValue);

    wchar_t* GetCLRModuleName();

    ClrHostRuntimeModule* GetClrHostRuntimeModule();
    bool SetClrHostRuntimeModule(ClrHostRuntimeModule* pClrHostRuntimeModule);

    PackageLoader* GetPackageLoader();
    bool SetPackageLoader(PackageLoader* pPackageLoader);

    wchar_t* GetHostDirectoryPath();
    const wchar_t*  GetHostPath();
    const wchar_t*  GetHostExeName();
    bool InitHostPath();

    wchar_t* GetManagedHostAssemblyFileNameDefault();
    wchar_t* GetManagedHostAssemblyNameDefault();
    wchar_t* GetManagedHostTypeDefault();

    wchar_t* GetManagedHostMethodNameStartupDefault();
    wchar_t* GetManagedHostMethodNameMainDefault();
    wchar_t* GetManagedHostMethodNameShutdownDefault();

    wchar_t* GetApplicationTypeName();
    wchar_t* GetApplicationAssembly();
    wchar_t* GetApplicationDirectoryPath();

    int GetApplicationArgc();
    wchar_t** GetApplicationArgv();

    bool GetVerboseTrace();
    bool GetWaitForDebugger();
    bool GetWaitForDomainShutdown();

    void SetExitCode(int exitCode);
    int GetExitCode();
    wchar_t* GetHostBitness();

    bool ShowUsage();
    bool ProcessCommmandLine(const int argc, const wchar_t* argv[]);
    bool ReadEnvironmentVariables();

    bool Startup();
    bool Execute();
    bool Shutdown();
};
