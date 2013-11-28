// klr.net45.cpp : Defines the exported functions for the DLL application.
//

#include "stdafx.h"
#include <string>

#include "..\klr\klr.h"

// TODO: Don't hardcode the list of assemblies
static const wchar_t *trustedAssemblies[] =
{
    L"microsoft.csharp",
    L"mscorlib.extensions",
    L"mscorlib",
    L"system.collections.concurrent",
    L"system.collections",
    L"system.componentmodel.eventbasedasync",
    L"system.componentmodel",
    L"system.console.dll",
    L"system.core",
    L"system.diagnostics.contracts",
    L"system.diagnostics.debug",
    L"system.diagnostics.tools",
    L"system.diagnostics.tracing",
    L"system.dynamic.runtime",
    L"system.globalization",
    L"system.io.compression",
    L"system.io",
    L"system.linq.expressions",
    L"system.linq",
    L"system.linq.parallel",
    L"system.linq.queryable",
    L"system.net.http",
    L"system.net.networkinformation",
    L"system.net",
    L"system.net.primitives",
    L"system.net.requests",
    L"system",
    L"system.objectmodel",
    L"system.observable",
    L"system.reflection.emit.ilgeneration",
    L"system.reflection.emit.lightweight",
    L"system.reflection.emit",
    L"system.reflection.extensions",
    L"system.reflection",
    L"system.reflection.primitives",
    L"system.resources.resourcemanager",
    L"system.runtime.extensions",
    L"system.runtime.interopservices",
    L"system.runtime.interopservices.windowsruntime",
    L"system.runtime",
    L"system.runtime.numerics",
    L"system.runtime.serialization.json",
    L"system.runtime.serialization",
    L"system.runtime.serialization.primitives",
    L"system.runtime.serialization.xml",
    L"system.runtime.windowsruntime",
    L"system.security.principal",
    L"system.servicemodel.web",
    L"system.text.encoding.extensions",
    L"system.text.encoding",
    L"system.text.regularexpressions",
    L"system.threading",
    L"system.threading.tasks",
    L"system.threading.tasks.parallel",
    L"system.threading.timer",
    L"system.xml.linq",
    L"system.xml",
    L"system.xml.readerwriter",
    L"system.xml.serialization",
    L"system.xml.xdocument",
    L"system.xml.xmlserializer",
};

typedef int (STDMETHODCALLTYPE *HostMain)(
    const int argc,
    const wchar_t** argv
);

void GetModuleDirectory(HMODULE module, LPWSTR szPath)
{
    DWORD dirLength = GetModuleFileName(module, szPath, MAX_PATH);
    for (dirLength--; dirLength >= 0 && szPath[dirLength] != '\\'; dirLength--);
    szPath[dirLength + 1] = '\0';
}

HMODULE LoadCoreClr()
{
    TCHAR szCoreClrDirectory[MAX_PATH];
    DWORD dwCoreClrDirectory = GetEnvironmentVariableW(L"CORECLR_DIR", szCoreClrDirectory, MAX_PATH);
    HMODULE hCoreCLRModule = nullptr;

    if (dwCoreClrDirectory != 0)
    {
        wstring clrPath(szCoreClrDirectory);
        if (clrPath.back() != '\\')
        {
            clrPath += L"\\";
        }

        clrPath += L"coreclr.dll";

        hCoreCLRModule = ::LoadLibraryExW(clrPath.c_str(), NULL, 0);
    }

    if (hCoreCLRModule == nullptr)
    {
        // Try the relative location based in install dir
        // ..\..\Runtime\x86
        hCoreCLRModule = ::LoadLibraryExW(L"..\\..\\Runtime\\x86\\coreclr.dll", NULL, 0);
    }

    if (hCoreCLRModule == nullptr)
    {
        // Try the relative location based in install

        hCoreCLRModule = ::LoadLibraryExW(L"coreclr.dll", NULL, 0);
    }

    return hCoreCLRModule;
}

extern "C" __declspec(dllexport) bool __stdcall CallApplicationMain(PCALL_APPLICATION_MAIN_DATA data)
{
    HRESULT hr = S_OK;
    FnGetCLRRuntimeHost pfnGetCLRRuntimeHost = nullptr;
    ICLRRuntimeHost2* pCLRRuntimeHost = nullptr;
    TCHAR szCurrentDirectory[MAX_PATH];
    TCHAR szCoreClrDirectory[MAX_PATH];
    TCHAR lpCoreClrModulePath[MAX_PATH];

    GetModuleDirectory(NULL, szCurrentDirectory);

    HMODULE hCoreCLRModule = LoadCoreClr();
    if (!hCoreCLRModule)
    {
        printf_s("Failed to locate coreclr.dll.\n");
        return false;
    }

    // Get the path to the module
    DWORD dwCoreClrModulePathSize = GetModuleFileName(hCoreCLRModule, lpCoreClrModulePath, MAX_PATH);
    lpCoreClrModulePath[dwCoreClrModulePathSize] = '\0';

    GetModuleDirectory(hCoreCLRModule, szCoreClrDirectory);

    HMODULE ignoreModule;
    // Pin the module - CoreCLR.dll does not support being unloaded.
    if (!::GetModuleHandleExW(GET_MODULE_HANDLE_EX_FLAG_PIN, lpCoreClrModulePath, &ignoreModule))
    {
        printf_s("Failed to pin coreclr.dll.\n");
        return false;
    }

    pfnGetCLRRuntimeHost = (FnGetCLRRuntimeHost)::GetProcAddress(hCoreCLRModule, "GetCLRRuntimeHost");
    if (!pfnGetCLRRuntimeHost)
    {
        printf_s("Failed to find export GetCLRRuntimeHost.\n");
        return false;
    }

    hr = pfnGetCLRRuntimeHost(IID_ICLRRuntimeHost2, (IUnknown**)&pCLRRuntimeHost);
    if (FAILED(hr))
    {
        printf_s("Failed to get IID_ICLRRuntimeHost2.\n");
        return false;
    }

    STARTUP_FLAGS dwStartupFlags = (STARTUP_FLAGS)(
        STARTUP_FLAGS::STARTUP_LOADER_OPTIMIZATION_SINGLE_DOMAIN |
        STARTUP_FLAGS::STARTUP_SINGLE_APPDOMAIN
        );

    pCLRRuntimeHost->SetStartupFlags(dwStartupFlags);

    // Authenticate with either CORECLR_HOST_AUTHENTICATION_KEY or CORECLR_HOST_AUTHENTICATION_KEY_NONGEN 
    hr = pCLRRuntimeHost->Authenticate(CORECLR_HOST_AUTHENTICATION_KEY);
    if (FAILED(hr))
    {
        printf_s("Failed to Authenticate().\n");
        return false;
    }

    hr = pCLRRuntimeHost->Start();

    if (FAILED(hr))
    {
        printf_s("Failed to Start().\n");
        return false;
    }

    const wchar_t* property_keys[] =
    {
        // Allowed property names:
        // APPBASE
        // - The base path of the application from which the exe and other assemblies will be loaded
        L"APPBASE",
        //
        // TRUSTED_PLATFORM_ASSEMBLIES
        // - The list of complete paths to each of the fully trusted assemblies
        L"TRUSTED_PLATFORM_ASSEMBLIES",
        //
        // APP_PATHS
        // - The list of paths which will be probed by the assembly loader
        L"APP_PATHS",
        //
        // APP_NI_PATHS
        // - The list of additional paths that the assembly loader will probe for ngen images
        //
        // NATIVE_DLL_SEARCH_DIRECTORIES
        // - The list of paths that will be probed for native DLLs called by PInvoke
        //
    };

    wstring trustedPlatformAssemblies(L"");

    for (const wchar_t * &assembly : trustedAssemblies) {
        trustedPlatformAssemblies += szCoreClrDirectory;
        trustedPlatformAssemblies += assembly;
        trustedPlatformAssemblies += L".dll;";
    }

    trustedPlatformAssemblies += szCurrentDirectory;
    trustedPlatformAssemblies += L"klr.core45.managed.dll";

    wstring appPaths(szCurrentDirectory);

    appPaths += L";";
    appPaths += szCoreClrDirectory;

    const wchar_t* property_values[] = {
        // APPBASE
        szCurrentDirectory, // TODO: Allow overriding this
        // TRUSTED_PLATFORM_ASSEMBLIES
        trustedPlatformAssemblies.c_str(),
        // APP_PATHS
        appPaths.c_str(),
    };

    DWORD domainId;
    DWORD dwFlagsAppDomain = APPDOMAIN_ENABLE_PLATFORM_SPECIFIC_APPS | APPDOMAIN_ENABLE_PINVOKE_AND_CLASSIC_COMINTEROP;
    LPCWSTR szAssemblyName = L"klr.core45.managed, Version=1.0.0.0";
    LPCWSTR szDomainManagerTypeName = L"DomainManager";
    LPCWSTR szMainMethodName = L"Main";

    int nprops = sizeof(property_keys) / sizeof(wchar_t*);

    hr = pCLRRuntimeHost->CreateAppDomainWithManager(
        L"klr.core45.managed",
        dwFlagsAppDomain,
        szAssemblyName,
        szDomainManagerTypeName,
        nprops,
        property_keys,
        property_values,
        &domainId);

    if (FAILED(hr))
    {
        printf_s("Failed to create app domain (%d).\n", hr);
        return false;
    }

    HostMain pHostMain;

    hr = pCLRRuntimeHost->CreateDelegate(
        domainId,
        szAssemblyName,
        szDomainManagerTypeName,
        szMainMethodName,
        (INT_PTR*)&pHostMain);

    if (FAILED(hr))
    {
        printf_s("Failed to create main delegate (%d).\n", hr);
        return false;
    }

    // Call main
    data->exitcode = pHostMain(data->argc, data->argv);

    pCLRRuntimeHost->UnloadAppDomain(domainId, true);

    pCLRRuntimeHost->Stop();

    return hr;
}