// klr.net45.cpp : Defines the exported functions for the DLL application.
//

#include "stdafx.h"
#include <string>

#include "..\klr\klr.h"

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
#if AMD64
        hCoreCLRModule = ::LoadLibraryExW(L"..\\..\\..\\Runtime\\amd64\\coreclr.dll", NULL, 0);
#else
        hCoreCLRModule = ::LoadLibraryExW(L"..\\..\\..\\Runtime\\x86\\coreclr.dll", NULL, 0);
#endif
    }

    if (hCoreCLRModule == nullptr)
    {
        // This is used when developing
#if AMD64
        hCoreCLRModule = ::LoadLibraryExW(L"..\\..\\..\\artifacts\\build\\ProjectK\\Runtime\\amd64\\coreclr.dll", NULL, 0);
#else
        hCoreCLRModule = ::LoadLibraryExW(L"..\\..\\..\\artifacts\\build\\ProjectK\\Runtime\\x86\\coreclr.dll", NULL, 0);
#endif
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

    if (data->klrDirectory) {
        wcscpy_s(szCurrentDirectory, data->klrDirectory);
    } else {
        GetModuleDirectory(NULL, szCurrentDirectory);
    }

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

    // Enumerate the core clr directory and add each .dll or .ni.dll to the list of trusted assemblies
    wstring pattern(szCoreClrDirectory);
    pattern += L"*.dll";

    WIN32_FIND_DATA ffd;
    HANDLE findHandle = FindFirstFile(pattern.c_str(), &ffd);

    if (INVALID_HANDLE_VALUE == findHandle)
    {
        printf_s("Failed to find files in the coreclr directory\n");
        return -1;
    }

    do
    {
        if (ffd.dwFileAttributes & FILE_ATTRIBUTE_DIRECTORY)
        {
            // Skip directories
        }
        else
        {
            trustedPlatformAssemblies += szCoreClrDirectory;
            trustedPlatformAssemblies += ffd.cFileName;
            trustedPlatformAssemblies += L";";
        }

    } while (FindNextFile(findHandle, &ffd) != 0);

    // Add the assembly containing the app domain manager to the trusted list
    trustedPlatformAssemblies += szCurrentDirectory;
    trustedPlatformAssemblies += L"klr.core45.managed.dll";

    wstring appPaths(szCurrentDirectory);

    appPaths += L";";
    appPaths += szCoreClrDirectory;

    const wchar_t* property_values[] = {
        // APPBASE
        data->applicationBase,
        // TRUSTED_PLATFORM_ASSEMBLIES
        trustedPlatformAssemblies.c_str(),
        // APP_PATHS
        appPaths.c_str(),
    };

    DWORD domainId;
    DWORD dwFlagsAppDomain =
        APPDOMAIN_ENABLE_PLATFORM_SPECIFIC_APPS |
        APPDOMAIN_ENABLE_PINVOKE_AND_CLASSIC_COMINTEROP |
        APPDOMAIN_ENABLE_ASSEMBLY_LOADFILE;

    LPCWSTR szAssemblyName = L"klr.core45.managed, Version=1.0.0.0";
    LPCWSTR szEntryPointTypeName = L"DomainManager";
    LPCWSTR szMainMethodName = L"Execute";

    int nprops = sizeof(property_keys) / sizeof(wchar_t*);

    hr = pCLRRuntimeHost->CreateAppDomainWithManager(
        L"klr.core45.managed",
        dwFlagsAppDomain,
        NULL,
        NULL,
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
        szEntryPointTypeName,
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