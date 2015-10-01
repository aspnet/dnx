// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

#include "stdafx.h"

HMODULE load_dnx_dll(const wchar_t* dnx_dll_name)
{
    auto dnx_dll = LoadLibraryEx(dnx_dll_name, NULL, LOAD_LIBRARY_SEARCH_DEFAULT_DIRS);
    if (!dnx_dll)
    {
        auto last_error = GetLastError();
        _tprintf(L"%s could not be loaded. Last error: %d\n", dnx_dll_name, last_error);
    }

    return dnx_dll;
}

int _tmain(int argc, _TCHAR* argv[])
{
    OSVERSIONINFO version_info;
    ZeroMemory(&version_info, sizeof(OSVERSIONINFO));
    version_info.dwOSVersionInfoSize = sizeof(OSVERSIONINFO);

#pragma warning(disable:4996)
    GetVersionEx(&version_info);
#pragma warning(default:4996)

    bool is_oneCore = version_info.dwMajorVersion >= 10;

    HMODULE dnx_dll = nullptr;

    if (is_oneCore)
    {
        dnx_dll = load_dnx_dll(L"dnx.onecore.dll");
    }

    if (!dnx_dll)
    {
        if (is_oneCore)
        {
            _tprintf(L"Falling back to loading dnx.win32.dll\n");
        }

        dnx_dll = load_dnx_dll(L"dnx.win32.dll");
    }

    if (!dnx_dll)
    {
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
