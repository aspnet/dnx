// Defines the entry point for the console application.

#include "stdafx.h"
#include "klr.h"

void GetModuleDirectory(HMODULE module, LPWSTR szPath)
{
    DWORD dirLength = GetModuleFileName(module, szPath, MAX_PATH);
    for (dirLength--; dirLength >= 0 && szPath[dirLength] != '\\'; dirLength--);
    szPath[dirLength + 1] = '\0';
}

int CallFirmwareProcessMain(int argc, wchar_t* argv[])
{
    TCHAR szKreTrace[1];
    bool m_fVerboseTrace = GetEnvironmentVariableW(L"KRE_TRACE", szKreTrace, 1) > 0;
    bool fSuccess = true;
    HMODULE m_hHostModule = nullptr;
#if CORECLR
    LPCWSTR pwzHostModuleName = L"klr.core45.dll";
#else
    LPCWSTR pwzHostModuleName = L"klr.net45.dll";
#endif

    // Note: need to keep as ASCII as GetProcAddress function takes ASCII params
    LPCSTR pszCallApplicationMainName = "CallApplicationMain";
    FnCallApplicationMain pfnCallApplicationMain = nullptr;
    int exitCode = 0;

    TCHAR szCurrentDirectory[MAX_PATH];
    GetModuleDirectory(NULL, szCurrentDirectory);

    // Set the DEFAULT_LIB environment variable to be the same directory
    // as the exe
    SetEnvironmentVariable(L"DEFAULT_LIB", szCurrentDirectory);

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
        if (FreeLibrary(m_hHostModule))
        {
            fSuccess = true;
        }
        else
        {
            fSuccess = false;
        }
        
        m_hHostModule = nullptr;
    }

    return exitCode;
}

int wmain(int argc, wchar_t* argv[])
{
    return CallFirmwareProcessMain(argc, argv);
}
