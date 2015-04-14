// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

#include "stdafx.h"
#include <string>
#include <assert.h>
#include <dlfcn.h>

std::string GetNativeBootstrapperDirectory()
{
    char buffer[PATH_MAX + 1];
    ssize_t ret = readlink("/proc/self/exe", buffer, PATH_MAX);

    assert(ret != -1);

    // readlink does not null terminate the path
    buffer[ret] = '\0';

    for (; ret > 0 && buffer[ret] != '/'; ret--)
        ;

    buffer[ret] = '\0';

    return std::string(buffer);
}

void WaitForDebuggerToAttach()
{
    // TODO: Implement this.  procfs will be able to tell us this.
}
