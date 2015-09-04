// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

#include "stdafx.h"
#include "tpa.h"
#include "utils.h"
#include "app_main.h"
#include <assert.h>

typedef int (*coreclr_initialize_fn)(
            const char* exePath,
            const char* appDomainFriendlyName,
            int propertyCount,
            const char** propertyKeys,
            const char** propertyValues,
            void** hostHandle,
            unsigned int* domainId);

typedef int (*coreclr_create_delegate_fn)(
            void* hostHandle,
            unsigned int domainId,
            const char* entryPointAssemblyName,
            const char* entryPointTypeName,
            const char* entryPointMethodName,
            void** delegate);

typedef int (*coreclr_shutdown_fn)(
            void* hostHandle,
            unsigned int domainId);

typedef int (*host_main_fn)(const int argc, const wchar_t** argv);

#define BootstrapperName "Microsoft.Dnx.Host.CoreClr"

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
        trustedPlatformAssemblies.append(dnx::utils::path_combine(tpaDirectory, assembly_name));
        trustedPlatformAssemblies.append(":");
    }

    return true;
}

void* pLibCoreClr = nullptr;
void* pLibCoreClrPal = nullptr;

bool LoadCoreClrAtPath(const std::string& loadPath, void** ppLibCoreClr, void** ppLibCoreClrPal)
{
    std::string coreClrDllPath = dnx::utils::path_combine(loadPath, LIBCORECLR_NAME);
    std::string coreClrPalPath = dnx::utils::path_combine(loadPath, LIBCORECLRPAL_NAME);

    *ppLibCoreClrPal = dlopen(coreClrPalPath.c_str(), RTLD_NOW | RTLD_GLOBAL);
    *ppLibCoreClr = dlopen(coreClrDllPath.c_str(), RTLD_NOW | RTLD_GLOBAL);

    return *ppLibCoreClrPal != nullptr && *ppLibCoreClr != nullptr;
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

// libcoreclr has a dependency on libcoreclrpal, which is commonly not on LD_LIBRARY_PATH, so for every
// location we try to load libcoreclr from, we first try to load libcoreclrpal so when we load coreclr
// itself the linker is happy.
//
// NOTE: The code here is structured in a way such that it is OK if the load of libcoreclrpal fails,
// because depending on the version of the coreclr DNX has, the PAL may still be staticlly linked
// into coreclr and we want to be able to load coreclr's that have been built this way.
int LoadCoreClr(std::string& coreClrDirectory, const std::string& runtimeDirectory)
{
    void* ret = nullptr;

    char* coreClrEnvVar = getenv("CORECLR_DIR");

    if (coreClrEnvVar)
    {
        coreClrDirectory = coreClrEnvVar;

        LoadCoreClrAtPath(coreClrDirectory, &pLibCoreClr, &pLibCoreClrPal);

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
        coreClrDirectory = runtimeDirectory;
        LoadCoreClrAtPath(coreClrDirectory, &pLibCoreClr, &pLibCoreClrPal);
    }
    
    if (!pLibCoreClr)
    {
        char* error = dlerror();
        fprintf(stderr, "failed to locate libcoreclr with error %s\n", error);

        FreeCoreClr();
        return -1;
    }
    
    return 0;
}

int32_t initialize_runtime(const char* app_base, const std::string& coreclr_directory, const std::string& runtime_directory,
    void **host_handle, unsigned int* domain_id)
{
    auto coreclr_initialize = (coreclr_initialize_fn)dlsym(pLibCoreClr, "coreclr_initialize");
    if (!coreclr_initialize)
    {
        fprintf(stderr, "Could not find coreclr_initialize entrypoint in coreclr\n");
        return -1;
    }

    auto bootstrapper_path = GetPathToBootstrapper();

    const char* property_keys[] =
    {
        "APPBASE",
        "TRUSTED_PLATFORM_ASSEMBLIES",
        "APP_PATHS",
    };

    std::string trusted_assemblies;

    // Try native images first
    if (!GetTrustedPlatformAssembliesList(coreclr_directory, true, trusted_assemblies))
    {
        if (!GetTrustedPlatformAssembliesList(coreclr_directory, false, trusted_assemblies))
        {
            fprintf(stderr, "Failed to find files in the coreclr directory\n");
            return -1;
        }
    }

    // Add the assembly containing the app domain manager to the trusted list
    trusted_assemblies.append(dnx::utils::path_combine(runtime_directory, BootstrapperName ".dll"));

    auto app_paths = runtime_directory + ":" + coreclr_directory + ":";

    const char* property_values[] = {
        // APPBASE
        app_base,
        // TRUESTED_PLATFORM_ASSEMBLIES
        trusted_assemblies.c_str(),
        // APP_PATHS
        app_paths.c_str(),
    };

    return coreclr_initialize(bootstrapper_path.c_str(), BootstrapperName, sizeof(property_keys) / sizeof(const char*),
                property_keys, property_values, host_handle, domain_id);
}

int32_t create_delegate(void *host_handle, unsigned int domain_id, void** delegate)
{
    auto coreclr_create_delegate = (coreclr_create_delegate_fn)dlsym(pLibCoreClr, "coreclr_create_delegate");
    if (!coreclr_create_delegate)
    {
        fprintf(stderr, "Could not find coreclr_create_delegate entrypoint in coreclr\n");
        return -1;
    }

    return coreclr_create_delegate(host_handle, domain_id, BootstrapperName", Version=0.0.0.0",
            "DomainManager", "Execute", delegate);
}

int32_t shutdown_runtime(void* host_handle, unsigned int domain_id)
{
    auto coreclr_shutdown = (coreclr_shutdown_fn)dlsym(pLibCoreClr, "coreclr_shutdown");
    if (!coreclr_shutdown)
    {
        fprintf(stderr, "Could not find coreclr_shutdown entrypoint in coreclr\n");
        return -1;
    }

    return coreclr_shutdown(host_handle, domain_id);
}

int InvokeDelegate(host_main_fn host_main, int argc, const char** argv)
{
    typedef int (*MultiByteToWideChar_fn)(
        unsigned int CodePage,
        int32_t dwFlags,
        const char* lpMultiByteStr,
        int cbMultiByte,
        wchar_t* lpWideCharStr,
        int cchWideChar);

    auto MultiByteToWideChar = (MultiByteToWideChar_fn)dlsym(pLibCoreClr, "MultiByteToWideChar");

    if (!MultiByteToWideChar)
    {
        fprintf(stderr, "Could not find MultiByteToWideChar entrypoint in coreclr\n");
        return -1;
    }

    const wchar_t** wchar_argv = new const wchar_t*[argc];
    for (auto i = 0; i < argc; i++)
    {
        int length = MultiByteToWideChar(0 /*CP_ACP*/, 0, argv[i], -1, nullptr, 0);
        auto arg = new wchar_t[length];
        MultiByteToWideChar(0 /*CP_ACP*/, 0, argv[i], length, arg, length);
        wchar_argv[i] = arg;
    }

    auto exit_code = host_main(argc, wchar_argv);

    for (auto i = 0; i < argc; i++)
    {
        delete[] wchar_argv[i];
    }
    delete[] wchar_argv;

    return exit_code;
}

int CallApplicationMain(CALL_APPLICATION_MAIN_DATA* data, const std::string& runtime_directory, const std::string& coreclr_directory)
{
    void* host_handle = nullptr;
    unsigned int domain_id;

    auto result = initialize_runtime(data->applicationBase, coreclr_directory, runtime_directory, &host_handle, &domain_id);
    if (result < 0)
    {
        fprintf(stderr, "Failed to initialize runtime: 0x%08x\n", result);
        return -1;
    }

    void* host_main;
    result = create_delegate(host_handle, domain_id, &host_main);
    if (result < 0)
    {
        fprintf(stderr, "Failed to create delegate: 0x%08x\n", result);
    }
    else
    {
        data->exitcode = InvokeDelegate((host_main_fn)host_main, data->argc, data->argv);
    }

    auto shutdown_result = shutdown_runtime(host_handle, domain_id);
    if (shutdown_result < 0)
    {
        fprintf(stderr, "Failed to shutdown runtime: 0x%08x\n", shutdown_result);
        return -1;
    }

    return result;
}
}

extern "C" int CallApplicationMain(CALL_APPLICATION_MAIN_DATA* data)
{
    const std::string runtime_directory = data->runtimeDirectory;
    std::string coreclr_directory;

    if (LoadCoreClr(coreclr_directory, runtime_directory) != 0)
    {
        return -1;
    }

    setenv("DNX_FRAMEWORK", "dnxcore50", 1);

    auto result = CallApplicationMain(data, runtime_directory, coreclr_directory);

    FreeCoreClr();

    return result;
}
