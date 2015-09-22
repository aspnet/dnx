// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

#pragma once

#include "xplat.h"
#include <string>

namespace dnx
{
    namespace utils
    {
        std::string to_string(const std::string& s);
        std::string to_string(const std::wstring& s);
        dnx::xstring_t to_xstring_t(const std::string& s);
        dnx::xstring_t to_xstring_t(const std::wstring& s);
        std::wstring to_wstring(const std::string& s);
        bool strings_equal_ignore_case(const dnx::char_t* s1, const dnx::char_t* s2);

        dnx::xstring_t path_combine(const dnx::xstring_t& path1, const dnx::xstring_t& path2);
        bool file_exists(const dnx::xstring_t& path);
        bool directory_exists(const dnx::xstring_t& path);
        dnx::xstring_t remove_file_from_path(const dnx::xstring_t& path);

#if defined(_WIN32)
        const wchar_t* get_windows_version();
#endif
        int find_bootstrapper_option_index(int argc, dnx::char_t**argv, const dnx::char_t* optionName);
        int find_first_non_bootstrapper_param_index(int argc, dnx::char_t**argv);
        dnx::char_t* get_option_value(int argc, dnx::char_t* argv[], const dnx::char_t* optionName);
    }
}