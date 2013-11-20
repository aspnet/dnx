#pragma once

class ClrHostRuntimeModule
{
private:
    Firmware* m_pFirmware;

    // Name of the CLR native runtime DLL
    wchar_t m_wszCLRDll[MAX_STR];

    // Path to the directory that CLR native runtime DLL is in
    wchar_t m_wszCLRDirectoryPath[MAX_PATH];

    // CLR Engine
    HMODULE m_hCLRModule;
    ICLRRuntimeHost2* m_pCLRRuntimeHost;

    //Map of dwDomainId to ClrHostDomainInstances created
    map<DWORD,VOID*> m_mapDomains;

    //Default Domain
    ClrDomainInstance* m_pClrDomainInstanceDefaultDomain;

public:
    ClrHostRuntimeModule(void);
    ~ClrHostRuntimeModule(void);

    bool Init(Firmware* pFirmware);
    bool Dispose();

    wchar_t* ClrHostRuntimeModule::GetCLRDirectoryPath();

    // Attempts to load CoreCLR.dll from the given directory.
    // On success pins the dll, returns the HMODULE.
    // On failure returns nullptr.
    HMODULE TryLoadCLRModule(const wchar_t* directoryPath);

    // Returns the ICLRRuntimeHost2 instance, loading it from CoreCLR.dll if necessary, or nullptr on failure.
    ICLRRuntimeHost2* GetCLRRuntimeHost();
    bool LoadCoreCLRModule();

    bool Start();
    bool Execute();
    bool Shutdown();
};
