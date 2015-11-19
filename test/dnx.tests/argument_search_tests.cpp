// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

#include "stdafx.h"
#include "utils.h"

TEST(parameter_search, find_first_non_bootstrapper_param_index_returns_minus_1_if_no_params)
{
    ASSERT_EQ(-1, dnx::utils::find_first_non_bootstrapper_param_index(0, nullptr));
}

TEST(parameter_search, find_first_non_bootstrapper_param_index_returns_minus_1_if_no_non_bootstrapper_options)
{
    dnx::char_t* args[]{ _X("--port"), _X("1234"), _X("--appbase"), _X("C:\\temp") };
    ASSERT_EQ(-1, dnx::utils::find_first_non_bootstrapper_param_index(4, args));
}

TEST(parameter_search, find_first_non_bootstrapper_param_index_returns_index_of_non_bootstrapper_option)
{
    dnx::char_t* args[]{ _X("--port"), _X("1234"), _X("--appbase"), _X("C:\\temp"), _X("run") };
    ASSERT_EQ(4, dnx::utils::find_first_non_bootstrapper_param_index(5, args));
}

TEST(parameter_search, find_first_non_bootstrapper_param_index_returns_index_of_first_non_bootstrapper_option)
{
    dnx::char_t* args[]{ _X("--port"), _X("1234"), _X("--appbase"), _X("C:\\temp"), _X("run"), _X("Forrest"), _X("run") };
    ASSERT_EQ(4, dnx::utils::find_first_non_bootstrapper_param_index(8, args));
}

TEST(parameter_search, find_bootstrapper_option_index_returns_minus_1_if_no_params)
{
    ASSERT_EQ(-1, dnx::utils::find_bootstrapper_option_index(0, nullptr, _X("--appbase")));
}

TEST(parameter_search, find_bootstrapper_option_index_returns_minus_1_if_param_not_found)
{
    dnx::char_t* args[]{ _X("--port"), _X("1234"), _X("--appbase"), _X("C:\\temp") };
    ASSERT_EQ(-1, dnx::utils::find_bootstrapper_option_index(4, args, _X("--version")));
}

TEST(parameter_search, find_bootstrapper_option_index_returns_minus_1_if_param_not_found_but_name_matches)
{
    dnx::char_t* args[]{ _X("--port"), _X("1234"), _X("--appbase"), _X("C:\\temp"), _X("run"), _X("--version") };
    ASSERT_EQ(-1, dnx::utils::find_bootstrapper_option_index(6, args, _X("--version")));
}

TEST(parameter_search, find_bootstrapper_option_index_returns_minus_1_if_param_name_is_not_bootstrapper_option)
{
    dnx::char_t* args[]{ _X("--port"), _X("1234"), _X("--appbase"), _X("C:\\temp"), _X("run") };
    ASSERT_EQ(-1, dnx::utils::find_bootstrapper_option_index(6, args, _X("run")));
}

TEST(parameter_search, find_bootstrapper_option_index_returns_param_index_if_param_exists)
{
    dnx::char_t* args[]{ _X("--port"), _X("1234"), _X("--appbase"), _X("C:\\temp") };
    ASSERT_EQ(2, dnx::utils::find_bootstrapper_option_index(4, args, _X("--appbase")));
}