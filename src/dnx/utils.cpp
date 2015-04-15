// Copyright (c) Microsoft Open Technologies, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

#include "stdafx.h"

#include "xplat.h"
#include <string>

// <codecvt> not supported in libstdc++ (gcc, Clang) but conversions from wstring are only
// meant to be used on Windows
#ifndef PLATFORM_UNIX

#include <locale>
#include <codecvt>

#endif

namespace dnx
{
    namespace utils
    {
        std::string to_string(const std::string& s)
        {
            return s;
        }

        // std::string <--> std::wstring conversion functions are not general purpose and should be
        // used only to convert strings containing ASCII characters
        dnx::xstring_t to_xstring_t(const std::string& s)
        {
#ifndef PLATFORM_UNIX
            return std::wstring_convert<std::codecvt_utf8_utf16<wchar_t>>{}.from_bytes(s);
#else
            return s;
#endif
        }

        // conversions from wstring are only meant to be use on Windows
#ifndef PLATFORM_UNIX
        std::string to_string(const std::wstring& s)
        {
            return std::wstring_convert<std::codecvt_utf8_utf16<wchar_t>>{}.to_bytes(s);
        }

        dnx::xstring_t to_xstring_t(const std::wstring& s)
        {
            return s;
        }

        std::wstring to_wstring(const std::string& s)
        {
            return to_xstring_t(s);
        }
#endif
    }
}
