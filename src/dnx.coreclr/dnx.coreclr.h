// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

/*
    AddDllDirectory
    See: http://msdn.microsoft.com/en-us/library/windows/desktop/hh310513(v=vs.85).aspx
    Windows 7, Windows Server 2008 R2, Windows Vista, and Windows Server 2008:  To use this function in an application, call GetProcAddress to retrieve the function's address from Kernel32.dll. KB2533623 must be installed on the target platform.

     DLL_DIRECTORY_COOKIE  WINAPI AddDllDirectory(
      _In_  PCWSTR NewDirectory
    );

    SetDefaultDllDirectories
    See: http://msdn.microsoft.com/en-us/library/windows/desktop/hh310515(v=vs.85).aspx

    BOOL  WINAPI SetDefaultDllDirectories(
      _In_  DWORD DirectoryFlags
    );
*/

typedef PVOID DLL_DIRECTORY_COOKIE, *PDLL_DIRECTORY_COOKIE;

typedef DLL_DIRECTORY_COOKIE (WINAPI *FnAddDllDirectory)(
    _In_  PCWSTR NewDirectory
    );

typedef BOOL (WINAPI *FnSetDefaultDllDirectories)(
  _In_  DWORD DirectoryFlags
);

#define LOAD_LIBRARY_SEARCH_DEFAULT_DIRS 0x00001000
#define LOAD_LIBRARY_SEARCH_USER_DIRS 0x00000400

/*
    typedef struct _PROCESS_MITIGATION_SYSTEM_CALL_DISABLE_POLICY {
      union {
        DWORD  Flags;
        struct {
          DWORD DisallowWin32kSystemCalls : 1;
          DWORD ReservedFlags : 31;
        };
      };
    } PROCESS_MITIGATION_SYSTEM_CALL_DISABLE_POLICY, *PPROCESS_MITIGATION_SYSTEM_CALL_DISABLE_POLICY;

    typedef enum _PROCESS_MITIGATION_POLICY {
      ProcessDEPPolicy                  = 0,
      ProcessASLRPolicy                 = 1,
      ProcessReserved1MitigationPolicy  = 2,
      ProcessStrictHandleCheckPolicy    = 3,
      ProcessSystemCallDisablePolicy    = 4,
      MaxProcessMitigationPolicy        = 5
    } PROCESS_MITIGATION_POLICY, *PPROCESS_MITIGATION_POLICY;

    BOOL WINAPI SetProcessMitigationPolicy(
      _In_  PROCESS_MITIGATION_POLICY MitigationPolicy,
      _In_  PVOID lpBuffer,
      _In_  SIZE_T dwLength
    );
*/

typedef BOOL (WINAPI *FnSetProcessMitigationPolicy)(
      _In_  PROCESS_MITIGATION_POLICY MitigationPolicy,
      _In_  PVOID lpBuffer,
      _In_  SIZE_T dwLength);
