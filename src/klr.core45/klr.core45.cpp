// klr.core45.cpp : Defines the exported functions for the DLL application.
//

#include "stdafx.h"

#include "..\klr\klr.h"
#include "klr.core45.h"
#include "tpa.h"

#define TRUSTED_PLATFORM_ASSEMBLIES_STRING_BUFFER_SIZE_CCH (63 * 1024) //32K WCHARs
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

// Generate a list of trusted platform assembiles. 
bool GetTrustedPlatformAssembliesList(WCHAR* szDirectory, bool bNative, LPWSTR pszTrustedPlatformAssemblies, size_t cchTrustedPlatformAssemblies)
{
    bool ret = true;
    errno_t errno = 0;
    WIN32_FIND_DATA ffd = {};
    size_t cTpaAssemblyNames = 0;
    LPWSTR* ppszTpaAssemblyNames = nullptr;

    // Build the list of the tpa assemblie 
    CreateTpaBase(&ppszTpaAssemblyNames, &cTpaAssemblyNames, bNative);

    // Scan the directory to see if all the files in TPA list exist
    for (size_t i = 0; i < cTpaAssemblyNames; ++i)
    {
        WCHAR wszPattern[MAX_PATH];
        wszPattern[0] = L'\0';

        errno = wcscpy_s(wszPattern, _countof(wszPattern), szDirectory);
        CHECK_RETURN_VALUE_FAIL_EXIT_VIA_FINISHED_SETSTATE(errno, ret = false);

        errno = wcscat_s(wszPattern, _countof(wszPattern), ppszTpaAssemblyNames[i]);
        CHECK_RETURN_VALUE_FAIL_EXIT_VIA_FINISHED_SETSTATE(errno, ret = false);

        HANDLE findHandle = FindFirstFile(wszPattern, &ffd);

        if ((findHandle == INVALID_HANDLE_VALUE) ||
            (ffd.dwFileAttributes & FILE_ATTRIBUTE_DIRECTORY))
        {
            // if file is missing or a directory is found, breaks the loop and
            // set the missing flag to true
            ret = false;
            goto Finished;
        }
    }

    for (size_t i = 0; i < cTpaAssemblyNames; ++i)
    {
        errno = wcscat_s(pszTrustedPlatformAssemblies, cchTrustedPlatformAssemblies, szDirectory);
        CHECK_RETURN_VALUE_FAIL_EXIT_VIA_FINISHED_SETSTATE(errno, ret = false);

        errno = wcscat_s(pszTrustedPlatformAssemblies, cchTrustedPlatformAssemblies, ppszTpaAssemblyNames[i]);
        CHECK_RETURN_VALUE_FAIL_EXIT_VIA_FINISHED_SETSTATE(errno, ret = false);

        errno = wcscat_s(pszTrustedPlatformAssemblies, cchTrustedPlatformAssemblies, L";");
        CHECK_RETURN_VALUE_FAIL_EXIT_VIA_FINISHED_SETSTATE(errno, ret = false);
    }

Finished:
    if (ppszTpaAssemblyNames != nullptr)
    {
        FreeTpaBase(ppszTpaAssemblyNames, cTpaAssemblyNames);
    }

    return ret;
}

bool KlrLoadLibraryExWAndGetProcAddress(
            LPWSTR   pwszModuleFileName, 
            LPCSTR   pszFunctionName, 
            HMODULE* phModule, 
            FARPROC* ppFunction)
{
    bool fSuccess = true;
    HMODULE hModule = nullptr; 
    FARPROC pFunction = nullptr;
    
    //Clear out params
    *phModule = nullptr;
    *(FARPROC*)ppFunction = nullptr;
    
    //Load module and look for require DLL export
    hModule = ::LoadLibraryExW(pwszModuleFileName, NULL, LOAD_LIBRARY_SEARCH_DEFAULT_DIRS);
    if (!hModule)
    {
        ::wprintf_s(L"Failed to load: %s\r\n", pwszModuleFileName);
        hModule = nullptr;
        fSuccess = false;
        goto Finished;
    }
    
    pFunction = ::GetProcAddress(hModule, pszFunctionName);
    if (!pFunction)
    {
        ::wprintf_s(L"Failed to find function %S in %s\n", pszFunctionName, pwszModuleFileName);
        fSuccess = false;
        goto Finished;
    }

Finished:
    //Cleanup
    if (fSuccess)
    {
        *phModule = hModule;
        *(FARPROC*)ppFunction = pFunction;
    }
    else
    {
        if (hModule)
        {
            FreeLibrary(hModule);
            hModule = nullptr;
        }
    }

    return fSuccess;
}

HMODULE LoadCoreClr()
{
    errno_t errno = 0;
    bool fSuccess = true;
    TCHAR szKreTrace[1] = {};
    bool m_fVerboseTrace = GetEnvironmentVariableW(L"KRE_TRACE", szKreTrace, 1) > 0;
    LPWSTR rgwzOSLoaderModuleNames[] = {
                        L"api-ms-win-core-libraryloader-l1-1-1.dll", 
                        L"kernel32.dll", 
                        NULL
    };
    LPWSTR rgwszModuleFileName = NULL;
    DWORD dwModuleFileName = 0;

    HMODULE hOSLoaderModule = nullptr;

    // Note: need to keep as ASCII as GetProcAddress function takes ASCII params
    LPCSTR pszAddDllDirectoryName = "AddDllDirectory";
    FnAddDllDirectory pFnAddDllDirectory = nullptr;

    LPCSTR pszSetDefaultDllDirectoriesName = "SetDefaultDllDirectories";
    FnSetDefaultDllDirectories pFnSetDefaultDllDirectories = nullptr;

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

        //Scan through module name list looking for a valid module that has first DLL export
        rgwszModuleFileName = rgwzOSLoaderModuleNames[dwModuleFileName];
        while (rgwszModuleFileName != NULL)
        {
            fSuccess = KlrLoadLibraryExWAndGetProcAddress(
                            rgwszModuleFileName, 
                            pszAddDllDirectoryName, 
                            &hOSLoaderModule, 
                            (FARPROC*)&pFnAddDllDirectory);
            if (fSuccess)
                break;
            
            dwModuleFileName++;
            rgwszModuleFileName = rgwzOSLoaderModuleNames[dwModuleFileName];
         }
         
        if (!hOSLoaderModule || !pFnAddDllDirectory)
        {
            fSuccess = false;
            goto Finished;
        }
         
        //Find the second DLL export
        pFnSetDefaultDllDirectories = (FnSetDefaultDllDirectories)::GetProcAddress(hOSLoaderModule, pszSetDefaultDllDirectoriesName);
        if (!pFnSetDefaultDllDirectories)
        {
            if (m_fVerboseTrace)
                ::wprintf_s(L"Failed to find function %S in %s\n", pszSetDefaultDllDirectoriesName, rgwszModuleFileName);

            fSuccess = false;
            goto Finished;
        }

        //Verify HANDLE and two DLL exports are valid before proceeding
        if (!hOSLoaderModule || !pFnAddDllDirectory || !pFnSetDefaultDllDirectories)
        {
            fSuccess = false;
            goto Finished;
        }
        
        pFnAddDllDirectory(szCoreClrDirectory);
        // Modify the default dll flags so that dependencies can be found in this path
        pFnSetDefaultDllDirectories(LOAD_LIBRARY_SEARCH_DEFAULT_DIRS | LOAD_LIBRARY_SEARCH_USER_DIRS);
        
        fSuccess = true;

        // Continue loading as usual
        hCoreCLRModule = ::LoadLibraryExW(wszClrPath, NULL, 0);
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


/*
    Win2KDisable : DisallowWin32kSystemCalls
    SET KRE_WIN32K_DISABLE=1
*/

bool Win32KDisable()
{
    bool fSuccess = true;
    TCHAR szKreWin32KDisable[2] = {};
    LPWSTR lpwszModuleFileName = L"api-ms-win-core-processthreads-l1-1-1.dll";
    DWORD dwModuleFileName = 0;
    HMODULE hProcessThreadsModule = nullptr;
    // Note: Need to keep as ASCII as GetProcAddress function takes ASCII params
    LPCSTR pszSetProcessMitigationPolicy = "SetProcessMitigationPolicy";
    FnSetProcessMitigationPolicy pFnSetProcessMitigationPolicy = nullptr;
    PROCESS_MITIGATION_SYSTEM_CALL_DISABLE_POLICY systemCallDisablePolicy = {};
    systemCallDisablePolicy.DisallowWin32kSystemCalls = 1;

    fSuccess = GetEnvironmentVariableW(L"KRE_WIN32K_DISABLE", szKreWin32KDisable, _countof(szKreWin32KDisable)) > 0;
    if (!fSuccess)
    {
        goto Finished;
    }
    
    if (wcscmp(szKreWin32KDisable, L"1") != 0)
    {
        fSuccess = false;
        goto Finished;
    }

    fSuccess = KlrLoadLibraryExWAndGetProcAddress(
                    lpwszModuleFileName, 
                    pszSetProcessMitigationPolicy, 
                    &hProcessThreadsModule, 
                    (FARPROC*)&pFnSetProcessMitigationPolicy);
    if (!fSuccess)
    {
        goto Finished;
    }
    
    if (!hProcessThreadsModule || !pFnSetProcessMitigationPolicy)
    {
        fSuccess = false;
        goto Finished;
    }

    if (pFnSetProcessMitigationPolicy(
              ProcessSystemCallDisablePolicy,   //_In_  PROCESS_MITIGATION_POLICY MitigationPolicy,
              &systemCallDisablePolicy,         //_In_  PVOID lpBuffer,
              sizeof(systemCallDisablePolicy)  //_In_  SIZE_T dwLength
            ))
    {
        printf_s("KRE_WIN32K_DISABLE successful.\n");
    }

Finished:
    //Cleanup
    if (pFnSetProcessMitigationPolicy)
    {
        pFnSetProcessMitigationPolicy = nullptr;
    }
    
    if (hProcessThreadsModule)
    {
        FreeLibrary(hProcessThreadsModule);
        hProcessThreadsModule = nullptr;
    }

    return fSuccess;
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

    Win32KDisable();

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
    if (!GetTrustedPlatformAssembliesList(szCoreClrDirectory, true, pwszTrustedPlatformAssemblies, cchTrustedPlatformAssemblies))
    {
        if (!GetTrustedPlatformAssembliesList(szCoreClrDirectory, false, pwszTrustedPlatformAssemblies, cchTrustedPlatformAssemblies))
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

    LPCWSTR szAssemblyName = L"klr.core45.managed, Version=0.1.0.0";
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
        wprintf_s(L"TPA      %d %S\n", wcslen(pwszTrustedPlatformAssemblies), pwszTrustedPlatformAssemblies);
        wprintf_s(L"AppPaths %S\n", wszAppPaths);
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

    SetEnvironmentVariable(L"KRE_FRAMEWORK", L"aspnetcore50");

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