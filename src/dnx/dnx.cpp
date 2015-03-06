// Defines the entry point for the console application.

#include "stdafx.h"
#include "dnx.h"

void GetModuleDirectory(HMODULE module, LPTSTR szPath)
{
    DWORD dirLength = GetModuleFileName(module, szPath, MAX_PATH);
    for (dirLength--; dirLength >= 0 && szPath[dirLength] != _T('\\'); dirLength--);
    szPath[dirLength + 1] = _T('\0');
}

bool LastIndexOfChar(LPCTSTR const pszStr, TCHAR c, size_t* pIndex)
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
    return ::_tcsnicmp(pszStrA, pszStrB, MAX_PATH) == 0;
}

bool StringEndsWith(LPCTSTR const pszStr, LPCTSTR const pszSuffix)
{
    size_t nStrLen = _tcsnlen(pszStr, MAX_PATH);
    size_t nSuffixLen = _tcsnlen(pszSuffix, MAX_PATH);

    if (nSuffixLen > nStrLen)
    {
        return false;
    }

    size_t nOffset = nStrLen - nSuffixLen;

    return StringsEqual(pszStr + nOffset, pszSuffix);
}

bool LastPathSeparatorIndex(LPCTSTR const pszPath, size_t* pIndex)
{
    size_t nLastSlashIndex;
    size_t nLastBackSlashIndex;

    bool hasLastSlashIndex = LastIndexOfChar(pszPath, _T('/'), &nLastSlashIndex);
    bool hasLastBackSlashIndex = LastIndexOfChar(pszPath, _T('\\'), &nLastBackSlashIndex);

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

    CopyMemory(pszParentDir, pszPath, (nLastSeparatorIndex + 1) * sizeof(TCHAR));
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
    if (StringEndsWith(pszPathArg, _T(".exe")) || StringEndsWith(pszPathArg, _T(".dll")))
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
    TCHAR szTrace[2];
    DWORD nEnvTraceSize = GetEnvironmentVariable(_T("DNX_TRACE"), szTrace, 2);
    if (nEnvTraceSize == 0)
    {
        nEnvTraceSize = GetEnvironmentVariable(_T("DNX_TRACE"), szTrace, 2);
    }
    bool m_fVerboseTrace = (nEnvTraceSize == 1);
    if (m_fVerboseTrace)
    {
        szTrace[1] = _T('\0');
        m_fVerboseTrace = (_tcsnicmp(szTrace, _T("1"), 1) == 0);
    }

    bool fSuccess = true;
    HMODULE m_hHostModule = nullptr;
#if CORECLR_WIN
    LPCTSTR pwzHostModuleName = _T("dnx.coreclr.dll");
#else
    LPCTSTR pwzHostModuleName = _T("dnx.clr.dll");
#endif

    // Note: need to keep as ASCII as GetProcAddress function takes ASCII params
    LPCSTR pszCallApplicationMainName = "CallApplicationMain";
    FnCallApplicationMain pfnCallApplicationMain = nullptr;
    int exitCode = 0;

    TCHAR szCurrentDirectory[MAX_PATH];
    GetModuleDirectory(NULL, szCurrentDirectory);

    // Set the DEFAULT_LIB environment variable to be the same directory
    // as the exe
    SetEnvironmentVariable(_T("DNX_DEFAULT_LIB"), szCurrentDirectory);

    // Set the DNX_CONOSLE_HOST flag which will print exceptions
    // to stderr instead of throwing
    SetEnvironmentVariable(_T("DNX_CONSOLE_HOST"), _T("1"));

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
    DWORD dwAppBase = GetEnvironmentVariable(_T("DNX_APPBASE"), szAppBase, MAX_PATH);
    if (dwAppBase == 0)
    {
        dwAppBase = GetEnvironmentVariable(_T("DNX_APPBASE"), szAppBase, MAX_PATH);
    }
    if (dwAppBase != 0)
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
    DWORD dwFullAppBase = GetFullPathName(data.applicationBase, MAX_PATH, szFullAppBase, nullptr);
    if (!dwFullAppBase)
    {
        ::_tprintf_s(_T("Failed to get full path of application base: %s\r\n"), data.applicationBase);
        exitCode = 1;
        goto Finished;
    }
    else if (dwFullAppBase > MAX_PATH)
    {
        ::_tprintf_s(_T("Full path of application base is too long\r\n"));
        exitCode = 1;
        goto Finished;
    }
    data.applicationBase = szFullAppBase;

    m_hHostModule = ::LoadLibraryEx(pwzHostModuleName, NULL, LOAD_LIBRARY_SEARCH_DEFAULT_DIRS);
    if (!m_hHostModule)
    {
        if (m_fVerboseTrace)
            ::_tprintf_s(_T("Failed to load: %s\r\n"), pwzHostModuleName);
        m_hHostModule = nullptr;
        goto Finished;
    }
    if (m_fVerboseTrace)
        ::_tprintf_s(_T("Loaded Module: %s\r\n"), pwzHostModuleName);

    pfnCallApplicationMain = (FnCallApplicationMain)::GetProcAddress(m_hHostModule, pszCallApplicationMainName);
    if (!pfnCallApplicationMain)
    {
        if (m_fVerboseTrace)
            ::_tprintf_s(_T("Failed to find function %S in %s\n"), pszCallApplicationMainName, pwzHostModuleName);
        fSuccess = false;
        goto Finished;
    }
    if (m_fVerboseTrace)
        ::_tprintf_s(_T("Found DLL Export: %s\r\n"), pszCallApplicationMainName);

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
    // Check for the debug flag before doing anything else
    for (int i = 0; i < argc; i++)
    {
        if (StringsEqual(argv[i], _T("--debug")))
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
    }

    return CallApplicationProcessMain(argc, argv);
}
