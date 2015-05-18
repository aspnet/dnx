// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

#include "stdafx.h"
#include "dnx.h"
#include "pal.h"
#include "utils.h"

bool LastIndexOfCharInPath(LPCTSTR pszStr, TCHAR c, size_t* pIndex)
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

bool StringsEqual(LPCTSTR pszStrA, LPCTSTR pszStrB)
{
    return ::_tcsicmp(pszStrA, pszStrB) == 0;
}

bool PathEndsWith(LPCTSTR pszStr, LPCTSTR pszSuffix)
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

bool LastPathSeparatorIndex(LPCTSTR pszPath, size_t* pIndex)
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

void GetParentDir(LPCTSTR pszPath, LPTSTR pszParentDir)
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

void GetFileName(LPCTSTR pszPath, LPTSTR pszFileName)
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

dnx::char_t* GetAppBaseParameterValue(int argc, dnx::char_t* argv[])
{
    for (auto i = 0; i < argc - 1; ++i)
    {
        if (StringsEqual(argv[i], _X("--appbase")))
        {
            return argv[i + 1];
        }
    }

    return nullptr;
}

bool ExpandCommandLineArguments(int nArgc, LPTSTR* ppszArgv, int& nExpandedArgc, LPTSTR*& ppszExpandedArgv)
{
    // If no args or '--appbase' is already given and it has a value
    if (nArgc == 0 || GetAppBaseParameterValue(nArgc, ppszArgv))
    {
        return false;
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

bool GetApplicationBase(const dnx::xstring_t& currentDirectory, int argc, dnx::char_t* argv[], /*out*/ dnx::char_t* fullAppBasePath)
{
    dnx::char_t buffer[MAX_PATH];
    const dnx::char_t* appBase = GetAppBaseParameterValue(argc, argv);

    // Note: We use application base from DNX_APPBASE environment variable only if --appbase
    // did not exist. if neither --appBase nor DNX_APPBASE existed we use current directory
    if (!appBase)
    {
        appBase = GetAppBasePathFromEnvironment(buffer) ? buffer : currentDirectory.c_str();
    }

    // Prevent coreclr native bootstrapper from failing with relative appbase
    return GetFullPath(appBase, fullAppBasePath) != 0;
}

int CallApplicationProcessMain(int argc, dnx::char_t* argv[], TraceWriter traceWriter)
{
    // Set the DNX_CONOSLE_HOST flag which will print exceptions to stderr instead of throwing
    SetConsoleHost();

    auto currentDirectory = GetNativeBootstrapperDirectory();

    // Set the DEFAULT_LIB environment variable to be the same directory as the exe
    SetEnvironmentVariable(_T("DNX_DEFAULT_LIB"), currentDirectory.c_str());

    CALL_APPLICATION_MAIN_DATA data = { 0 };
    data.argc = argc;
    data.argv = const_cast<const dnx::char_t**>(argv);

    dnx::char_t appBaseBuffer[MAX_PATH];
    if (!GetApplicationBase(currentDirectory, argc, argv, appBaseBuffer))
    {
        return 1;
    }

    data.applicationBase = appBaseBuffer;

    try
    {
        const dnx::char_t* hostModuleName =
#if defined(CORECLR_WIN)
#if defined(ONECORE) || defined(ARM)
        _X("dnx.onecore.coreclr.dll");
#else
        _X("dnx.win32.coreclr.dll");
#endif
#elif defined(CORECLR_DARWIN)
        _X("dnx.coreclr.dylib");
#elif defined(CORECLR_LINUX)
        _X("dnx.coreclr.so");
#else
        _X("dnx.clr.dll");
#endif

        // Note: need to keep as ASCII as GetProcAddress function takes ASCII params
        return CallApplicationMain(hostModuleName, "CallApplicationMain", &data, traceWriter);
    }
    catch (const std::exception& ex)
    {
        xout << dnx::utils::to_xstring_t(ex.what()) << std::endl;
        return 1;
    }
}

#if defined(ARM)
int wmain(int argc, wchar_t* argv[])
#elif defined(PLATFORM_UNIX)
int main(int argc, char* argv[])
#else
extern "C" int __stdcall DnxMain(int argc, wchar_t* argv[])
#endif
{
    // Check for the debug flag before doing anything else
    for (int i = 1; i < argc; ++i)
    {
        //anything without - or -- is appbase or non-dnx command
        if (argv[i][0] != _X('-'))
        {
            break;
        }
        if (StringsEqual(argv[i], _X("--appbase")))
        {
            //skip path argument
            ++i;
            continue;
        }
        if (StringsEqual(argv[i], _X("--debug")))
        {
            WaitForDebuggerToAttach();
            break;
        }
    }

    int nExpandedArgc = -1;
    LPTSTR* ppszExpandedArgv = nullptr;
    auto expanded = ExpandCommandLineArguments(argc - 1, &(argv[1]), nExpandedArgc, ppszExpandedArgv);

    auto traceWriter = TraceWriter{ IsTracingEnabled() };
    if (!expanded)
    {
        return CallApplicationProcessMain(argc - 1, &argv[1], traceWriter);
    }

    auto exitCode = CallApplicationProcessMain(nExpandedArgc, ppszExpandedArgv, traceWriter);
    FreeExpandedCommandLineArguments(nExpandedArgc, ppszExpandedArgv);
    return exitCode;
}
