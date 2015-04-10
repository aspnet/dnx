// Copyright (c) Microsoft Open Technologies, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

#pragma once

#include "xplat.h"
#include <string>

namespace dnx
{
    namespace utils
    {
        std::string to_std_string(std::string s);
        std::string to_std_string(std::wstring s);
    }
}