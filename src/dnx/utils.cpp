// Copyright (c) Microsoft Open Technologies, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

#include "stdafx.h"

#include "xplat.h"
#include <string>
#include <locale>
#include <codecvt>

namespace dnx
{
    namespace utils
    {
        std::string to_std_string(std::string s)
        {
            return s;
        }

        std::string to_std_string(std::wstring s)
        {
            std::wstring_convert<std::codecvt_utf8_utf16<wchar_t>> converter;
            return converter.to_bytes(s);
        }
    }
}