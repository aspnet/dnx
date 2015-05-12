// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

#include "stdafx.h"

int _tmain(int /*argc*/, _TCHAR* /*argv[]*/)
{
    OSVERSIONINFO version_info;
    ZeroMemory(&version_info, sizeof(OSVERSIONINFO));
    version_info.dwOSVersionInfoSize = sizeof(OSVERSIONINFO);

#pragma warning(disable:4996)
    GetVersionEx(&version_info);
#pragma warning(default:4996)

    bool is_oneCore = version_info.dwMajorVersion >= 6 && version_info.dwMinorVersion >= 2;

    _tprintf(L"is one core %d: ", is_oneCore);

    return 0;
}
