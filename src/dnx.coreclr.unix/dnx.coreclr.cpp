// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

#include "stdafx.h"
#include "tpa.h"
#include "utils.h"
#include "app_main.h"
#include <assert.h>
#include <string>
#include <vector>
#include <fstream>

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

typedef int (*MultiByteToWideChar_fn)(
            unsigned int CodePage,
            int32_t dwFlags,
            const char* lpMultiByteStr,
            int cbMultiByte,
            wchar_t* lpWideCharStr,
            int cchWideChar);

typedef int (*host_main_fn)(const int argc, const wchar_t** argv, const bootstrapper_context* ctx);

#define BootstrapperName "Microsoft.Dnx.Host.CoreClr"

namespace
{

void* pLibCoreClr = nullptr;
MultiByteToWideChar_fn MultiByteToWideChar = nullptr;

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

bool GetTrustedPlatformAssembliesList(const std::string& runtime_directory, std::string& trusted_assemblies)
{
        // Try native images first
    if (!GetTrustedPlatformAssembliesList(runtime_directory, true, trusted_assemblies))
    {
        if (!GetTrustedPlatformAssembliesList(runtime_directory, false, trusted_assemblies))
        {
            return false;
        }
    }

    return true;
}

bool LoadCoreClrAtPath(const char* runtime_directory, void** ppLibCoreClr)
{
    const char* LIBCORECLR_NAME =
#ifdef PLATFORM_DARWIN
        "libcoreclr.dylib";
#else
        "libcoreclr.so";
#endif

    auto coreclr_lib_path = dnx::utils::path_combine(runtime_directory, LIBCORECLR_NAME);

    *ppLibCoreClr = dlopen(coreclr_lib_path.c_str(), RTLD_NOW | RTLD_GLOBAL);

    return *ppLibCoreClr != nullptr;
}

void FreeCoreClr()
{
    if (pLibCoreClr)
    {
        dlclose(pLibCoreClr);
        pLibCoreClr = nullptr;
    }
}

int LoadCoreClr(const char* runtime_directory)
{
    void* ret = nullptr;

    LoadCoreClrAtPath(runtime_directory, &pLibCoreClr);

    if (!pLibCoreClr)
    {
        char* error = dlerror();
        fprintf(stderr, "failed to locate libcoreclr with error %s\n", error);

        FreeCoreClr();
        return 1;
    }
    
    return 0;
}

int32_t initialize_runtime(CALL_APPLICATION_MAIN_DATA* data, void **host_handle, unsigned int* domain_id)
{
    auto coreclr_initialize = (coreclr_initialize_fn)dlsym(pLibCoreClr, "coreclr_initialize");
    if (!coreclr_initialize)
    {
        fprintf(stderr, "Could not find coreclr_initialize entrypoint in coreclr\n");
        return 1;
    }

    auto bootstrapper_path = GetPathToBootstrapper();

    const char* property_keys[] =
    {
        "APPBASE",
        "TRUSTED_PLATFORM_ASSEMBLIES",
        "APP_PATHS",
    };

    std::string trusted_assemblies;

    if (!GetTrustedPlatformAssembliesList(data->runtimeDirectory, trusted_assemblies))
    {
        fprintf(stderr, "Failed to find files in the coreclr directory\n");
        return 1;
    }

    // Add the assembly containing the app domain manager to the trusted list
    trusted_assemblies.append(dnx::utils::path_combine(data->runtimeDirectory, BootstrapperName ".dll"));

    const char* property_values[] = {
        // APPBASE
        data->applicationBase,
        // TRUESTED_PLATFORM_ASSEMBLIES
        trusted_assemblies.c_str(),
        // APP_PATHS
        data->runtimeDirectory,
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
        return 1;
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
        return 1;
    }

    return coreclr_shutdown(host_handle, domain_id);
}

// the caller is responsible for deleting the instance
const wchar_t* to_wchar_t(const char* str)
{
    auto length = MultiByteToWideChar(0 /*CP_ACP*/, 0, str, -1, nullptr, 0);
    auto str_w = new wchar_t[length];
    MultiByteToWideChar(0 /*CP_ACP*/, 0, str, length, str_w, length);
    return str_w;
}

int InvokeDelegate(host_main_fn host_main, int argc, const char** argv, const bootstrapper_context& ctx)
{
    const wchar_t** wchar_argv = new const wchar_t*[argc];
    for (auto i = 0; i < argc; i++)
    {
        wchar_argv[i] = to_wchar_t(argv[i]);
    }

    auto exit_code = host_main(argc, wchar_argv, &ctx);

    for (auto i = 0; i < argc; i++)
    {
        delete[] wchar_argv[i];
    }
    delete[] wchar_argv;

    return exit_code;
}

#if defined(PLATFORM_LINUX)
std::string get_os_version()
{
    std::vector<std::string> qualifiers { "DISTRIB_ID=", "DISTRIB_RELEASE=" };

    std::ifstream lsb_release;
    lsb_release.open("/etc/lsb-release", std::ifstream::in);
    if (lsb_release.is_open())
    {
        std::string os_version;
        for (std::string line; std::getline(lsb_release, line); )
        {
            for (auto& qualifier : qualifiers)
            {
                if (line.compare(0, qualifier.length(), qualifier) == 0)
                {
                    if (os_version.length() > 0)
                    {
                        os_version.append(" ");
                    }
                    os_version.append(line.substr(qualifier.length()));
                }
            }
        }

        if (os_version.length() == 0)
        {
            fprintf(stderr, "Could not find version information. OS version will default to the empty string.\n");
        }

        return os_version;
    }

    fprintf(stderr, "Could not open /etc/lsb_release. OS version will default to the empty string.\n");
    return "";
}

#endif

bootstrapper_context initialize_context(const CALL_APPLICATION_MAIN_DATA* data)
{
    bootstrapper_context ctx;
    ctx.operating_system = 
#if defined(PLATFORM_LINUX)
    to_wchar_t("Linux");
#else
    to_wchar_t("Darwin");
#endif

    ctx.os_version = to_wchar_t(get_os_version().c_str());
    // currently we only support 64-bit linux
    ctx.architecture = to_wchar_t("x64");
    ctx.runtime_directory = to_wchar_t(data->runtimeDirectory);
    ctx.application_base = to_wchar_t(data->applicationBase);
    // we always wanto handle exceptions since they cannot be
    // marshaled from managed to native code
    ctx.handle_exceptions = true;

    return ctx;
}

void clean_context(bootstrapper_context& ctx)
{
    delete[] ctx.operating_system;
    ctx.operating_system = nullptr;
    delete[] ctx.architecture;
    ctx.architecture = nullptr;
    delete[] ctx.os_version;
    ctx.os_version = nullptr;
    delete[] ctx.runtime_directory;
    ctx.runtime_directory = nullptr;
    delete[] ctx.application_base;
    ctx.application_base = nullptr;
}

int CallMain(CALL_APPLICATION_MAIN_DATA* data)
{
    void* host_handle = nullptr;
    unsigned int domain_id;

    auto result = initialize_runtime(data, &host_handle, &domain_id);
    if (result < 0)
    {
        fprintf(stderr, "Failed to initialize runtime: 0x%08x\n", result);
        return 1;
    }
    
    void* host_main;
    result = create_delegate(host_handle, domain_id, &host_main);
    if (result < 0)
    {
        fprintf(stderr, "Failed to create delegate: 0x%08x\n", result);
    }
    else
    {
        auto ctx = initialize_context(data);
        data->exitcode = InvokeDelegate((host_main_fn)host_main, data->argc, data->argv, ctx);
        clean_context(ctx);
    }

    auto shutdown_result = shutdown_runtime(host_handle, domain_id);
    if (shutdown_result < 0)
    {
        fprintf(stderr, "Failed to shutdown runtime: 0x%08x\n", shutdown_result);
        return 1;
    }

    return result;
}
}

extern "C" int CallApplicationMain(CALL_APPLICATION_MAIN_DATA* data)
{
    if (LoadCoreClr(data->runtimeDirectory) != 0)
    {
        return 1;
    }

    int result = 0;

    MultiByteToWideChar = (MultiByteToWideChar_fn)dlsym(pLibCoreClr, "MultiByteToWideChar");
    if (!MultiByteToWideChar)
    {
        fprintf(stderr, "Could not find MultiByteToWideChar entrypoint in coreclr\n");
        result = 1;
    }
    else
    {
        setenv("DNX_FRAMEWORK", "dnxcore50", 1);

        result = CallMain(data);
    }

    FreeCoreClr();

    return result;
}
