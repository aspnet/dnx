// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

#include "stdafx.h"
#include "xplat.h"
#include <vector>
#include <unordered_map>

bool ExpandCommandLineArguments(size_t nArgc, dnx::char_t** ppszArgv, size_t& nExpandedArgc, dnx::char_t**& ppszExpandedArgv);
void FreeExpandedCommandLineArguments(size_t nArgc, dnx::char_t** ppszArgv);

template <size_t arg_count>
void test_ExpandCommandLineArguments(dnx::char_t*(&args)[arg_count], bool should_expand, std::vector<const dnx::char_t*>& expected_expanded_args)
{
    size_t expanded_arg_count;
    dnx::char_t** expanded_args = nullptr;

    ASSERT_EQ(should_expand, ExpandCommandLineArguments(arg_count, args, expanded_arg_count, expanded_args));

    if (should_expand)
    {
        ASSERT_EQ(expected_expanded_args.size(), expanded_arg_count);
        for (auto i = 0u; i < expanded_arg_count; i++)
        {
            ASSERT_STREQ(expected_expanded_args[i], expanded_args[i]);
        }

        FreeExpandedCommandLineArguments(expanded_arg_count, expanded_args);
    }
}

TEST(parameter_expansion, ExpandCommandLineArguments_should_not_expand_when_no_params)
{
    size_t expanded_arg_count;
    dnx::char_t** expanded_args = nullptr;
    ASSERT_FALSE(ExpandCommandLineArguments(0u, nullptr, expanded_arg_count, expanded_args));
    ASSERT_EQ(nullptr, expanded_args);
}

TEST(parameter_expansion, ExpandCommandLineArguments_should_expand_dot)
{
    dnx::char_t* args[]
    { _X("."), _X("run") };
    std::vector<const dnx::char_t*> expected_expanded_args(
    { _X("--appbase"), _X("."), _X("Microsoft.Dnx.ApplicationHost"), _X("run") });

    test_ExpandCommandLineArguments(args, true, expected_expanded_args);
}

TEST(parameter_expansion, ExpandCommandLineArguments_should_ignore_appbase_after_bootstrapper_commands)
{
    dnx::char_t* args[]
        { _X("."), _X("run"), _X("--appbase"), _X("C:\\temp") };
    std::vector<const dnx::char_t*> expected_expanded_args(
        { _X("--appbase"), _X("."), _X("Microsoft.Dnx.ApplicationHost"), _X("run"), _X("--appbase"), _X("C:\\temp") });

    test_ExpandCommandLineArguments(args, true, expected_expanded_args);
}

TEST(parameter_expansion, ExpandCommandLineArguments_should_not_expand_params_if_appbase_after_parameter_with_argument)
{
    dnx::char_t* args[] { _X("--port"), _X("1234"), _X("--appbase"), _X("C:\\temp"), _X("run") };
    size_t expanded_arg_count;
    dnx::char_t** expanded_args = nullptr;

    ASSERT_FALSE(ExpandCommandLineArguments(0u, args, expanded_arg_count, expanded_args));
    ASSERT_EQ(nullptr, expanded_args);
}

TEST(parameter_expansion, ExpandCommandLineArguments_should_expand_params_and_add_implicit_appbase_path_for_dll)
{
    dnx::char_t* args[]
    { _X("--port"), _X("1234"), _X("MyApp.dll"), _X("param")};
    std::vector<const dnx::char_t*> expected_expanded_args(
    { _X("--port"), _X("1234"), _X("--appbase"), _X("."), _X("MyApp.dll"), _X("param") });

    test_ExpandCommandLineArguments(args, true, expected_expanded_args);
}

TEST(parameter_expansion, ExpandCommandLineArguments_should_expand_params_and_add_implicit_appbase_path_for_exe)
{
    dnx::char_t* args[]
    { _X("--port"), _X("1234"), _X("MyApp.exe") };
    std::vector<const dnx::char_t*> expected_expanded_args(
    { _X("--port"), _X("1234"), _X("--appbase"), _X("."), _X("MyApp.exe") });

    test_ExpandCommandLineArguments(args, true, expected_expanded_args);
}

TEST(parameter_expansion, ExpandCommandLineArguments_should_expand_params_and_copy_appbase_path_for_dll_with_path)
{
    dnx::char_t* args[]
    { _X("--port"), _X("1234"), _X("C:\\app\\MyApp.dll") };
    std::vector<const dnx::char_t*> expected_expanded_args(
    { _X("--port"), _X("1234"), _X("--appbase"), _X("C:\\app\\"), _X("C:\\app\\MyApp.dll") });

    test_ExpandCommandLineArguments(args, true, expected_expanded_args);
}

TEST(parameter_expansion, ExpandCommandLineArguments_should_expand_params_and_copy_appbase_path_for_exe_with_path)
{
    dnx::char_t* args[]
    { _X("--port"), _X("1234"), _X("/MyApp.exe") };
    std::vector<const dnx::char_t*> expected_expanded_args(
    { _X("--port"), _X("1234"), _X("--appbase"), _X("/"), _X("/MyApp.exe") });

    test_ExpandCommandLineArguments(args, true, expected_expanded_args);
}

TEST(parameter_expansion, ExpandCommandLineArguments_should_expand_params_and_add_implicit_appbase_for_project_json)
{
    dnx::char_t* args[]
    { _X("project.json"), _X("run") };
    std::vector<const dnx::char_t*> expected_expanded_args(
    { _X("--appbase"), _X("."), _X("Microsoft.Dnx.ApplicationHost"), _X("run") });

    test_ExpandCommandLineArguments(args, true, expected_expanded_args);
}

TEST(parameter_expansion, ExpandCommandLineArguments_should_expand_params_and_add_appbase_for_project_json_path)
{
    dnx::char_t* args[]
    { _X("C:\\MyApp\\project.json"), _X("run") };
    std::vector<const dnx::char_t*> expected_expanded_args(
    { _X("--appbase"), _X("C:\\MyApp\\"), _X("Microsoft.Dnx.ApplicationHost"), _X("run"), });

    test_ExpandCommandLineArguments(args, true, expected_expanded_args);
}

TEST(parameter_expansion, ExpandCommandLineArguments_should_expand_implicit_appbase_to_dot)
{
    dnx::char_t* args[]
    { _X("run") };
    std::vector<const dnx::char_t*> expected_expanded_args(
    { _X("--appbase"), _X("."), _X("Microsoft.Dnx.ApplicationHost"), _X("run") });

    test_ExpandCommandLineArguments(args, true, expected_expanded_args);
}

TEST(parameter_expansion, ExpandCommandLineArguments_should_expand_implicit_appbase_to_dot_for_command_after_bootstrapper_params)
{
    dnx::char_t* args[]
    { _X("--port"), _X("1234"), _X("run") };
    std::vector<const dnx::char_t*> expected_expanded_args(
    { _X("--port"), _X("1234"), _X("--appbase"), _X("."), _X("Microsoft.Dnx.ApplicationHost"), _X("run") });

    test_ExpandCommandLineArguments(args, true, expected_expanded_args);
}

TEST(parameter_expansion, ExpandCommandLineArguments_should_not_expand_if_appbase_parameter_value_missing)
{
    dnx::char_t* args[]
    { _X("--port"), _X("1234"), _X("--appbase") };
    size_t expanded_arg_count;
    dnx::char_t** expanded_args = nullptr;
    ASSERT_FALSE(ExpandCommandLineArguments(3u, args, expanded_arg_count, expanded_args));
    ASSERT_EQ(nullptr, expanded_args);
}