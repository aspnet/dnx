// klr.core45.cpp : Defines the exported functions for the DLL application.
//

#include "stdafx.h"

#include "..\klr\klr.h"

#define TRUSTED_PLATFORM_ASSEMBLIES_STRING_BUFFER_SIZE_CCH (32 * 1024) //32K WCHARs
#define CHECK_RETURN_VALUE_FAIL_EXIT_VIA_FINISHED(errno) { if (errno) { goto Finished;}}
#define CHECK_RETURN_VALUE_FAIL_EXIT_VIA_FINISHED_SETSTATE(errno,SET_EXIT_STATE) { if (errno) { SET_EXIT_STATE; goto Finished;}}

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

bool ScanDirectory(WCHAR* szDirectory, WCHAR* szPattern, LPWSTR pszTrustedPlatformAssemblies, size_t cchTrustedPlatformAssemblies)
{
    bool ret = true;
    errno_t errno = 0;
    WIN32_FIND_DATA ffd = {};
    
    WCHAR wszPattern[MAX_PATH];
    wszPattern[0] = L'\0';
    
    errno = wcscpy_s(wszPattern, _countof(wszPattern), szDirectory);
    CHECK_RETURN_VALUE_FAIL_EXIT_VIA_FINISHED_SETSTATE(errno, ret = false);

    errno = wcscat_s(wszPattern, _countof(wszPattern), szPattern);
    CHECK_RETURN_VALUE_FAIL_EXIT_VIA_FINISHED_SETSTATE(errno, ret = false);
    
    HANDLE findHandle = FindFirstFile(wszPattern, &ffd);

    if (INVALID_HANDLE_VALUE == findHandle)
    {
        ret = false;
        goto Finished;
    }

    do
    {
        if (ffd.dwFileAttributes & FILE_ATTRIBUTE_DIRECTORY)
        {
            // Skip directories
        }
        else
        {
            errno = wcscat_s(pszTrustedPlatformAssemblies, cchTrustedPlatformAssemblies, szDirectory);
            CHECK_RETURN_VALUE_FAIL_EXIT_VIA_FINISHED_SETSTATE(errno, ret = false);
            
            errno = wcscat_s(pszTrustedPlatformAssemblies, cchTrustedPlatformAssemblies, ffd.cFileName);
            CHECK_RETURN_VALUE_FAIL_EXIT_VIA_FINISHED_SETSTATE(errno, ret = false);
            
            errno = wcscat_s(pszTrustedPlatformAssemblies, cchTrustedPlatformAssemblies, L";");
            CHECK_RETURN_VALUE_FAIL_EXIT_VIA_FINISHED_SETSTATE(errno, ret = false);
        }

    } while (FindNextFile(findHandle, &ffd) != 0);

Finished:
    return ret;
}

HMODULE LoadCoreClr()
{
    errno_t errno = 0;

    TCHAR szCoreClrDirectory[MAX_PATH];
    DWORD dwCoreClrDirectory = GetEnvironmentVariableW(L"CORECLR_DIR", szCoreClrDirectory, MAX_PATH);
    HMODULE hCoreCLRModule = nullptr;

    if (dwCoreClrDirectory != 0)
    {
        WCHAR wszClrPath[MAX_PATH];
        wszClrPath[0] = L'\0';

        errno = wcscpy_s(wszClrPath, _countof(wszClrPath), szCoreClrDirectory);
        CHECK_RETURN_VALUE_FAIL_EXIT_VIA_FINISHED(errno);

        if (wszClrPath[wcslen(wszClrPath) - 1] != L'\\')
        {
            errno = wcscat_s(wszClrPath, _countof(wszClrPath), L"\\");
            CHECK_RETURN_VALUE_FAIL_EXIT_VIA_FINISHED(errno);
        }

        errno = wcscat_s(wszClrPath, _countof(wszClrPath), L"coreclr.dll");
        CHECK_RETURN_VALUE_FAIL_EXIT_VIA_FINISHED(errno);

        hCoreCLRModule = ::LoadLibraryExW(wszClrPath, NULL, 0);
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

Finished:
    return hCoreCLRModule;
}

extern "C" __declspec(dllexport) bool __stdcall CallApplicationMain(PCALL_APPLICATION_MAIN_DATA data)
{
    HRESULT hr = S_OK;
    errno_t errno = 0;
    FnGetCLRRuntimeHost pfnGetCLRRuntimeHost = nullptr;
    ICLRRuntimeHost2* pCLRRuntimeHost = nullptr;
    TCHAR szCurrentDirectory[MAX_PATH];
    TCHAR szCoreClrDirectory[MAX_PATH];
    TCHAR lpCoreClrModulePath[MAX_PATH];
    size_t cchTrustedPlatformAssemblies = 0;
    LPWSTR pwszTrustedPlatformAssemblies = nullptr;

    if (data->klrDirectory) {
        errno = wcscpy_s(szCurrentDirectory, data->klrDirectory);
        CHECK_RETURN_VALUE_FAIL_EXIT_VIA_FINISHED(errno);
    }
    else {
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
        STARTUP_FLAGS::STARTUP_SINGLE_APPDOMAIN |
        STARTUP_FLAGS::STARTUP_SERVER_GC
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

    cchTrustedPlatformAssemblies = TRUSTED_PLATFORM_ASSEMBLIES_STRING_BUFFER_SIZE_CCH;
    pwszTrustedPlatformAssemblies = (LPWSTR)calloc(cchTrustedPlatformAssemblies+1, sizeof(WCHAR));
    if (pwszTrustedPlatformAssemblies == NULL)
    {
        goto Finished;
    }
    pwszTrustedPlatformAssemblies[0] = L'\0';
    
    // Try native images first
    if (!ScanDirectory(szCoreClrDirectory, L"*.ni.dll", pwszTrustedPlatformAssemblies, cchTrustedPlatformAssemblies))
    {
        if (!ScanDirectory(szCoreClrDirectory, L"*.dll", pwszTrustedPlatformAssemblies, cchTrustedPlatformAssemblies))
        {
            printf_s("Failed to find files in the coreclr directory\n");
            return false;
        }
    }

    // Add the assembly containing the app domain manager to the trusted list

    errno = wcscat_s(pwszTrustedPlatformAssemblies, cchTrustedPlatformAssemblies, szCurrentDirectory);
    CHECK_RETURN_VALUE_FAIL_EXIT_VIA_FINISHED(errno);

    errno = wcscat_s(pwszTrustedPlatformAssemblies, cchTrustedPlatformAssemblies, L"klr.core45.managed.dll");
    CHECK_RETURN_VALUE_FAIL_EXIT_VIA_FINISHED(errno);

    //wstring appPaths(szCurrentDirectory);
    WCHAR wszAppPaths[MAX_PATH];
    wszAppPaths[0] = L'\0';

    errno = wcscat_s(wszAppPaths, _countof(wszAppPaths), szCurrentDirectory);
    CHECK_RETURN_VALUE_FAIL_EXIT_VIA_FINISHED(errno);

    errno = wcscat_s(wszAppPaths, _countof(wszAppPaths), L";");
    CHECK_RETURN_VALUE_FAIL_EXIT_VIA_FINISHED(errno);

    errno = wcscat_s(wszAppPaths, _countof(wszAppPaths), szCoreClrDirectory);
    CHECK_RETURN_VALUE_FAIL_EXIT_VIA_FINISHED(errno);

    errno = wcscat_s(wszAppPaths, _countof(wszAppPaths), L";");
    CHECK_RETURN_VALUE_FAIL_EXIT_VIA_FINISHED(errno);

    const wchar_t* property_values[] = {
        // APPBASE
        data->applicationBase,
        // TRUSTED_PLATFORM_ASSEMBLIES
        pwszTrustedPlatformAssemblies,
        // APP_PATHS
        wszAppPaths,
    };

    DWORD domainId;
    DWORD dwFlagsAppDomain =
        APPDOMAIN_ENABLE_PLATFORM_SPECIFIC_APPS |
        APPDOMAIN_ENABLE_PINVOKE_AND_CLASSIC_COMINTEROP;

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
        wprintf_s(L"TPA      %d %s\n", wcslen(pwszTrustedPlatformAssemblies), pwszTrustedPlatformAssemblies);
        wprintf_s(L"AppPaths %s\n", wszAppPaths);
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

    // REVIEW: Versioning? k 1.0, 2.0?
    SetEnvironmentVariable(L"TARGET_FRAMEWORK", L"k10");

    // Call main
    data->exitcode = pHostMain(data->argc, data->argv);

    pCLRRuntimeHost->UnloadAppDomain(domainId, true);

    pCLRRuntimeHost->Stop();
    
Finished:    
    if (pwszTrustedPlatformAssemblies != NULL)
    {
        free(pwszTrustedPlatformAssemblies);
        pwszTrustedPlatformAssemblies = NULL;
    }

    if (FAILED(hr))
    {
        return false;
    }
    else
    {
        return true;
    }
}