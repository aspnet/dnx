// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

#pragma once

#include <string>
#include <iostream>

namespace dnx
{

#if defined(_WIN32)

typedef wchar_t char_t;
typedef std::wstring xstring_t;
#define xout std::wcout
#define _X(s) L ## s

#define PATH_SEPARATOR L"\\"

#define x_strlen wcslen

#else // non-windows

typedef char char_t;
typedef std::string xstring_t;
#define xout std::cout
#define _X(s) s

#define PATH_SEPARATOR "/"

#define x_strlen strlen

#endif
}
