// Copyright (c) Microsoft Open Technologies, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

#include "stdafx.h"
#include <assert.h>
#include <libproc.h>

LPTSTR GetNativeBootstrapperDirectory()
{
    LPTSTR szPath = (LPTSTR)calloc(PROC_PIDPATHINFO_MAXSIZE, sizeof(TCHAR));
    ssize_t ret = proc_pidpath(getpid(), szPath, PROC_PIDPATHINFO_MAXSIZE);

    assert(ret != -1);

    size_t lastSlash = 0;

    for (size_t i = 0; szPath[i] != _T('\0'); i++)
    {
        if (szPath[i] == _T('/'))
        {
            lastSlash = i;
        }
    }

    szPath[lastSlash] = _T('\0');

    return szPath;
}

void WaitForDebuggerToAttach()
{
    // TODO: Implement this.
}
