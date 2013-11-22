// Defines the entry point for the console application.

#include "stdafx.h"
#include "klr.h"

int CallFirmwareProcessMain(int argc, wchar_t* argv[])
{
    bool fSuccess = true;
    bool m_fVerboseTrace = true;
    HMODULE m_hHostModule = nullptr;
    LPCWSTR pwzHostModuleName = L"klr.net45.dll";
    //Note: need to keep as ASCII as GetProcAddress function takes ASCII params
    LPCSTR pszCallApplicationMainName = "CallApplicationMain"; 
    FnCallApplicationMain pfnCallApplicationMain = nullptr;
    int exitCode = 0;

    m_hHostModule = ::LoadLibraryExW(pwzHostModuleName, NULL, LOAD_LIBRARY_SEARCH_DEFAULT_DIRS);
    if (!m_hHostModule) 
    {
        if (m_fVerboseTrace)
            ::wprintf_s(L"Failed to load: %s\r\n", pwzHostModuleName);
        m_hHostModule = nullptr;
        goto Finished;
    }
    if (m_fVerboseTrace)
        ::wprintf_s(L"Loaded Module: %s\r\n", pwzHostModuleName);

    pfnCallApplicationMain = (FnCallApplicationMain)::GetProcAddress(m_hHostModule, pszCallApplicationMainName);
    if (!pfnCallApplicationMain) 
    {
        if (m_fVerboseTrace)
            ::wprintf_s(L"Failed to find function %S in %s\n", pszCallApplicationMainName, pwzHostModuleName);
        fSuccess = false;
        goto Finished;
    }
    if (m_fVerboseTrace)
        printf_s("Found DLL Export: %s\r\n", pszCallApplicationMainName);

    fSuccess = pfnCallApplicationMain(argc, (const wchar_t**)argv, exitCode);

Finished:
    if (pfnCallApplicationMain) 
    {
        pfnCallApplicationMain = nullptr;
    }

    if (m_hHostModule)
    {
        fSuccess = FreeLibrary(m_hHostModule);
        m_hHostModule = nullptr;
    }

    return exitCode;
}

int wmain(int argc, wchar_t* argv[])
{
    return CallFirmwareProcessMain(argc, argv);
}
