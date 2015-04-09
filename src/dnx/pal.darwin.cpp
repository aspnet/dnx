// Copyright (c) Microsoft Open Technologies, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

#include "stdafx.h"
#include <assert.h>
#include <libproc.h>

LPTSTR GetNativeBootstrapperDirectory()
{
    LPTSTR szPath = new TCHAR[PROC_PIDPATHINFO_MAXSIZE];
    ssize_t ret = proc_pidpath(getpid(), szPath, PROC_PIDPATHINFO_MAXSIZE);

    assert(ret != -1);

    for (; ret > 0 && szPath[ret] != _T('/'); ret--);
    szPath[ret] = _T('\0');

    return szPath;
}

void WaitForDebuggerToAttach()
{
    // TODO: Implement this.
}
