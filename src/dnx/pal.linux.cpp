// Copyright (c) Microsoft Open Technologies, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

#include "stdafx.h"
#include <assert.h>
#include <dlfcn.h>

LPTSTR GetNativeBootstrapperDirectory()
{
    LPTSTR szPath = new TCHAR[PATH_MAX + 1];
    ssize_t ret = readlink("/proc/self/exe", szPath, PATH_MAX);

    assert(ret != -1);

    // readlink does not null terminate the path
    szPath[ret] = _T('\0');

    for (; ret > 0 && szPath[ret] != _T('/'); ret--);
    szPath[ret] = _T('\0');

    return szPath;
}

void WaitForDebuggerToAttach()
{
    // TODO: Implement this.  procfs will be able to tell us this.
}
