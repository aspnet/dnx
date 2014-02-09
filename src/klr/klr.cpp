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


    CALL_APPLICATION_MAIN_DATA data = { 0 };
    data.argc = argc - 1;
    data.argv = const_cast<LPCWSTR*>(&argv[1]);

    auto stringsEqual = [](const wchar_t*  const a, const wchar_t*  const b) -> bool
    {
        return ::_wcsicmp(a, b) == 0;
    };

    bool processing = true;
    while (processing)
    {
        if (data.argc >= 2 && stringsEqual(data.argv[0], L"--appbase"))
        {
            data.applicationBase = data.argv[1];
            data.argc -= 2;
            data.argv += 2;
        }
        else if (data.argc >= 1 && stringsEqual(data.argv[0], L"--net45"))
        {
            pwzHostModuleName = L"klr.net45.dll";
            data.argc -= 1;
            data.argv += 1;
        }
        else if (data.argc >= 1 && stringsEqual(data.argv[0], L"--core45"))
        {
            pwzHostModuleName = L"klr.core45.dll";
            data.argc -= 1;
            data.argv += 1;

            // HACK
            SetEnvironmentVariable(L"TARGET_FRAMEWORK", L"k10");
        }
        else
        {
            processing = false;
        }
    }

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

    // Set the klr path path
    TCHAR szModulePath[MAX_PATH];
    DWORD dwModulePathSize = GetModuleFileName(NULL, szModulePath, MAX_PATH);
    szModulePath[dwModulePathSize] = '\0';
    ::SetEnvironmentVariable(L"KLR_PATH", szModulePath);

    HRESULT hr = pfnCallApplicationMain(&data);
    if (SUCCEEDED(hr))
    {
        fSuccess = true;
        exitCode = data.exitcode;
    }
    else
    {
        fSuccess = false;
        exitCode = hr;
    }

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
