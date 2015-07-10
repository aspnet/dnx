// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

#include "stdafx.h"
#include "xplat.h"

bool ExpandCommandLineArguments(int nArgc, dnx::char_t** ppszArgv, int& nExpandedArgc, dnx::char_t**& ppszExpandedArgv);
void FreeExpandedCommandLineArguments(int nArgc, dnx::char_t** ppszArgv);

TEST(parameter_expansion, ExpandCommandLineArguments_returns_false_when_no_params)
{
    int expanded_arg_count;
    dnx::char_t** expanded_args = nullptr;
    ASSERT_FALSE(ExpandCommandLineArguments(0, nullptr, expanded_arg_count, expanded_args));
    ASSERT_EQ(nullptr, expanded_args);
}

TEST(parameter_expansion, ExpandCommandLineArguments_should_ignore_appbase_after_bootstrapper_commands)
{
    int expanded_arg_count;
    dnx::char_t** expanded_args = nullptr;
    dnx::char_t* args[]{ _X("."), _X("run"), _X("--appbase"), _X("C:\\temp") };
    ASSERT_TRUE(ExpandCommandLineArguments(4, args, expanded_arg_count, expanded_args));
    ASSERT_EQ(6, expanded_arg_count);
    ASSERT_STREQ(_X("--appbase"), expanded_args[0]);
    ASSERT_STREQ(_X("."), expanded_args[1]);
    ASSERT_STREQ(_X("Microsoft.Framework.ApplicationHost"), expanded_args[2]);
    ASSERT_STREQ(_X("run"), expanded_args[3]);
    ASSERT_STREQ(_X("--appbase"), expanded_args[4]);
    ASSERT_STREQ(_X("C:\\temp"), expanded_args[5]);

    FreeExpandedCommandLineArguments(expanded_arg_count, expanded_args);
}