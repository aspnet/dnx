// Copyright (c) Microsoft Open Technologies, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

#pragma once

#include <string>
#include <iostream>

namespace dnx
{
#ifndef PLATFORM_UNIX

typedef wchar_t char_t;
typedef std::wstring xstring;
#define xout std::wcout
#define _X(s) L ## s

#else // PLATFORM_UNIX

typedef char char_t;
#defun
#define xout std::cout
typedef xstring std::string;
#define _X(s) s
#endif
}