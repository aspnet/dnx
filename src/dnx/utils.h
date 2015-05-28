// Copyright (c) Microsoft Open Technologies, Inc. All rights reserved.
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

        dnx::xstring_t path_combine(const dnx::xstring_t& path1, const dnx::xstring_t& path2);
    }
}