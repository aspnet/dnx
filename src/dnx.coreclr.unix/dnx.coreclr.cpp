// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

#include "stdafx.h"
#include "tpa.h"
#include <assert.h>

// Windows types used by the ExecuteAssembly function
typedef int32_t HRESULT;
typedef const char* LPCSTR;
typedef uint32_t DWORD;

typedef struct CALL_APPLICATION_MAIN_DATA
{
    char* applicationBase;
    char* runtimeDirectory;
    int argc;
    char** argv;
    int exitcode;
} *PCALL_APPLICATION_MAIN_DATA;

// Prototype of the ExecuteAssembly function from the libcoreclr.so
typedef HRESULT (*ExecuteAssemblyFunction)(
                    LPCSTR exePath,
                    LPCSTR coreClrPath,
                    LPCSTR appDomainFriendlyName,
                    int propertyCount,
                    LPCSTR* propertyKeys,
                    LPCSTR* propertyValues,
                    int argc,
                    LPCSTR* argv,
                    LPCSTR managedAssemblyPath,
                    LPCSTR entryPointAssemblyName,
                    LPCSTR entryPointTypeName,
                    LPCSTR entryPointMethodsName,
                    DWORD* exitCode);

const HRESULT S_OK = 0;
const HRESULT E_FAIL = -1;

namespace
{

#ifdef PLATFORM_DARWIN
const char* LIBCORECLR_NAME = "libcoreclr.dylib";
const char* LIBCORECLRPAL_NAME = "libcoreclrpal.dylib";
#else
const char* LIBCORECLR_NAME = "libcoreclr.so";
const char* LIBCORECLRPAL_NAME = "libcoreclrpal.so";
#endif

std::string GetPathToBootstrapper()
{
#ifdef PLATFORM_DARWIN
    char pathToBootstrapper[PROC_PIDPATHINFO_MAXSIZE];
    ssize_t pathLen = proc_pidpath(getpid(), pathToBootstrapper, sizeof(pathToBootstrapper));
#else
    char pathToBootstrapper[PATH_MAX + 1];
    ssize_t pathLen = readlink("/proc/self/exe", pathToBootstrapper, PATH_MAX);
#endif
    assert(pathLen > 0);

    // ensure pathToBootstrapper is null terminated, readlink for example
    // will not null terminate it.
    pathToBootstrapper[pathLen] = '\0';

    return std::string(pathToBootstrapper);
}

bool GetTrustedPlatformAssembliesList(const std::string& tpaDirectory, bool isNative, std::string& trustedPlatformAssemblies)
{
    //TODO: The Windows version of this actually ensures the files are present.  We just fail for native and assume MSIL is present
    if (isNative)
    {
        return false;
    }

    for (auto assembly_name : CreateTpaBase(isNative))
    {
        trustedPlatformAssemblies.append(tpaDirectory);
        trustedPlatformAssemblies.append("/");
        trustedPlatformAssemblies.append(assembly_name);
        trustedPlatformAssemblies.append(":");
    }

    return true;
}

void* pLibCoreClr = nullptr;
void* pLibCoreClrPal = nullptr;

bool LoadCoreClrAtPath(const std::string loadPath, void** ppLibCoreClr, void** ppLibCoreClrPal)
{
    std::string coreClrDllPath = loadPath;
    std::string coreClrPalPath = loadPath;

    coreClrDllPath.append("/");
    coreClrDllPath.append(LIBCORECLR_NAME);

    coreClrPalPath.append("/");
    coreClrPalPath.append(LIBCORECLRPAL_NAME);

    *ppLibCoreClrPal = dlopen(coreClrPalPath.c_str(), RTLD_NOW | RTLD_GLOBAL);
    *ppLibCoreClr = dlopen(coreClrDllPath.c_str(), RTLD_NOW | RTLD_GLOBAL);

    return *ppLibCoreClrPal != nullptr && *ppLibCoreClr != nullptr;
}

// libcoreclr has a dependency on libcoreclrpal, which is commonly not on LD_LIBRARY_PATH, so for every
// location we try to load libcoreclr from, we first try to load libcoreclrpal so when we load coreclr
// itself the linker is happy.
//
// NOTE: The code here is structured in a way such that it is OK if the load of libcoreclrpal fails,
// because depending on the version of the coreclr DNX has, the PAL may still be staticlly linked
// into coreclr and we want to be able to load coreclr's that have been built this way.
void LoadCoreClr(std::string& runtimeDirectory)
{
    void* ret = nullptr;

    char* coreClrEnvVar = getenv("CORECLR_DIR");

    if (coreClrEnvVar)
    {
        runtimeDirectory = coreClrEnvVar;

        LoadCoreClrAtPath(runtimeDirectory, &pLibCoreClr, &pLibCoreClrPal);

        if (!pLibCoreClr && pLibCoreClrPal)
        {
            // The PAL loaded but CoreCLR did not.  We are going to try other places, so let's
            // unload this PAL.
            dlclose(pLibCoreClrPal);
            pLibCoreClrPal = nullptr;
        }
    }

    if (!pLibCoreClr)
    {
        // Try to load coreclr from application path.

        runtimeDirectory = GetPathToBootstrapper();

        size_t lastSlash = runtimeDirectory.rfind('/');

        assert(lastSlash != std::string::npos);

        runtimeDirectory.erase(lastSlash);

        LoadCoreClrAtPath(runtimeDirectory, &pLibCoreClr, &pLibCoreClrPal);
    }
}

void FreeCoreClr()
{
    if (pLibCoreClr)
    {
        dlclose(pLibCoreClr);
        pLibCoreClr = nullptr;
    }

    if (pLibCoreClrPal)
    {
        dlclose(pLibCoreClrPal);
        pLibCoreClrPal = nullptr;
    }
}
}

extern "C" HRESULT CallApplicationMain(PCALL_APPLICATION_MAIN_DATA data)
{
    HRESULT hr = S_OK;
    size_t cchTrustedPlatformAssemblies = 0;
    std::string runtimeDirectory;

    if (data->runtimeDirectory)
    {
        runtimeDirectory = data->runtimeDirectory;
    }
    else
    {
        // TODO: This should get the directory that this library is in, not the CWD.
        char szCurrentDirectory[PATH_MAX];

        if (!getcwd(szCurrentDirectory, PATH_MAX))
        {
            return E_FAIL;
        }

        runtimeDirectory = std::string(szCurrentDirectory);
    }

    std::string coreClrDirectory;
    LoadCoreClr(coreClrDirectory);

    if (!pLibCoreClr)
    {
        char* error = dlerror();
        fprintf(stderr, "failed to locate libcoreclr with error %s\n", error);

        FreeCoreClr();
        return E_FAIL;
    }

    const char* property_keys[] =
    {
        "APPBASE",
        "TRUSTED_PLATFORM_ASSEMBLIES",
        "APP_PATHS",
    };

    std::string trustedPlatformAssemblies;

    // Try native images first
    if (!GetTrustedPlatformAssembliesList(coreClrDirectory.c_str(), true, trustedPlatformAssemblies))
    {
        if (!GetTrustedPlatformAssembliesList(coreClrDirectory.c_str(), false, trustedPlatformAssemblies))
        {
            fprintf(stderr, "Failed to find files in the coreclr directory\n");

            FreeCoreClr();
            return E_FAIL;
        }
    }

    // Add the assembly containing the app domain manager to the trusted list
    trustedPlatformAssemblies.append(runtimeDirectory);
    trustedPlatformAssemblies.append("Microsoft.Dnx.Host.CoreClr.dll");

    std::string appPaths(runtimeDirectory);

    appPaths.append(":");
    appPaths.append(coreClrDirectory);
    appPaths.append(":");

    const char* property_values[] = {
        // APPBASE
        data->applicationBase,
        // TRUESTED_PLATFORM_ASSEMBLIES
        trustedPlatformAssemblies.c_str(),
        // APP_PATHS
        appPaths.c_str(),
    };

    ExecuteAssemblyFunction executeAssembly = (ExecuteAssemblyFunction)dlsym(pLibCoreClr, "ExecuteAssembly");

    if (!executeAssembly)
    {
        fprintf(stderr, "Could not find ExecuteAssembly entrypoint in coreclr.\n");

        FreeCoreClr();
        return E_FAIL;
    }

    setenv("DNX_FRAMEWORK", "dnxcore50", 1);

    std::string coreClrDllPath(coreClrDirectory);
    coreClrDllPath.append("/");
    coreClrDllPath.append(LIBCORECLR_NAME);

    std::string pathToBootstrapper = GetPathToBootstrapper();

    hr = executeAssembly(pathToBootstrapper.c_str(),
                         coreClrDllPath.c_str(),
                         "Microsoft.Dnx.Host.CoreClr",
                         sizeof(property_keys) / sizeof(property_keys[0]),
                         property_keys,
                         property_values,
                         data->argc,
                         (const char**)data->argv,
                         nullptr,
                         "Microsoft.Dnx.Host.CoreClr, Version=0.0.0.0",
                         "DomainManager",
                         "Execute",
                         (DWORD*)&(data->exitcode));

    FreeCoreClr();

    return hr;
}
