// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

typedef char TCHAR;
typedef char* LPTSTR;
typedef const char* LPCTSTR;
typedef const char* LPCSTR;

#define _T(x) x

#define _tcsnicmp strncasecmp
#define _tcsicmp strcasecmp
#define _tcsnlen strnlen
#define _tprintf_s printf_s

int printf_s(LPCTSTR format, ...);
int _tcscpy_s(LPTSTR strDestination, size_t numberOfElements, LPCTSTR strSrc);
