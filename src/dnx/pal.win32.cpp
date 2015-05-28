// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

#include "stdafx.h"
#include "dnx.h"
#include "xplat.h"
#include "TraceWriter.h"
#include "utils.h"
#include "servicing.h"
#include <sstream>
#include <vector>

std::wstring GetNativeBootstrapperDirectory()
{
    wchar_t buffer[MAX_PATH];
    DWORD dirLength = GetModuleFileName(NULL, buffer, MAX_PATH);
    for (dirLength--; dirLength >= 0 && buffer[dirLength] != _T('\\'); dirLength--);
    buffer[dirLength + 1] = _T('\0');
    return std::wstring(buffer);
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

namespace
{
    std::wstring get_runtime_path(TraceWriter& trace_writer)
    {
        std::vector<wchar_t*> servicing_locations =
        {
            L"DNX_SERVICING",
            L"ProgramFiles(x86)"
        };

        wchar_t servicing_location_buffer[MAX_PATH];
        // The servicing index should be always under %ProgramFiles(x86)% however
        // on 32-bit OSes there is only %ProgramFiles%
        if (GetEnvironmentVariable(L"ProgramFiles(x86)", servicing_location_buffer, MAX_PATH) == 0)
        {
            servicing_locations.push_back(L"ProgramFiles");
        }

        for (auto servicing_location : servicing_locations)
        {
            if (GetEnvironmentVariable(servicing_location, servicing_location_buffer, MAX_PATH) != 0)
            {
                // %DNX_SERVICING% should point directly to servicing folder. For program files we need to append the
                // actual servicing folder location to %ProgramFilesXXX%
                auto append_servicing_folder = servicing_location != servicing_locations.front();
                auto runtime_path = dnx::servicing::get_runtime_path(servicing_location_buffer, append_servicing_folder, trace_writer);

                if (runtime_path.length() > 0)
                {
                    return dnx::utils::path_combine(runtime_path, L"bin\\");
                }
            }
        }

        return std::wstring{};
    }
}

int CallApplicationMain(const wchar_t* moduleName, const char* functionName, CALL_APPLICATION_MAIN_DATA* data, TraceWriter traceWriter)
{
    HMODULE hostModule = nullptr;
    try
    {
        auto runtime_path = get_runtime_path(traceWriter);
        if (runtime_path.length() > 0)
        {
            traceWriter.Write(std::wstring(L"Redirecting runtime to: ").append(runtime_path), true);
            SetEnvironmentVariable(_T("DNX_DEFAULT_LIB"), runtime_path.c_str());
        }

        auto module_path = dnx::utils::path_combine(runtime_path, moduleName);
        hostModule = LoadLibraryEx(module_path.c_str(), NULL, LOAD_LIBRARY_SEARCH_DEFAULT_DIRS);
        if (!hostModule)
        {
            throw std::runtime_error(std::string("Failed to load: ")
                .append(dnx::utils::to_string(moduleName)));
        }

        traceWriter.Write(std::wstring(L"Loaded module: ").append(module_path), true);

        auto pfnCallApplicationMain = reinterpret_cast<FnCallApplicationMain>(GetProcAddress(hostModule, functionName));
        if (!pfnCallApplicationMain)
        {
            std::ostringstream oss;
            oss << "Failed to find export '" << functionName << "' in " << dnx::utils::to_string(moduleName);
            throw std::runtime_error(oss.str());
        }

        traceWriter.Write(std::wstring(L"Found export: ").append(dnx::utils::to_wstring(functionName)), true);

        HRESULT hr = pfnCallApplicationMain(data);
        FreeLibrary(hostModule);
        return SUCCEEDED(hr) ? data->exitcode : hr;
    }
    catch (...)
    {
        if (hostModule)
        {
            FreeLibrary(hostModule);
        }

        throw;
    }
}
