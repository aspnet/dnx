// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

#pragma once

#include "targetver.h"

#define _CRT_SECURE_NO_WARNINGS

#include <tchar.h>

// Windows Headers
// Exclude rarely-used stuff from Windows headers
#define WIN32_LEAN_AND_MEAN
#include <windows.h>

// CRT Headers
#include <stdio.h>
#include <strsafe.h>

// CLR Headers
#include "mscoree.h"
