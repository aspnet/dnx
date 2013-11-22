#include "stdafx.h"

ClrDomainInstance::ClrDomainInstance(void)
{
    m_pFirmware = nullptr;
    m_dwDomainId = 0;

    m_pHostStartup = nullptr;
    m_pHostMain = nullptr;
    m_pHostShutdown = nullptr;

    //Defaults
    ::wcscpy_s(m_wszManagedHostAssembly, L"");
    ::wcscpy_s(m_wszManagedHostType, L"");

    ::wcscpy_s(m_wszManagedHostMethodNameStartup,   L"");
    ::wcscpy_s(m_wszManagedHostMethodNameMain,      L"");
    ::wcscpy_s(m_wszManagedHostMethodNameShutdown,  L"");

    ::wcscpy_s(m_wszApplicationTypeName, L"");
    ::wcscpy_s(m_wszApplicationAssembly, L"");
    ::wcscpy_s(m_wszApplicationDirectoryPath, L"");

    ::wcscpy_s(m_wszApplicationDirectoryProbeNameBinDefault, L"bin");
    ::wcscpy_s(m_wszApplicationDirectoryProbeNamePackagesDefault, L"packages");
}

ClrDomainInstance::~ClrDomainInstance(void)
{
    Dispose();
}

bool ClrDomainInstance::Init(Firmware* pFirmware)
{
    bool fSuccess = true;

    if (pFirmware == nullptr)
    {
        fSuccess = false;
        goto Finished;
    }

    m_pFirmware = pFirmware;

    //Managed Host - obtain values from Firmware
    ::wcscpy_s(m_wszManagedHostAssembly, m_pFirmware->GetManagedHostAssemblyNameDefault());
    ::wcscpy_s(m_wszManagedHostType, m_pFirmware->GetManagedHostTypeDefault());

    ::wcscpy_s(m_wszManagedHostMethodNameStartup,   m_pFirmware->GetManagedHostMethodNameStartupDefault());
    ::wcscpy_s(m_wszManagedHostMethodNameMain,      m_pFirmware->GetManagedHostMethodNameMainDefault());
    ::wcscpy_s(m_wszManagedHostMethodNameShutdown,  m_pFirmware->GetManagedHostMethodNameShutdownDefault());

    //Application
    SetApplicationTypeName(m_pFirmware->GetApplicationTypeName());
    SetApplicationAssembly(m_pFirmware->GetApplicationAssembly());
    SetApplicationDirectoryPath(m_pFirmware->GetApplicationDirectoryPath());

Finished:

    return fSuccess;
}

bool ClrDomainInstance::Dispose()
{
    bool fSuccess = true;

    if (m_pHostStartup)
    {
        m_pHostStartup = nullptr;
    }

    if (m_pHostMain)
    {
        m_pHostMain = nullptr;
    }

    if (m_pHostShutdown)
    {
        m_pHostShutdown = nullptr;
    }

    return fSuccess;
}

bool ClrDomainInstance::CreateDomain()
{
    bool fSuccess = true;
    HRESULT hr = S_OK;
    DWORD dwDomainId = 0;
    int iExitCode = 0;
    HostStartupDelegate pHostStartup = nullptr;
    HostMainDelegate pHostMain = nullptr;
    HostShutdownDelegate pHostShutdown = nullptr;
    DWORD dwFlagsAppDomain = 0;
    std::wstring s_TrustedPlatformAssemblies(L"");
    wchar_t* pwszPackageAssembliesList = nullptr;
    std::wstring s_ApplicationPaths(L"");
    wchar_t m_wszDomainName[MAX_STR];
    wchar_t wszFilePathTemp[MAX_PATH];

    //-------------------------------------------------------------
    const wchar_t* property_keys[] = 
    { 
        // Allowed property names:
        // APPBASE
        // - The base path of the application from which the exe and other assemblies will be loaded
        L"APPBASE",
        //
        // TRUSTED_PLATFORM_ASSEMBLIES
        // - The list of complete paths to each of the fully trusted assemblies
        L"TRUSTED_PLATFORM_ASSEMBLIES",
        //
        // APP_PATHS
        // - The list of paths which will be probed by the assembly loader
        L"APP_PATHS",
        //
        // APP_NI_PATHS
        // - The list of additional paths that the assembly loader will probe for ngen images
        //
        // NATIVE_DLL_SEARCH_DIRECTORIES
        // - The list of paths that will be probed for native DLLs called by PInvoke
        //
    };

    //ValidateSettings();
    if (m_wszApplicationDirectoryPath[0] == L'\0')
    {
        //Determine from input m_wszApplicationAssembly
        fSuccess = m_pFirmware->GetPackageLoader()->UseLoadLibraryToGetFinalFilePath(
                        m_wszApplicationAssembly, 
                        wszFilePathTemp, _countof(wszFilePathTemp));
        if (fSuccess == false)
        {
            goto Finished;
        }

        wcscpy_s(m_wszApplicationDirectoryPath, wszFilePathTemp);
    }

    //-------------------------------------------------------------
    // Prepapre values in order to Create an AppDomain

    //##Future: rework when we no longer need to have everyting in TRUSTED_PLATFORM_ASSEMBLIES and can 
    // split into TRUSTED_PLATFORM_ASSEMBLIES and APP_PATHS

    //Compute Paths
    //Concat ManagedHost + CLR Engine + Application Packages from Manifest + Application

    //-------------------------------------------------------------
    //TRUSTED_PLATFORM_ASSEMBLIES

    //ManagedHost Assembly File List
    s_TrustedPlatformAssemblies += m_pFirmware->GetHostDirectoryPath();
    s_TrustedPlatformAssemblies += m_pFirmware->GetManagedHostAssemblyFileNameDefault();
    TRACE_ODS1(L"TPA.Host=%s", s_TrustedPlatformAssemblies.c_str());

    //ClrEngine Asembly File List
    s_TrustedPlatformAssemblies += m_pFirmware->GetPackageLoader()->GetClrEngineAssemblyList();

    //Application Packages from Manifest
    pwszPackageAssembliesList  = m_pFirmware->GetPackageLoader()->ParsePackageManifestFromDirectory(m_wszApplicationDirectoryPath);
    if (pwszPackageAssembliesList != nullptr)
        s_TrustedPlatformAssemblies += pwszPackageAssembliesList;

    //-------------------------------------------------------------
    //APP_PATHS
    s_ApplicationPaths += m_wszApplicationDirectoryPath;

    //Add application probing paths
    //##FUTURE - make configurable list of probing directories

    // "\bin"
    s_ApplicationPaths += L";"; 
    s_ApplicationPaths += m_wszApplicationDirectoryPath;
    s_ApplicationPaths += m_wszApplicationDirectoryProbeNameBinDefault;

    // "\packages"
    s_ApplicationPaths += L";"; 
    s_ApplicationPaths += m_wszApplicationDirectoryPath;
    s_ApplicationPaths += m_wszApplicationDirectoryProbeNamePackagesDefault;

    //-------------------------------------------------------------
    // Create an AppDomain
    const wchar_t* property_values[] = { 
        // APPBASE
        m_wszApplicationDirectoryPath,
        // TRUSTED_PLATFORM_ASSEMBLIES
        s_TrustedPlatformAssemblies.c_str(),
        // APP_PATHS
        s_ApplicationPaths.c_str()//m_wszApplicationDirectoryPath,  //managedExePath,
    };

    TRACE_ODS(L"Creating an AppDomain");
    //TT TRACE_ODS1(L"APPBASE=%s", property_values[0]);
    //TT TRACE_ODS1(L"TRUSTED_PLATFORM_ASSEMBLIES=%s", property_values[1]);
    TRACE_ODS1(L"APP_PATHS=%s", property_values[2]);

    //## Future - add unique suffix (GUID?) when multiple Domains in a process
    wcscpy_s(m_wszDomainName, _countof(m_wszDomainName), m_pFirmware->GetHostExeName());

    TRACE_ODS1(L"Domain Name=%s", m_wszDomainName);
    TRACE_ODS1(L"AppDomainManager Assembly=%s", m_wszManagedHostAssembly);
    TRACE_ODS1(L"AppDomainManager Type=%s", m_wszManagedHostType);

    // Flags:
    // APPDOMAIN_ENABLE_PLATFORM_SPECIFIC_APPS
    // - By default CoreCLR only allows platform neutral assembly to be run. To allow
    //   assemblies marked as platform specific, include this flag
    //
    // APPDOMAIN_ENABLE_PINVOKE_AND_CLASSIC_COMINTEROP
    // - Allows sandboxed applications to make P/Invoke calls and use COM interop
    //
    // APPDOMAIN_SECURITY_SANDBOXED
    // - Enables sandboxing. If not set, the app is considered full trust
    //
    // APPDOMAIN_IGNORE_UNHANDLED_EXCEPTION
    // - Prevents the application from being torn down if a managed exception is unhandled
    //
    dwFlagsAppDomain = APPDOMAIN_ENABLE_PLATFORM_SPECIFIC_APPS | 
                        APPDOMAIN_ENABLE_PINVOKE_AND_CLASSIC_COMINTEROP;

    //##Future - supplement flags from settings  and/or environment variables

    hr = m_pFirmware->GetClrHostRuntimeModule()->GetCLRRuntimeHost()->CreateAppDomainWithManager(
        m_wszDomainName,   // The friendly name of the AppDomain
        dwFlagsAppDomain,
        m_wszManagedHostAssembly,                // Name of the assembly that contains the AppDomainManager implementation
        m_wszManagedHostType,                    // The AppDomainManager implementation type name
        sizeof(property_keys)/sizeof(wchar_t*),  // The number of properties
        property_keys,
        property_values,
        &dwDomainId
        );

    if (FAILED(hr)) 
    {
        TRACE_ODS_HR(L"Failed call to CreateAppDomainWithManager", hr);
        fSuccess = false;
        goto Finished;
    }

    m_dwDomainId = dwDomainId;

    //-------------------------------------------------------------
    // Create delegates to make calls into managed code

    //Startup Delegate
    TRACE_ODS(L"Creating the startup delegate" );
    hr = m_pFirmware->GetClrHostRuntimeModule()->GetCLRRuntimeHost()->CreateDelegate(
        m_dwDomainId,                       // Domain ID
        m_wszManagedHostAssembly,           // Assembly name
        m_wszManagedHostType,               // Type
        m_wszManagedHostMethodNameStartup,         // Method
        (INT_PTR*)&pHostStartup);              // Delegate (returned)

    //It is OK if the delegate is not found. Continue
    if (FAILED(hr)) {
        TRACE_ODS_HR(L"Failed to create startup delegate", hr);
        //Reset error code
        hr = S_OK;
    }

    m_pHostStartup = pHostStartup;

    //Main Delegate
    TRACE_ODS(L"Creating the main delegate" );
    hr = m_pFirmware->GetClrHostRuntimeModule()->GetCLRRuntimeHost()->CreateDelegate(
        m_dwDomainId,                       // Domain ID
        m_wszManagedHostAssembly,           // Assembly name
        m_wszManagedHostType,               // Type
        m_wszManagedHostMethodNameMain,     // Method
        (INT_PTR*)&pHostMain);              // Delegate (returned)

    //It is OK if the delegate is not found. Continue
    if (FAILED(hr)) {
        TRACE_ODS_HR(L"Failed to create Main delegate", hr);
        //Reset error code
        hr = S_OK;
    }

    m_pHostMain = pHostMain;

    //Shutdown Delegate
    TRACE_ODS(L"Creating the Shutdown delegate" );
    hr = m_pFirmware->GetClrHostRuntimeModule()->GetCLRRuntimeHost()->CreateDelegate(
        m_dwDomainId,                       // Domain ID
        m_wszManagedHostAssembly,           // Assembly name
        m_wszManagedHostType,               // Type
        m_wszManagedHostMethodNameShutdown, // Method
        (INT_PTR*)&pHostShutdown);              // Delegate (returned)

    //It is OK if the delegate is not found. Continue
    if (FAILED(hr)) {
        TRACE_ODS_HR(L"Failed to create Shutdown delegate", hr);
        //Reset error code
        hr = S_OK;
    }

    m_pHostShutdown = pHostShutdown;

    //Startup Domain
    if (m_pHostStartup != nullptr)
    {
        fSuccess = Startup();
    }

Finished:
    if (fSuccess == false)
    {
        //Release resources
    }

    return fSuccess;
}

bool ClrDomainInstance::Startup()
{
    bool fSuccess = true;
    int exitCode = 0;
    int delegateSuccess = 0;

    //-------------------------------------------------------------
    // Call the delegate to start the application

    int argc = 0;
    wchar_t* * argv = nullptr;

    argc = m_pFirmware->GetApplicationArgc();
    argv = m_pFirmware->GetApplicationArgv();

    if (m_pHostStartup != nullptr)
    {
        TRACE_ODS(L"Calling the startup delegate" );

        exitCode = m_pHostStartup(
            argc, 
            (const wchar_t**)argv,
            m_pFirmware->GetHostPath(),
            m_wszApplicationTypeName,
            m_wszApplicationAssembly,
            m_pFirmware->GetVerboseTrace() ? 1 : 0, 
            m_pFirmware->GetWaitForDebugger() ? 1 : 0,
            &delegateSuccess
            );

        m_pFirmware->SetExitCode(exitCode);
    }

    return fSuccess;
}

bool ClrDomainInstance::Execute()
{
    bool fSuccess = true;
    int exitCode = 0;
    int delegateSuccess = 0;

    int argc = 0;
    wchar_t* * argv = nullptr;

    argc = m_pFirmware->GetApplicationArgc();
    argv = m_pFirmware->GetApplicationArgv();

    if (m_pHostMain != nullptr)
    {
        TRACE_ODS(L"Calling the main delegate" );

        exitCode = m_pHostMain(
            argc, 
            (const wchar_t**)argv,
            &delegateSuccess
            );

        m_pFirmware->SetExitCode(exitCode);
    }

    return fSuccess;
}

bool ClrDomainInstance::Shutdown()
{
    bool fSuccess = true;
    int exitCode = 0;
    int delegateSuccess = 0;

    if (m_pHostShutdown != nullptr)
    {
        TRACE_ODS(L"Calling the Shutdown delegate" );
        exitCode = m_pHostShutdown(
            &delegateSuccess
            );

        m_pFirmware->SetExitCode(exitCode);
    }

    fSuccess = UnloadDomain();
    if (fSuccess = false)
    {
        goto Finished;
    }

Finished:

    return fSuccess;
}

bool ClrDomainInstance::UnloadDomain()
{
    bool fSuccess = true;
    HRESULT hr = S_OK;

    TRACE_ODS1(L"Unloading the AppDomain: %d", m_dwDomainId);
    hr = m_pFirmware->GetClrHostRuntimeModule()->GetCLRRuntimeHost()->UnloadAppDomain(
        m_dwDomainId, 
        m_pFirmware->GetWaitForDomainShutdown());
    if (FAILED(hr)) 
    {
        TRACE_ODS_HR(L"Failed to unload the AppDomain", hr);
        fSuccess = false;
    }

    m_dwDomainId = 0;

    m_pHostStartup = nullptr;
    m_pHostMain = nullptr;
    m_pHostShutdown = nullptr;

    return fSuccess;
}

DWORD ClrDomainInstance::GetDomainId()
{
    return m_dwDomainId;
}

void ClrDomainInstance::SetApplicationTypeName(const wchar_t* applicationTypeName)
{
    size_t cchApplicationTypeName = ::wcslen(applicationTypeName);

    // Copy the directory path
    ::wcsncpy_s(m_wszApplicationTypeName, applicationTypeName, cchApplicationTypeName );
    m_wszApplicationTypeName[cchApplicationTypeName] = L'\0';
}

void ClrDomainInstance::SetApplicationAssembly(const wchar_t* applicationAssembly)
{
    size_t cchApplicationAssembly = ::wcslen(applicationAssembly);

    // Copy the directory path
    ::wcsncpy_s(m_wszApplicationAssembly, applicationAssembly, cchApplicationAssembly );
    m_wszApplicationAssembly[cchApplicationAssembly] = L'\0';
}

void ClrDomainInstance::SetApplicationDirectoryPath(const wchar_t* applicationPath) 
{
    size_t cchApplicationPath = ::wcslen(applicationPath);

    // Copy the directory path
    ::wcsncpy_s(m_wszApplicationDirectoryPath, applicationPath, cchApplicationPath );
    m_wszApplicationDirectoryPath[cchApplicationPath] = L'\0';
}

void ClrDomainInstance::SetApplicationPackagesPath(const wchar_t* applicationPackagesPath) 
{
    size_t cchApplicationPackagesPath = ::wcslen(applicationPackagesPath);

    // Copy the directory path
    ::wcsncpy_s(m_wszApplicationPackagesPath, applicationPackagesPath, cchApplicationPackagesPath );
    m_wszApplicationPackagesPath[cchApplicationPackagesPath] = L'\0';
}
