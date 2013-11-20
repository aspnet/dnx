#include "stdafx.h"
#include "ClrHostRuntimeModule.h"

ClrHostRuntimeModule::ClrHostRuntimeModule(void)
{
    m_hCLRModule = nullptr;
    m_pCLRRuntimeHost = nullptr;
    m_pClrDomainInstanceDefaultDomain = nullptr;

    wcscpy_s(m_wszCLRDll, _countof(m_wszCLRDll), L"");
}

ClrHostRuntimeModule::~ClrHostRuntimeModule(void)
{
    Dispose();
}

bool ClrHostRuntimeModule::Init(Firmware* pFirmware)
{
    bool fSuccess = true;

    if (pFirmware == nullptr)
    {
        goto Finished;
    }

    m_pFirmware = pFirmware;

    wcscpy_s(m_wszCLRDll, m_pFirmware->GetCLRModuleName());

Finished:
    return fSuccess;
}

bool ClrHostRuntimeModule::Dispose()
{
    bool fSuccess = true;

    if(m_hCLRModule) 
    {
        // Free the module. This is done for completeness, but in fact CoreCLR.dll 
        // was pinned earlier so this call won't actually free it. The pinning is
        // done because CoreCLR does not support unloading.
        ::FreeLibrary(m_hCLRModule);
        m_hCLRModule = NULL;
    }

    return fSuccess;
}

wchar_t* ClrHostRuntimeModule::GetCLRDirectoryPath()
{
    return m_wszCLRDirectoryPath;
}

// Attempts to load CoreCLR.dll from the given directory.
// On success pins the dll, returns the HMODULE.
// On failure returns nullptr.
HMODULE ClrHostRuntimeModule::TryLoadCLRModule(const wchar_t* directoryPath) 
{
    HMODULE hCoreCLRModule = 0;
    HMODULE dummy_coreCLRModule = 0;
    wchar_t coreCLRLoadedPath[MAX_PATH] = {};
    std::wstring coreCLRPath(directoryPath);

    TRACE_ODS1(L"Attempting to load: %s", coreCLRPath.c_str());
    hCoreCLRModule = ::LoadLibraryExW(coreCLRPath.c_str(), NULL, 0);
    if (!hCoreCLRModule) 
    {
        TRACE_ODS1(L"Failed to load: %s", coreCLRPath.c_str());
        hCoreCLRModule = nullptr;
        goto Finished;
    }

    // Pin the module - CoreCLR.dll does not support being unloaded.
    if (!::GetModuleHandleExW(GET_MODULE_HANDLE_EX_FLAG_PIN, coreCLRPath.c_str(), &dummy_coreCLRModule)) 
    {
        TRACE_ODS1(L"Failed to pin: %s", coreCLRPath.c_str());
        hCoreCLRModule = nullptr;
        goto Finished;
    }

    m_hCLRModule = hCoreCLRModule;

    ::GetModuleFileNameW(hCoreCLRModule, coreCLRLoadedPath, MAX_PATH);
    wcscpy_s(m_wszCLRDirectoryPath, _countof(m_wszCLRDirectoryPath), coreCLRLoadedPath);

    TRACE_ODS1(L"Loaded Module: %s", coreCLRLoadedPath);

Finished:
    return hCoreCLRModule;
}

// Returns the ICLRRuntimeHost2 instance, loading it from CoreCLR.dll if necessary, or nullptr on failure.
ICLRRuntimeHost2* ClrHostRuntimeModule::GetCLRRuntimeHost() 
{
    HRESULT hr = S_OK;
    FnGetCLRRuntimeHost pfnGetCLRRuntimeHost = nullptr;
    ICLRRuntimeHost2* pCLRRuntimeHost = nullptr;

    //Have precomputed value?
    if (m_pCLRRuntimeHost != nullptr) 
    {
        goto Finished;
    }

    if (!m_hCLRModule)
    {
        TRACE_ODS1(L"Unable to load %d. HMODULE is invalid or NULL", m_hCLRModule);
        m_pCLRRuntimeHost = nullptr;
        goto Finished;
    }

    TRACE_ODS(L"Finding GetCLRRuntimeHost(...)");
    pfnGetCLRRuntimeHost = (FnGetCLRRuntimeHost)::GetProcAddress(m_hCLRModule, "GetCLRRuntimeHost");
    if (!pfnGetCLRRuntimeHost) 
    {
        TRACE_ODS1(L"Failed to find function GetCLRRuntimeHost in %s", m_wszCLRDll);
        m_pCLRRuntimeHost = nullptr;
        goto Finished;
    }

    TRACE_ODS(L"Calling GetCLRRuntimeHost(...)");
    hr = pfnGetCLRRuntimeHost(IID_ICLRRuntimeHost2, (IUnknown**)&pCLRRuntimeHost);
    if (FAILED(hr)) 
    {
        TRACE_ODS_HR(L"Failed to get ICLRRuntimeHost2 interface", hr);
        m_pCLRRuntimeHost = nullptr;
        goto Finished;
    }

    m_pCLRRuntimeHost = pCLRRuntimeHost;

Finished:

    return m_pCLRRuntimeHost;
}

bool ClrHostRuntimeModule::LoadCoreCLRModule()
{
    bool fSuccess = true;
    wchar_t wszCLRModulePath[MAX_PATH];
    DWORD modulePathLength = 0;
    int lastBackslashIndex = 0;

    if (!m_hCLRModule) 
    {
        fSuccess = m_pFirmware->GetPackageLoader()->ProbeForFileInPackages(
                        g_wszKitePackagesCoreCLRPackageName, 
                        m_wszCLRDll, 
                        wszCLRModulePath,
                        _countof(wszCLRModulePath)
                        );
        if (fSuccess)
        {
            m_hCLRModule = TryLoadCLRModule(wszCLRModulePath);
            if (m_hCLRModule) 
            {
                wcscpy_s(g_wszCoreCLRPackageDirectoryName,
                    _countof(g_wszCoreCLRPackageDirectoryName),
                    wszCLRModulePath
                    );
            }
        }
    }

    if (m_hCLRModule == nullptr) 
    {
        TRACE_ODS1(L"Unable to load %s", m_wszCLRDll);
        fSuccess = false;
        goto Finished;
    }

    // Sets m_wszCLRDirectoryPath
    // Save the directory that CoreCLR was found in
    modulePathLength = ::GetModuleFileNameW(m_hCLRModule, m_wszCLRDirectoryPath, MAX_PATH);
    // Search for the last backslash and terminate it there to keep just the directory path with trailing slash
    for (lastBackslashIndex = modulePathLength-1; lastBackslashIndex >= 0; lastBackslashIndex--) 
    {
        if (m_wszCLRDirectoryPath[lastBackslashIndex] == L'\\') 
        {
            m_wszCLRDirectoryPath[lastBackslashIndex + 1] = L'\0';
            break;
        }
    }

Finished:

    return fSuccess;
}

bool ClrHostRuntimeModule::Start()
{
    bool fSuccess = true;
    HRESULT hr = S_OK;
    ICLRRuntimeHost2* pHostICLRRuntimeHost2 = nullptr;
    STARTUP_FLAGS dwStartupFlags;
    ClrDomainInstance* pClrDomainInstance = nullptr;

    fSuccess = LoadCoreCLRModule();
    if (!fSuccess) 
    {
        goto Finished;
    }

    // Start the CoreCLR
    pHostICLRRuntimeHost2 = GetCLRRuntimeHost();
    if (!pHostICLRRuntimeHost2) 
    {
        fSuccess = false;
        goto Finished;
    }

    //##Future extract startup flags so can supplement from k.ini or enivironment variables

    // Default startup flags
    dwStartupFlags = (STARTUP_FLAGS)(
        STARTUP_FLAGS::STARTUP_LOADER_OPTIMIZATION_SINGLE_DOMAIN | 
        STARTUP_FLAGS::STARTUP_SINGLE_APPDOMAIN
        );
    TRACE_ODS1(L"Setting ICLRRuntimeHost2 startup flags 0x%x", dwStartupFlags);

    hr = pHostICLRRuntimeHost2->SetStartupFlags(dwStartupFlags); 
    if (FAILED(hr)) 
    {
        fSuccess = false;
        TRACE_ODS_HR(L"Failed to set startup flagss", hr);
        goto Finished;
    }

    TRACE_ODS(L"Authenticating ICLRRuntimeHost2" );
    // Authenticate with either CORECLR_HOST_AUTHENTICATION_KEY or CORECLR_HOST_AUTHENTICATION_KEY_NONGEN  
    hr = pHostICLRRuntimeHost2->Authenticate(CORECLR_HOST_AUTHENTICATION_KEY); 
    if (FAILED(hr)) 
    {
        fSuccess = false;
        TRACE_ODS_HR(L"Failed autenticate", hr);
        goto Finished;
    }

    TRACE_ODS(L"Starting ICLRRuntimeHost2");

    hr = pHostICLRRuntimeHost2->Start();
    if (FAILED(hr)) 
    {
        fSuccess = false;
        TRACE_ODS_HR(L"Failed to start CoreCLR. " , hr);
        goto Finished;
    }

    pClrDomainInstance = new ClrDomainInstance();
    if (pClrDomainInstance == nullptr)
    {
        fSuccess = false;
        goto Finished;
    }

    fSuccess = pClrDomainInstance->Init(m_pFirmware);
    if (fSuccess == false)
    {
        goto Finished;
    }

    fSuccess = pClrDomainInstance->CreateDomain();
    if (fSuccess == false)
    {
        goto Finished;
    }

    //one and only one default domain
    if (m_pClrDomainInstanceDefaultDomain == nullptr)
        m_pClrDomainInstanceDefaultDomain = pClrDomainInstance;

    //add to map of DWORDto pointer to ClrDomainInstance
    m_mapDomains.insert(std::pair<DWORD,VOID*>(pClrDomainInstance->GetDomainId(),(VOID*)pClrDomainInstance));

Finished:        
    if (!fSuccess)
    {
        //##Future Release
    }

    return fSuccess;
}

bool ClrHostRuntimeModule::Execute()
{
    return m_pClrDomainInstanceDefaultDomain->Execute();
}

bool ClrHostRuntimeModule::Shutdown()
{
    bool fSuccess = true;
    HRESULT hr = S_OK;
    ICLRRuntimeHost2* pHostICLRRuntimeHost2 = nullptr;

    //##Future
    //Loop through m_mapDomains and shutting them down

    //Shutdown Domain and hence Application
    fSuccess = m_pClrDomainInstanceDefaultDomain->Shutdown();
    if (!fSuccess)
    {
        //##Future actions
    }

    //-------------------------------------------------------------
    // Stop the CLR Engine Host
    TRACE_ODS(L"Stopping the host" );

    hr = GetCLRRuntimeHost()->Stop();
    if (FAILED(hr)) 
    {
        TRACE_ODS_HR(L"Failed to stop the host", hr);
        fSuccess = false;
        goto Finished;
    }

    // Release the reference to the host
    TRACE_ODS(L"Releasing ICLRRuntimeHost2" );

    if (pHostICLRRuntimeHost2)
    {
        pHostICLRRuntimeHost2->Release();
        pHostICLRRuntimeHost2 = nullptr;
    }

Finished:

    return fSuccess;
}

