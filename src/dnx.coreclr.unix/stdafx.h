// Copyright (c) Microsoft Open Technologies, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

#include <dlfcn.h>
#include <limits.h>
#include <stdio.h>
#include <stdlib.h>
#include <string.h>
#include <string>
#include <unistd.h>

#if PLATFORM_DARWIN
#include <libproc.h>
#endif

typedef int BOOL;
typedef const char* LPCTSTR;

#define _T(str) str
