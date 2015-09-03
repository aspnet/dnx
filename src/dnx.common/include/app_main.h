#pragma once

// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

#include "xplat.h"

typedef struct CALL_APPLICATION_MAIN_DATA
{
    const dnx::char_t* applicationBase; // application base of managed domain
    const dnx::char_t* runtimeDirectory; // path to runtime helper directory
    int argc; // Number of args in argv
    const dnx::char_t** argv; // Array of arguments
    int exitcode; // Exit code from Managed Application
} *PCALL_APPLICATION_MAIN_DATA;

#if defined(_WIN32)
    typedef HRESULT(__stdcall
#else
    typedef int32_t(
#endif
    *FnCallApplicationMain)(
    PCALL_APPLICATION_MAIN_DATA pCallApplicationMainData);
