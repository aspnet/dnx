// Copyright (c) Microsoft Open Technologies, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

#include "stdafx.h"

LPTSTR GetNativeBootstrapperDirectory()
{
    LPTSTR szPath = (LPTSTR)calloc(MAX_PATH, sizeof(TCHAR));
    DWORD dirLength = GetModuleFileName(NULL, szPath, MAX_PATH);
    for (dirLength--; dirLength >= 0 && szPath[dirLength] != _T('\\'); dirLength--);
    szPath[dirLength + 1] = _T('\0');
    return szPath;
}

void WaitForDebuggerToAttach()
{
    if (!IsDebuggerPresent())
    {
        ::_tprintf_s(_T("Process Id: %ld\r\n"), GetCurrentProcessId());
        ::_tprintf_s(_T("Waiting for the debugger to attach...\r\n"));

        // Do not use getchar() like in corerun because it doesn't work well with remote sessions
        while (!IsDebuggerPresent())
        {
            Sleep(250);
        }

        ::_tprintf_s(_T("Debugger attached.\r\n"));
    }
}

bool IsTracingEnabled()
{
    TCHAR szTrace[2];
    DWORD nEnvTraceSize = GetEnvironmentVariable(_T("DNX_TRACE"), szTrace, 2);
    bool m_fVerboseTrace = (nEnvTraceSize == 1);
    if (m_fVerboseTrace)
    {
        szTrace[1] = _T('\0');
        return _tcsnicmp(szTrace, _T("1"), 1) == 0;
    }

    return false;
}

void SetConsoleHost()
{
    TCHAR szConsoleHost[2];
    DWORD nEnvConsoleHostSize = GetEnvironmentVariable(_T("DNX_CONSOLE_HOST"), szConsoleHost, 2);
    if (nEnvConsoleHostSize == 0)
    {
        SetEnvironmentVariable(_T("DNX_CONSOLE_HOST"), _T("1"));
    }
}

BOOL GetAppBasePathFromEnvironment(LPTSTR pszAppBase)
{
    DWORD dwAppBase = GetEnvironmentVariable(_T("DNX_APPBASE"), pszAppBase, MAX_PATH);
    return dwAppBase != 0 && dwAppBase < MAX_PATH;
}

BOOL GetFullPath(LPCTSTR szPath, LPTSTR pszNormalizedPath)
{
    DWORD dwFullAppBase = GetFullPathName(szPath, MAX_PATH, pszNormalizedPath, nullptr);
    if (!dwFullAppBase)
    {
        ::_tprintf_s(_T("Failed to get full path of application base: %s\r\n"), szPath);
        return FALSE;
    }
    else if (dwFullAppBase > MAX_PATH)
    {
        ::_tprintf_s(_T("Full path of application base is too long\r\n"));
        return FALSE;
    }

    return TRUE;
}

HMODULE LoadNativeHost(LPCTSTR pszHostModuleName)
{
    return LoadLibraryEx(pszHostModuleName, NULL, LOAD_LIBRARY_SEARCH_DEFAULT_DIRS);
}

BOOL FreeNativeHost(HMODULE hHost)
{
    return FreeLibrary(hHost);
}

FARPROC GetEntryPointFromHost(HMODULE hHost, LPCSTR lpProcName)
{
    return GetProcAddress(hHost, lpProcName);
}
