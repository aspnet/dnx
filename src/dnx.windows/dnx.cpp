// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

#include "stdafx.h"

int _tmain(int argc, _TCHAR* argv[])
{
    OSVERSIONINFO version_info;
    ZeroMemory(&version_info, sizeof(OSVERSIONINFO));
    version_info.dwOSVersionInfoSize = sizeof(OSVERSIONINFO);

#pragma warning(disable:4996)
    GetVersionEx(&version_info);
#pragma warning(default:4996)

    bool is_oneCore = version_info.dwMajorVersion >= 10;

    auto dnx_dll_name = is_oneCore ? L"dnx.onecore.dll" : L"dnx.win32.dll";

    auto dnx_dll = LoadLibraryEx(dnx_dll_name, NULL, LOAD_LIBRARY_SEARCH_DEFAULT_DIRS);
    if (!dnx_dll)
    {
        auto last_error = GetLastError();
        _tprintf(L"%s could not be loaded. Last error: %d\n", dnx_dll_name, last_error);
        return -1;
    }

    auto entry_point = (int (STDAPICALLTYPE*)(int, wchar_t **))GetProcAddress(dnx_dll, "DnxMain");
    if (!entry_point)
    {
        _tprintf(L"Getting entry point failed\n");
        return -1;
    }

    return entry_point(argc, argv);
}
