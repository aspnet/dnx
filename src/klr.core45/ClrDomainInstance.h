#pragma once

// Function Forwards
typedef int (STDMETHODCALLTYPE *HostStartupDelegate)(
    const int       argc,                 // Number of args in argv
    const wchar_t** argv,                 // Array of arguments
    const wchar_t*  hostPath,             // Path to the host executable
    const wchar_t*  applicationTypeName,  // The typename containing the application to run
    const wchar_t*  applicationAssembly,  // The assembly containing the application to run
    const byte      verboseTrace,         // Non-zero to indicate that verbose logging should be done
    const byte      waitForDebugger,      // Non-zero to indicate that the managed host should wait for a debugger to attach
    const int*      success               // Callee must set this non-zero to indicate the run succeeded
    );

typedef int (STDMETHODCALLTYPE *HostMainDelegate)(
    const int       argc,                 // Number of args in argv
    const wchar_t** argv,                 // Array of arguments
    const int*      success               // Callee must set this non-zero to indicate the run succeeded
    );

typedef int (STDMETHODCALLTYPE *HostShutdownDelegate)(
    const int       *success              // Callee must set this non-zero to indicate the run succeeded
    );

class ClrDomainInstance
{
private:
    Firmware* m_pFirmware;

    DWORD m_dwDomainId;

    //Application (User Code)
    wchar_t m_wszApplicationTypeName[MAX_STR];
    wchar_t m_wszApplicationAssembly[MAX_STR];

    // The path to the directory containing the application
    wchar_t m_wszApplicationDirectoryPath[MAX_PATH];

    wchar_t m_wszApplicationDirectoryProbeNameBinDefault[MAX_STR]; // "bin"
    wchar_t m_wszApplicationDirectoryProbes[MAX_PATH]; // {/bin}


    wchar_t m_wszApplicationDirectoryProbeNamePackagesDefault[MAX_STR]; // "packages"
    wchar_t m_wszApplicationPackagesPath[MAX_PATH]; // {/Packages}

    //-------------------------------
    //Managed Host for CoreCLR

    // The assembly name of the managed host dll. CoreCLR will bind
    //  to an assembly with this name and version, or an assembly with the
    //  same name and a later version. Setting this to too high a version
    //  will cause a failure in the call to CreateAppDomainWithManager.

    // K.Core.Host, Version=1.0.0.0
    wchar_t m_wszManagedHostAssembly[MAX_STR];

    // The type in the managed host dll that contains the entrypoint method. 
    // This must be a subclass of System.AppDomainManager.
    // K.Core.Host.DomainManager
    wchar_t m_wszManagedHostType[MAX_STR];

    //Names
    wchar_t m_wszManagedHostMethodNameStartup[MAX_STR];
    wchar_t m_wszManagedHostMethodNameMain[MAX_STR];
    wchar_t m_wszManagedHostMethodNameShutdown[MAX_STR];

    //Delegate instances
    HostStartupDelegate m_pHostStartup;
    HostMainDelegate m_pHostMain;
    HostShutdownDelegate m_pHostShutdown;

public:

    ClrDomainInstance(void);
    ~ClrDomainInstance(void);

    bool Init(Firmware* pFirmware);
    bool Dispose();

    bool CreateDomain();
    bool UnloadDomain();
    DWORD GetDomainId();

    bool Startup();
    bool Execute();
    bool Shutdown();

    void SetApplicationTypeName(const wchar_t* applicationTypeName);
    void SetApplicationAssembly(const wchar_t* applicationAssembly);
    void SetApplicationDirectoryPath(const wchar_t* applicationPath);
    void SetApplicationPackagesPath(const wchar_t* applicationPackagesPath); 
};

