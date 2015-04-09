// Copyright (c) Microsoft Open Technologies, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

#include "stdafx.h"
#include "dnx.h"
#include "pal.h"

bool LastIndexOfCharInPath(LPCTSTR const pszStr, TCHAR c, size_t* pIndex)
{
    size_t nIndex = _tcsnlen(pszStr, MAX_PATH) - 1;
    for (; nIndex != 0; nIndex--)
    {
        if (pszStr[nIndex] == c)
        {
            break;
        }
    }

    *pIndex = nIndex;
    return pszStr[nIndex] == c;
}

bool StringsEqual(LPCTSTR const pszStrA, LPCTSTR const pszStrB)
{
    return ::_tcsicmp(pszStrA, pszStrB) == 0;
}

bool PathEndsWith(LPCTSTR const pszStr, LPCTSTR const pszSuffix)
{
    size_t nStrLen = _tcsnlen(pszStr, MAX_PATH);
    size_t nSuffixLen = _tcsnlen(pszSuffix, MAX_PATH);

    if (nSuffixLen > nStrLen)
    {
        return false;
    }

    size_t nOffset = nStrLen - nSuffixLen;

    return ::_tcsnicmp(pszStr + nOffset, pszSuffix, MAX_PATH - nOffset) == 0;
}

bool LastPathSeparatorIndex(LPCTSTR const pszPath, size_t* pIndex)
{
    size_t nLastSlashIndex;
    size_t nLastBackSlashIndex;

    bool hasLastSlashIndex = LastIndexOfCharInPath(pszPath, _T('/'), &nLastSlashIndex);
    bool hasLastBackSlashIndex = LastIndexOfCharInPath(pszPath, _T('\\'), &nLastBackSlashIndex);

    if (hasLastSlashIndex && hasLastBackSlashIndex)
    {
        *pIndex = max(nLastSlashIndex, nLastBackSlashIndex);
        return true;
    }

    if (!hasLastSlashIndex && !hasLastBackSlashIndex)
    {
        return false;
    }

    *pIndex = hasLastSlashIndex ? nLastSlashIndex : nLastBackSlashIndex;
    return true;
}

void GetParentDir(LPCTSTR const pszPath, LPTSTR const pszParentDir)
{
    size_t nLastSeparatorIndex;
    if (!LastPathSeparatorIndex(pszPath, &nLastSeparatorIndex))
    {
        _tcscpy_s(pszParentDir, MAX_PATH, _T("."));
        return;
    }

    memcpy(pszParentDir, pszPath, (nLastSeparatorIndex + 1) * sizeof(TCHAR));
    pszParentDir[nLastSeparatorIndex + 1] = _T('\0');
}

void GetFileName(LPCTSTR const pszPath, LPTSTR const pszFileName)
{
    size_t nLastSeparatorIndex;

    if (!LastPathSeparatorIndex(pszPath, &nLastSeparatorIndex))
    {
        _tcscpy_s(pszFileName, MAX_PATH, pszPath);
        return;
    }

    _tcscpy_s(pszFileName, MAX_PATH, pszPath + nLastSeparatorIndex + 1);
}

int BootstrapperOptionValueNum(LPCTSTR pszCandidate)
{
    if (StringsEqual(pszCandidate, _T("--appbase")) ||
        StringsEqual(pszCandidate, _T("--lib")) ||
        StringsEqual(pszCandidate, _T("--packages")) ||
        StringsEqual(pszCandidate, _T("--configuration")) ||
        StringsEqual(pszCandidate, _T("--port")))
    {
        return 1;
    }
    else if (StringsEqual(pszCandidate, _T("--watch")) ||
        StringsEqual(pszCandidate, _T("--debug")) ||
        StringsEqual(pszCandidate, _T("--help")) ||
        StringsEqual(pszCandidate, _T("-h")) ||
        StringsEqual(pszCandidate, _T("-?")) ||
        StringsEqual(pszCandidate, _T("--version")))
    {
        return 0;
    }

    // It isn't a bootstrapper option
    return -1;
}

void FreeExpandedCommandLineArguments(int nArgc, LPTSTR* ppszArgv)
{
    for (int i = 0; i < nArgc; ++i)
    {
        delete[] ppszArgv[i];
    }
    delete[] ppszArgv;
}

bool ExpandCommandLineArguments(int nArgc, LPTSTR* ppszArgv, int& nExpandedArgc, LPTSTR*& ppszExpandedArgv)
{
    if (nArgc == 0)
    {
        return false;
    }

    for (int i = 0; i < nArgc; ++i)
    {
        // If '--appbase' is already given and it has a value
        if (StringsEqual(ppszArgv[i], _T("--appbase")) && (i < nArgc - 1))
        {
            return false;
        }
    }

    nExpandedArgc = nArgc + 2;
    ppszExpandedArgv = new LPTSTR[nExpandedArgc];
    memset(ppszExpandedArgv, 0, nExpandedArgc*sizeof(LPTSTR));
    TCHAR szParentDir[MAX_PATH];

    // Copy all arguments (options & values) as is before the project.json/assembly path
    int nPathArgIndex = -1;
    int nOptValNum;
    while (++nPathArgIndex < nArgc)
    {
        nOptValNum = BootstrapperOptionValueNum(ppszArgv[nPathArgIndex]);

        // It isn't a bootstrapper option, we treat it as the project.json/assembly path
        if (nOptValNum < 0)
        {
            break;
        }

        // Copy the option
        ppszExpandedArgv[nPathArgIndex] = new TCHAR[MAX_PATH];
        _tcscpy_s(ppszExpandedArgv[nPathArgIndex], MAX_PATH, ppszArgv[nPathArgIndex]);

        // Copy the value if the option has one
        if (nOptValNum > 0 && (++nPathArgIndex < nArgc))
        {
            ppszExpandedArgv[nPathArgIndex] = new TCHAR[MAX_PATH];
            _tcscpy_s(ppszExpandedArgv[nPathArgIndex], MAX_PATH, ppszArgv[nPathArgIndex]);
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
        ppszExpandedArgv[i] = new TCHAR[MAX_PATH];
    }

    // "dnx /path/App.dll arg1" --> "dnx --appbase /path/ /path/App.dll arg1"
    // "dnx /path/App.exe arg1" --> "dnx --appbase /path/ /path/App.exe arg1"
    LPTSTR pszPathArg = ppszArgv[nPathArgIndex];
    if (PathEndsWith(pszPathArg, _T(".exe")) || PathEndsWith(pszPathArg, _T(".dll")))
    {
        GetParentDir(pszPathArg, szParentDir);

        _tcscpy_s(ppszExpandedArgv[nPathArgIndex], MAX_PATH, _T("--appbase"));
        _tcscpy_s(ppszExpandedArgv[nPathArgIndex + 1], MAX_PATH, szParentDir);

        // Copy all arguments/options as is
        for (int i = nPathArgIndex; i < nArgc; ++i)
        {
            _tcscpy_s(ppszExpandedArgv[i + 2], MAX_PATH, ppszArgv[i]);
        }

        return true;
    }

    // "dnx /path/project.json run" --> "dnx --appbase /path/ Microsoft.Framework.ApplicationHost run"
    // "dnx /path/ run" --> "dnx --appbase /path/ Microsoft.Framework.ApplicationHost run"
    TCHAR szFileName[MAX_PATH];
    GetFileName(pszPathArg, szFileName);
    if (StringsEqual(szFileName, _T("project.json")))
    {
        GetParentDir(pszPathArg, szParentDir);
    }
    else
    {
        _tcscpy_s(szParentDir, MAX_PATH, pszPathArg);
    }

    _tcscpy_s(ppszExpandedArgv[nPathArgIndex], MAX_PATH, _T("--appbase"));
    _tcscpy_s(ppszExpandedArgv[nPathArgIndex + 1], MAX_PATH, szParentDir);
    _tcscpy_s(ppszExpandedArgv[nPathArgIndex + 2], MAX_PATH, _T("Microsoft.Framework.ApplicationHost"));

    for (int i = nPathArgIndex + 1; i < nArgc; ++i)
    {
        // Copy all other arguments/options as is
        _tcscpy_s(ppszExpandedArgv[i + 2], MAX_PATH, ppszArgv[i]);
    }

    return true;
}

int CallApplicationProcessMain(int argc, LPTSTR argv[])
{
    HRESULT hr = S_OK;

    bool m_fVerboseTrace = IsTracingEnabled();

    bool fSuccess = true;
    HMODULE m_hHostModule = nullptr;
#if CORECLR_WIN
    LPCTSTR pwzHostModuleName = _T("dnx.coreclr.dll");
#elif CORECLR_DARWIN
    LPCTSTR pwzHostModuleName = _T("dnx.coreclr.dylib");
#elif CORECLR_LINUX
    LPCTSTR pwzHostModuleName = _T("dnx.coreclr.so");
#else
    LPCTSTR pwzHostModuleName = _T("dnx.clr.dll");
#endif

    // Note: need to keep as ASCII as GetProcAddress function takes ASCII params
    LPCSTR pszCallApplicationMainName = "CallApplicationMain";
    FnCallApplicationMain pfnCallApplicationMain = nullptr;
    int exitCode = 0;

    LPTSTR szCurrentDirectory = GetNativeBootstrapperDirectory();

    // Set the DEFAULT_LIB environment variable to be the same directory
    // as the exe
    SetEnvironmentVariable(_T("DNX_DEFAULT_LIB"), szCurrentDirectory);

    // Set the DNX_CONOSLE_HOST flag which will print exceptions
    // to stderr instead of throwing
    SetConsoleHost();

    CALL_APPLICATION_MAIN_DATA data = { 0 };
    int nExpandedArgc = -1;
    LPTSTR* ppszExpandedArgv = nullptr;
    bool bExpanded = ExpandCommandLineArguments(argc - 1, &(argv[1]), nExpandedArgc, ppszExpandedArgv);
    if (bExpanded)
    {
        data.argc = nExpandedArgc;
        data.argv = const_cast<LPCTSTR*>(ppszExpandedArgv);
    }
    else
    {
        data.argc = argc - 1;
        data.argv = const_cast<LPCTSTR*>(&argv[1]);
    }

    // Get application base from DNX_APPBASE environment variable
    // Note: this value can be overriden by --appbase option
    TCHAR szAppBase[MAX_PATH];
    if (GetAppBasePathFromEnvironment(szAppBase))
    {
        data.applicationBase = szAppBase;
    }

    for (int i = 0; i < data.argc; ++i)
    {
        if ((i < data.argc - 1) && StringsEqual(data.argv[i], _T("--appbase")))
        {
            data.applicationBase = data.argv[i + 1];
        }
    }

    if (!data.applicationBase)
    {
        data.applicationBase = szCurrentDirectory;
    }

    // Prevent coreclr native bootstrapper from failing with relative appbase
    TCHAR szFullAppBase[MAX_PATH];
    if (!GetFullPath(data.applicationBase, szFullAppBase))
    {
        exitCode = 1;
        goto Finished;
    }

    data.applicationBase = szFullAppBase;

    m_hHostModule = LoadNativeHost(pwzHostModuleName);
    if (!m_hHostModule)
    {
        if (m_fVerboseTrace)
            ::_tprintf_s(_T("Failed to load: %s\r\n"), pwzHostModuleName);
        m_hHostModule = nullptr;
        goto Finished;
    }
    if (m_fVerboseTrace)
        ::_tprintf_s(_T("Loaded Module: %s\r\n"), pwzHostModuleName);

    pfnCallApplicationMain = (FnCallApplicationMain)GetEntryPointFromHost(m_hHostModule, pszCallApplicationMainName);
    if (!pfnCallApplicationMain)
    {
        if (m_fVerboseTrace)
            ::_tprintf_s(_T("Failed to find function %S in %s\n"), pszCallApplicationMainName, pwzHostModuleName);
        fSuccess = false;
        goto Finished;
    }
    if (m_fVerboseTrace)
        printf_s("Found DLL Export: %s\r\n", pszCallApplicationMainName);

    hr = pfnCallApplicationMain(&data);
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
        if (FreeNativeHost(m_hHostModule))
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

    if (szCurrentDirectory)
    {
        free(szCurrentDirectory);
    }

    return exitCode;
}

#if PLATFORM_UNIX
int main(int argc, char* argv[])
#else
int wmain(int argc, wchar_t* argv[])
#endif
{
    // Check for the debug flag before doing anything else
    for (int i = 1; i < argc; ++i)
    {
        //anything without - or -- is appbase or non-dnx command
        if (argv[i][0] != _T('-'))
        {
            break;
        }
        if (StringsEqual(argv[i], _T("--appbase")))
        {
            //skip path argument
            ++i;
            continue;
        }
        if (StringsEqual(argv[i], _T("--debug")))
        {
            WaitForDebuggerToAttach();
            break;
        }
    }

    return CallApplicationProcessMain(argc, argv);
}
