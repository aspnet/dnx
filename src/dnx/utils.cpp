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

        xstring_t path_combine(const xstring_t& path1, const xstring_t& path2)
        {
            if (path1.length() == 0)
            {
                return path2;
            }

            if (path2.length() == 0)
            {
                return path1;
            }

            xstring_t path{ path1 };

            if (path[path.length() - 1] == _X('\\') || path[path.length() - 1] == _X('/'))
            {
                path.resize(path.length() - 1);
            }

            return path + PATH_SEPARATOR + (path2[0] == _X('\\') || path2[0] == _X('/') ? path2.substr(1) : path2);
        }
    }
}
