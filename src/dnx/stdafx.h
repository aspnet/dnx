// Copyright (c) Microsoft Open Technologies, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

#pragma once

#include <stdio.h>
#include <stdlib.h>

#ifndef PLATFORM_UNIX
#include "targetver.h"

#include <tchar.h>
#include <strsafe.h>

#define WIN32_LEAN_AND_MEAN
#include <windows.h>
#else // PLATFORM_UNIX
#include <limits.h>
#include <stdarg.h>
#include <stddef.h>
#include <stdint.h>
#include <string.h>
#include <unistd.h>

typedef int BOOL;
typedef uint32_t DWORD;
typedef DWORD HRESULT;
typedef void* HMODULE;
typedef void* FARPROC;
typedef void* HANDLE;

#include "tchar.h"

#define SUCCEEDED(hr) (((HRESULT)(hr)) >= 0)

#define STDAPICALLTYPE
#define MAX_PATH PATH_MAX
#define S_OK 0
#define TRUE 1
#define FALSE 0

inline int max(int a, int b) { return a > b ? a : b; }
#endif // PLATFORM_UNIX
