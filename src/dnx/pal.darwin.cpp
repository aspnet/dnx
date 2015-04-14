// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

#include "stdafx.h"
#include <string>
#include <assert.h>
#include <libproc.h>

std::string GetNativeBootstrapperDirectory()
{
    char buffer[PROC_PIDPATHINFO_MAXSIZE];
    ssize_t ret = proc_pidpath(getpid(), buffer, PROC_PIDPATHINFO_MAXSIZE);

    assert(ret != -1);

    for (; ret > 0 && buffer[ret] != '/'; ret--)
        ;

    buffer[ret] = '\0';

    return std::string(buffer);
}

void WaitForDebuggerToAttach()
{
    // TODO: Implement this.
}
