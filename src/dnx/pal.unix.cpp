// Copyright (c) Microsoft Open Technologies, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

#include "stdafx.h"
#include <assert.h>
#include <dlfcn.h>

LPTSTR GetNativeBootstrapperDirectory();

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

BOOL GetAppBasePathFromEnvironment(LPTSTR szPath)
{
    char* appBaseEnv = getenv("DNX_APPBASE");

    if (appBaseEnv != NULL && strlen(appBaseEnv) < PATH_MAX)
    {
        strcpy(szPath, appBaseEnv);
        return TRUE;
    }

    return FALSE;
}

BOOL GetFullPath(LPCTSTR szPath, LPTSTR szNormalizedPath)
{
    if (realpath(szPath, szNormalizedPath) == nullptr)
    {
        printf("Failed to get full path of application base: %s\r\n", szPath);
        return FALSE;
    }

    return TRUE;
}

HMODULE LoadNativeHost(LPCTSTR szHostModuleName)
{
    LPTSTR localPath = GetNativeBootstrapperDirectory();

    strcat(localPath, "/");
    strcat(localPath, szHostModuleName);

    HMODULE hHost = dlopen(localPath, RTLD_NOW | RTLD_GLOBAL);

    free(localPath);

    return hHost;
}

BOOL FreeNativeHost(HMODULE hModule)
{
    if (hModule != nullptr)
    {
        return dlclose(hModule);
    }

    return TRUE;
}

FARPROC GetEntryPointFromHost(HMODULE hModule, LPCSTR lpProcName)
{
    return dlsym(hModule, lpProcName);
}

BOOL SetEnvironmentVariable(LPCTSTR lpName, LPCTSTR lpValue)
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
