// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

#include "stdafx.h"
#include <string>
#include <sstream>
#include <stdexcept>
#include <assert.h>
#include <dlfcn.h>
#include "app_main.h"
#include "trace_writer.h"

std::string GetNativeBootstrapperDirectory();

bool IsTracingEnabled()
{
    char* dnxTraceEnv = getenv("DNX_TRACE");
    return dnxTraceEnv != NULL && (strcmp(dnxTraceEnv, "1") == 0);
}

void SetConsoleHost()
{
    char* dnxConsoleHostEnv = getenv("DNX_CONSOLE_HOST");

    if (dnxConsoleHostEnv == NULL)
    {
        setenv("DNX_CONSOLE_HOST", "1", 1);
    }
}

bool GetAppBasePathFromEnvironment(char* szPath)
{
    char* appBaseEnv = getenv("DNX_APPBASE");

    if (appBaseEnv != NULL && strlen(appBaseEnv) < PATH_MAX)
    {
        strcpy(szPath, appBaseEnv);
        return true;
    }

    return false;
}

bool GetFullPath(const char* szPath, char* szNormalizedPath)
{
    if (realpath(szPath, szNormalizedPath) == nullptr)
    {
        printf("Failed to get full path of application base: %s\r\n", szPath);
        return false;
    }

    return true;
}

int CallApplicationMain(const char* moduleName, const char* functionName, CALL_APPLICATION_MAIN_DATA* data, dnx::trace_writer& trace_writer)
{
    auto localPath = GetNativeBootstrapperDirectory().append("/").append(moduleName);

    void* host = nullptr;
    try
    {
        host = dlopen(localPath.c_str(), RTLD_NOW | RTLD_GLOBAL);
        if (!host)
        {
            throw std::runtime_error(std::string("Failed to load: ").append(moduleName));
        }

        trace_writer.write(std::string("Loaded module: ").append(moduleName), true);

        auto pfnCallApplicationMain = reinterpret_cast<FnCallApplicationMain>(dlsym(host, functionName));
        if (!pfnCallApplicationMain)
        {
            std::ostringstream oss;
            oss << "Failed to find export '" << functionName << "' in " << moduleName;
            throw std::runtime_error(oss.str());
        }

        trace_writer.write(std::string("Found export: ").append(functionName), true);

        auto result = pfnCallApplicationMain(data);
        dlclose(host);
        return result == 0 ? data->exitcode : result;
    }
    catch(const std::exception& ex)
    {
        if(host)
        {
            dlclose(host);
        }

        throw;
    }
}

bool SetEnvironmentVariable(const char* lpName, const char* lpValue)
{
    int ret;

    if (lpValue != nullptr)
    {
        ret = setenv(lpName, lpValue, 1);
    }
    else
    {
        ret = unsetenv(lpName);
    }

    return ret == 0;
}
