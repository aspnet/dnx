// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

#include "stdafx.h"
#include "app_main.h"
#include "xplat.h"
#include "trace_writer.h"
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
    wchar_t buff[2];
    return GetEnvironmentVariable(L"DNX_TRACE", buff, 2) == 1 && buff[0] == L'1';
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

bool GetAppBasePathFromEnvironment(LPTSTR pszAppBase)
{
    DWORD dwAppBase = GetEnvironmentVariable(_T("DNX_APPBASE"), pszAppBase, MAX_PATH);
    return dwAppBase != 0 && dwAppBase < MAX_PATH;
}

bool GetFullPath(LPCTSTR szPath, LPTSTR pszNormalizedPath)
{
    DWORD dwFullAppBase = GetFullPathName(szPath, MAX_PATH, pszNormalizedPath, nullptr);
    if (!dwFullAppBase)
    {
        ::_tprintf_s(_T("Failed to get full path of application base: %s\r\n"), szPath);
        return false;
    }
    else if (dwFullAppBase > MAX_PATH)
    {
        ::_tprintf_s(_T("Full path of application base is too long\r\n"));
        return false;
    }

    return true;
}

namespace
{
    std::wstring get_runtime_path(dnx::trace_writer& trace_writer)
    {
        wchar_t servicing_location_buffer[MAX_PATH];

        std::vector<wchar_t*> servicing_locations =
        {
            L"DNX_SERVICING",
            L"ProgramFiles(x86)",
            // The servicing index should be always under %ProgramFiles(x86)% however
            // on 32-bit OSes there is only %ProgramFiles%
            L"ProgramFiles"
        };

        int result = 0;
        size_t index = 0;
        for (; index < servicing_locations.size(); index++)
        {
            if ((result = GetEnvironmentVariable(servicing_locations[index], servicing_location_buffer, MAX_PATH)) != 0)
            {
                break;
            }
        }

        _ASSERTE(index < servicing_locations.size());

        if (result > MAX_PATH)
        {
            throw std::runtime_error(std::string("The value of the '")
                .append(dnx::utils::to_string(servicing_locations.at(index)))
                .append("' environment variable is invalid. The application will exit."));
        }

        // %DNX_SERVICING% should point directly to servicing folder. For program files we need to append the
        // actual servicing folder location to %ProgramFilesXXX%
        auto is_default_servicing_location = index != 0;
        auto runtime_path = dnx::servicing::get_runtime_path(servicing_location_buffer, is_default_servicing_location, trace_writer);

        if (runtime_path.length() > 0)
        {
            return dnx::utils::path_combine(runtime_path, L"bin\\");
        }

        return std::wstring{};
    }
}

int CallApplicationMain(const wchar_t* moduleName, const char* functionName, CALL_APPLICATION_MAIN_DATA* data, dnx::trace_writer& trace_writer)
{
    HMODULE hostModule = nullptr;
    try
    {
        const auto runtime_new_path = get_runtime_path(trace_writer);
        if (runtime_new_path.length() > 0)
        {
            trace_writer.write(std::wstring(L"Redirecting runtime to: ").append(runtime_new_path), true);
            SetEnvironmentVariable(_T("DNX_DEFAULT_LIB"), runtime_new_path.c_str());
            data->runtimeDirectory = runtime_new_path.c_str();

            #if defined(CORECLR_WIN)
                SetEnvironmentVariable(_T("CORECLR_DIR"), runtime_new_path.c_str());
            #endif
        }

        auto module_path = dnx::utils::path_combine(runtime_new_path, moduleName);
        hostModule = LoadLibraryEx(module_path.c_str(), NULL, LOAD_LIBRARY_SEARCH_DEFAULT_DIRS);
        if (!hostModule)
        {
            throw std::runtime_error(std::string("Failed to load: ")
                .append(dnx::utils::to_string(moduleName)));
        }

        trace_writer.write(std::wstring(L"Loaded module: ").append(module_path), true);

        auto pfnCallApplicationMain = reinterpret_cast<FnCallApplicationMain>(GetProcAddress(hostModule, functionName));
        if (!pfnCallApplicationMain)
        {
            std::ostringstream oss;
            oss << "Failed to find export '" << functionName << "' in " << dnx::utils::to_string(moduleName);
            throw std::runtime_error(oss.str());
        }

        trace_writer.write(std::wstring(L"Found export: ").append(dnx::utils::to_wstring(functionName)), true);

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
