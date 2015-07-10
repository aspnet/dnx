// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

#include "stdafx.h"
#include "xplat.h"

bool ExpandCommandLineArguments(int nArgc, dnx::char_t** ppszArgv, int& nExpandedArgc, dnx::char_t**& ppszExpandedArgv);

TEST(parameter_expansion, ExpandCommandLineArguments_returns_false_when_no_params)
{
    int expanded_arg_count;
    dnx::char_t** expanded_argv = nullptr;
    ASSERT_FALSE(ExpandCommandLineArguments(0, nullptr, expanded_arg_count, expanded_argv));
    ASSERT_EQ(nullptr, expanded_argv);
}