// Defines the entry point for the console application.

#include "stdafx.h"
#include "dotnet.h"

void GetModuleDirectory(HMODULE module, LPWSTR szPath)
{
    DWORD dirLength = GetModuleFileName(module, szPath, MAX_PATH);
    for (dirLength--; dirLength >= 0 && szPath[dirLength] != '\\'; dirLength--);
    szPath[dirLength + 1] = L'\0';
}

int LastIndexOfChar(LPCWSTR const pszStr, WCHAR c)
{
    int nIndex = wcsnlen_s(pszStr, MAX_PATH) - 1;
    do
    {
        if (pszStr[nIndex] == c)
        {
            break;
        }
    } while (--nIndex >= 0);

    return nIndex;
}

bool StringsEqual(LPCWSTR const pszStrA, LPCWSTR const pszStrB)
{
    return ::_wcsnicmp(pszStrA, pszStrB, MAX_PATH) == 0;
}

bool StringEndsWith(LPCWSTR const pszStr, LPCWSTR const pszSuffix)
{
    int nStrLen = wcsnlen_s(pszStr, MAX_PATH);
    int nSuffixLen = wcsnlen_s(pszSuffix, MAX_PATH);
    int nOffset = nStrLen - nSuffixLen;

    if (nOffset < 0)
    {
        return false;
    }

    return StringsEqual(pszStr + nOffset, pszSuffix);
}

int LastPathSeparatorIndex(LPCWSTR const pszPath)
{
    int nLastSlashIndex = LastIndexOfChar(pszPath, L'/');
    int nLastBackSlashIndex = LastIndexOfChar(pszPath, L'\\');
    return max(nLastSlashIndex, nLastBackSlashIndex);
}

void GetParentDir(LPCWSTR const pszPath, LPWSTR const pszParentDir)
{
    int nLastSeparatorIndex = LastPathSeparatorIndex(pszPath);
    if (nLastSeparatorIndex < 0)
    {
        StringCchCopyW(pszParentDir, MAX_PATH, L".");
        return;
    }

    CopyMemory(pszParentDir, pszPath, (nLastSeparatorIndex + 1) * sizeof(WCHAR));
    pszParentDir[nLastSeparatorIndex + 1] = L'\0';
}

void GetFileName(LPCWSTR const pszPath, LPWSTR const pszFileName)
{
    int nLastSeparatorIndex = LastPathSeparatorIndex(pszPath);

    if (nLastSeparatorIndex < 0)
    {
        StringCchCopyW(pszFileName, MAX_PATH, pszPath);
        return;
    }

    StringCchCopyW(pszFileName, MAX_PATH, pszPath + nLastSeparatorIndex + 1);
}

int DotnetOptionValueNum(LPCWSTR pszCandidate)
{
    if (StringsEqual(pszCandidate, L"--appbase") ||
        StringsEqual(pszCandidate, L"--lib") ||
        StringsEqual(pszCandidate, L"--packages") ||
        StringsEqual(pszCandidate, L"--configuration") ||
        StringsEqual(pszCandidate, L"--port"))
    {
        return 1;
    }
    else if (StringsEqual(pszCandidate, L"--watch") ||
        StringsEqual(pszCandidate, L"--help") ||
        StringsEqual(pszCandidate, L"-h") ||
        StringsEqual(pszCandidate, L"-?") ||
        StringsEqual(pszCandidate, L"--version"))
    {
        return 0;
    }

    // It isn't a dotnet option
    return -1;
}

void FreeExpandedCommandLineArguments(int nArgc, LPWSTR* ppszArgv)
{
    for (int i = 0; i < nArgc; ++i)
    {
        delete[] ppszArgv[i];
    }
    delete[] ppszArgv;
}

bool ExpandCommandLineArguments(int nArgc, LPWSTR* ppszArgv, int& nExpandedArgc, LPWSTR*& ppszExpandedArgv)
{
    if (nArgc == 0)
    {
        return false;
    }

    for (int i = 0; i < nArgc; ++i)
    {
        // If '--appbase' is already given and it has a value
        if (StringsEqual(ppszArgv[i], L"--appbase") && (i < nArgc - 1))
        {
            return false;
        }
    }

    nExpandedArgc = nArgc + 2;
    ppszExpandedArgv = new LPWSTR[nExpandedArgc];
    memset(ppszExpandedArgv, 0, nExpandedArgc*sizeof(LPWSTR));
    WCHAR szParentDir[MAX_PATH];

    // Copy all arguments (options & values) as is before the project.json/assembly path
    int nPathArgIndex = -1;
    int nOptValNum;
    while (++nPathArgIndex < nArgc)
    {
        nOptValNum = DotnetOptionValueNum(ppszArgv[nPathArgIndex]);

        // It isn't a dotnet option, we treat it as the project.json/assembly path
        if (nOptValNum < 0)
        {
            break;
        }

        // Copy the option
        ppszExpandedArgv[nPathArgIndex] = new WCHAR[MAX_PATH];
        StringCchCopyW(ppszExpandedArgv[nPathArgIndex], MAX_PATH, ppszArgv[nPathArgIndex]);

        // Copy the value if the option has one
        if (nOptValNum > 0 && (++nPathArgIndex < nArgc))
        {
            ppszExpandedArgv[nPathArgIndex] = new WCHAR[MAX_PATH];
            StringCchCopyW(ppszExpandedArgv[nPathArgIndex], MAX_PATH, ppszArgv[nPathArgIndex]);
        }
    }

    // No path argument was found, no expansion is needed
    if (nPathArgIndex >= nArgc)
    {
        FreeExpandedCommandLineArguments(nExpandedArgc, ppszExpandedArgv);
        return false;
    }

    // Allocate memory before doing expansion
    for (int i = nPathArgIndex; i < nExpandedArgc; ++i)
    {
        ppszExpandedArgv[i] = new WCHAR[MAX_PATH];
    }

    // "dotnet /path/App.dll arg1" --> "dotnet --appbase /path/ /path/App.dll arg1"
    // "dotnet /path/App.exe arg1" --> "dotnet --appbase /path/ /path/App.exe arg1"
    LPWSTR pszPathArg = ppszArgv[nPathArgIndex];
    if (StringEndsWith(pszPathArg, L".exe") || StringEndsWith(pszPathArg, L".dll"))
    {
        GetParentDir(pszPathArg, szParentDir);

        StringCchCopyW(ppszExpandedArgv[nPathArgIndex], MAX_PATH, L"--appbase");
        StringCchCopyW(ppszExpandedArgv[nPathArgIndex + 1], MAX_PATH, szParentDir);

        // Copy all arguments/options as is
        for (int i = nPathArgIndex; i < nArgc; ++i)
        {
            StringCchCopyW(ppszExpandedArgv[i + 2], MAX_PATH, ppszArgv[i]);
        }

        return true;
    }

    // "dotnet /path/project.json run" --> "dotnet --appbase /path/ Microsoft.Framework.ApplicationHost run"
    // "dotnet /path/ run" --> "dotnet --appbase /path/ Microsoft.Framework.ApplicationHost run"
    WCHAR szFileName[MAX_PATH];
    GetFileName(pszPathArg, szFileName);
    if (StringsEqual(szFileName, L"project.json"))
    {
        GetParentDir(pszPathArg, szParentDir);
    }
    else
    {
        StringCchCopyW(szParentDir, MAX_PATH, pszPathArg);
    }

    StringCchCopyW(ppszExpandedArgv[nPathArgIndex], MAX_PATH, L"--appbase");
    StringCchCopyW(ppszExpandedArgv[nPathArgIndex + 1], MAX_PATH, szParentDir);
    StringCchCopyW(ppszExpandedArgv[nPathArgIndex + 2], MAX_PATH, L"Microsoft.Framework.ApplicationHost");

    for (int i = nPathArgIndex + 1; i < nArgc; ++i)
    {
        // Copy all other arguments/options as is
        StringCchCopyW(ppszExpandedArgv[i + 2], MAX_PATH, ppszArgv[i]);
    }

    return true;
}

int CallFirmwareProcessMain(int argc, wchar_t* argv[])
{
    TCHAR szDotnetTrace[2];
    // TODO: remove KRE_ env var
    DWORD nEnvTraceSize = GetEnvironmentVariableW(L"DOTNET_TRACE", szDotnetTrace, 2);
    if (nEnvTraceSize == 0)
    {
        nEnvTraceSize = GetEnvironmentVariableW(L"KRE_TRACE", szDotnetTrace, 2);
    }
    bool m_fVerboseTrace = (nEnvTraceSize == 1);
    if (m_fVerboseTrace)
    {
        szDotnetTrace[1] = L'\0';
        m_fVerboseTrace = (_wcsnicmp(szDotnetTrace, L"1", 1) == 0);
    }

    bool fSuccess = true;
    HMODULE m_hHostModule = nullptr;
#if CORECLR
    LPCWSTR pwzHostModuleName = L"dotnet.core45.dll";
#else
    LPCWSTR pwzHostModuleName = L"dotnet.net45.dll";
#endif

    // Note: need to keep as ASCII as GetProcAddress function takes ASCII params
    LPCSTR pszCallApplicationMainName = "CallApplicationMain";
    FnCallApplicationMain pfnCallApplicationMain = nullptr;
    int exitCode = 0;

    TCHAR szCurrentDirectory[MAX_PATH];
    GetModuleDirectory(NULL, szCurrentDirectory);

    // Set the DEFAULT_LIB environment variable to be the same directory
    // as the exe
    SetEnvironmentVariable(L"DOTNET_DEFAULT_LIB", szCurrentDirectory);

    // Set the DOTNET_CONOSLE_HOST flag which will print exceptions
    // to stderr instead of throwing
    SetEnvironmentVariable(L"DOTNET_CONSOLE_HOST", L"1");

    CALL_APPLICATION_MAIN_DATA data = { 0 };
    int nExpandedArgc = -1;
    LPWSTR* ppszExpandedArgv = nullptr;
    bool bExpanded = ExpandCommandLineArguments(argc - 1, &(argv[1]), nExpandedArgc, ppszExpandedArgv);
    if (bExpanded)
    {
        data.argc = nExpandedArgc;
        data.argv = const_cast<LPCWSTR*>(ppszExpandedArgv);
    }
    else
    {
        data.argc = argc - 1;
        data.argv = const_cast<LPCWSTR*>(&argv[1]);
    }

    // Get application base from DOTNET_APPBASE environment variable
    // Note: this value can be overriden by --appbase option
    TCHAR szAppBase[MAX_PATH];
    // TODO: remove KRE_ env var
    DWORD dwAppBase = GetEnvironmentVariableW(L"DOTNET_APPBASE", szAppBase, MAX_PATH);
    if (dwAppBase == 0)
    {
        dwAppBase = GetEnvironmentVariableW(L"KRE_APPBASE", szAppBase, MAX_PATH);
    }
    if (dwAppBase != 0)
    {
        data.applicationBase = szAppBase;
    }

    for (int i = 0; i < data.argc; ++i)
    {
        if ((i < data.argc - 1) && StringsEqual(data.argv[i], L"--appbase"))
        {
            data.applicationBase = data.argv[i + 1];
        }
    }

    if (!data.applicationBase)
    {
        data.applicationBase = szCurrentDirectory;
    }

    m_hHostModule = ::LoadLibraryExW(pwzHostModuleName, NULL, LOAD_LIBRARY_SEARCH_DEFAULT_DIRS);
    if (!m_hHostModule)
    {
        if (m_fVerboseTrace)
            ::wprintf_s(L"Failed to load: %S\r\n", pwzHostModuleName);
        m_hHostModule = nullptr;
        goto Finished;
    }
    if (m_fVerboseTrace)
        ::wprintf_s(L"Loaded Module: %S\r\n", pwzHostModuleName);

    pfnCallApplicationMain = (FnCallApplicationMain)::GetProcAddress(m_hHostModule, pszCallApplicationMainName);
    if (!pfnCallApplicationMain)
    {
        if (m_fVerboseTrace)
            ::wprintf_s(L"Failed to find function %s in %S\n", pszCallApplicationMainName, pwzHostModuleName);
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

    if (bExpanded)
    {
        FreeExpandedCommandLineArguments(nExpandedArgc, ppszExpandedArgv);
    }

    return exitCode;
}

int wmain(int argc, wchar_t* argv[])
{
    return CallFirmwareProcessMain(argc, argv);
}
