#include "stdafx.h"
#include <assert.h>
#include <dlfcn.h>

void GetNativeBootstrapperDirectory(LPTSTR szPath)
{
    ssize_t ret = readlink("/proc/self/exe", szPath, PATH_MAX - 1);

    assert(ret != -1);

    szPath[ret] = '\0';

    size_t lastSlash = 0;

    for (size_t i = 0; szPath[i] != '\0'; i++)
    {
        if (szPath[i] == '/')
        {
            lastSlash = i;
        }
    }

    szPath[lastSlash] = '\0';
}

void WaitForDebuggerToAttach()
{
    // TODO: Implement this.  procfs will be able to tell us this.
}

bool IsTracingEnabled()
{
    char* dnxTraceEnv = getenv("DNX_TRACE");
    return dnxTraceEnv != NULL && (strcmp(dnxTraceEnv, "1") == 0);
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
    char localPath[PATH_MAX];

    GetNativeBootstrapperDirectory(localPath);

    strcat(localPath, "/");
    strcat(localPath, szHostModuleName);

    return dlopen(localPath, RTLD_NOW | RTLD_GLOBAL);
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

    if (lpValue == nullptr)
    {
        ret = setenv(lpName, lpValue, 1);
    }
    else
    {
        ret = unsetenv(lpName);
    }

    return ret == 0;
}
