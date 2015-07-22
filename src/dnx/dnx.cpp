// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

#include "stdafx.h"
#include <vector>
#include "pal.h"
#include "utils.h"
#include "app_main.h"

bool strings_equal_ignore_case(const dnx::char_t* s1, const dnx::char_t* s2)
{
#if defined(_WIN32)
    return _wcsicmp(s1, s2) == 0;
#else
    return strcasecmp(s1, s2) == 0;
#endif
}

bool string_ends_with_ignore_case(const dnx::char_t* s, const dnx::char_t* suffix)
{
    auto str_len = x_strlen(s);
    auto suffix_len = x_strlen(suffix);

    if (suffix_len > str_len)
    {
        return false;
    }

    return strings_equal_ignore_case(s + str_len - suffix_len, suffix);
}

int split_path(const dnx::char_t* path)
{
    for (auto i = static_cast<int>(x_strlen(path)) - 1; i >= 0; i--)
    {
        if (path[i] == _X('\\') || path[i] == _X('/'))
        {
            return i;
        }
    }

    return -1;
}

const dnx::char_t* allocate_and_copy(const dnx::char_t* value, size_t count)
{
    auto buff_size = count + 1;
    auto buffer = new dnx::char_t[buff_size];
#if defined(_WIN32)
    wcsncpy_s(buffer, buff_size, value, count);
#else
    strncpy(buffer, value, count);
    buffer[count] = '\0';
#endif
    return buffer;
}

const dnx::char_t* allocate_and_copy(const dnx::char_t* value)
{
    return allocate_and_copy(value, x_strlen(value));
}

int BootstrapperOptionValueNum(const dnx::char_t* pszCandidate)
{
    if (strings_equal_ignore_case(pszCandidate, _X("--appbase")) ||
        strings_equal_ignore_case(pszCandidate, _X("--lib")) ||
        strings_equal_ignore_case(pszCandidate, _X("--packages")) ||
        strings_equal_ignore_case(pszCandidate, _X("--configuration")) ||
        strings_equal_ignore_case(pszCandidate, _X("--port")))
    {
        return 1;
    }

    if (strings_equal_ignore_case(pszCandidate, _X("--watch")) ||
        strings_equal_ignore_case(pszCandidate, _X("--debug")) ||
        strings_equal_ignore_case(pszCandidate, _X("--help")) ||
        strings_equal_ignore_case(pszCandidate, _X("-h")) ||
        strings_equal_ignore_case(pszCandidate, _X("-?")) ||
        strings_equal_ignore_case(pszCandidate, _X("--version")))
    {
        return 0;
    }

    // It isn't a bootstrapper option
    return -1;
}

size_t FindAppBaseOrNonHostOption(size_t argc, dnx::char_t**argv)
{
    for (size_t i = 0; i < argc; i++)
    {
        if (strings_equal_ignore_case(argv[i], _X("--appbase")))
        {
            return i;
        }

        auto option_num_args = BootstrapperOptionValueNum(argv[i]);
        if (option_num_args < 0)
        {
            return i;
        }

        i += option_num_args;
    }

    return argc;
}

dnx::char_t* GetAppBaseParameterValue(size_t argc, dnx::char_t* argv[])
{
    auto index = FindAppBaseOrNonHostOption(argc, argv);

    // no parameters or '--appbase' is the last value in the array or `--appbase` not found
    if (argc == 0 || index >= argc - 1 || argv[index][0] != _X('-'))
    {
        return nullptr;
    }

    return argv[index + 1];
}

void AppendAppbaseFromFile(const dnx::char_t* path, std::vector<const dnx::char_t*>& expanded_args)
{
    auto split_idx = split_path(path);

    expanded_args.push_back(allocate_and_copy(_X("--appbase")));

    if (split_idx < 0)
    {
        expanded_args.push_back(allocate_and_copy(_X(".")));
    }
    else
    {
        expanded_args.push_back(allocate_and_copy(path, split_idx + 1));
    }
}

void ExpandArgument(const dnx::char_t* value, std::vector<const dnx::char_t*>& expanded_args)
{
    if (string_ends_with_ignore_case(value, _X(".dll")) || string_ends_with_ignore_case(value, _X(".exe")))
    {
        // "dnx /path/App.dll arg1" --> "dnx --appbase /path/ /path/App.dll arg1"
        // "dnx /path/App.exe arg1" --> "dnx --appbase /path/ /path/App.exe arg1"
        // "dnx App.exe arg1" --> "dnx --appbase . App.exe arg1"
        AppendAppbaseFromFile(value, expanded_args);
        expanded_args.push_back(allocate_and_copy(value));

        return;
    }

    if (strings_equal_ignore_case(value, _X(".")))
    {
        // "dnx . run" --> "dnx --appbase . Microsoft.Dnx.ApplicationHost run"
        expanded_args.push_back(allocate_and_copy(_X("--appbase")));
        expanded_args.push_back(allocate_and_copy(value));
        expanded_args.push_back(allocate_and_copy(_X("Microsoft.Dnx.ApplicationHost")));

        return;
    }

    auto split_idx = split_path(value);
    if (string_ends_with_ignore_case(value + (split_idx < 0 ? 0 : split_idx), _X("project.json")))
    {
        // "dnx /path/project.json run" --> "dnx --appbase /path/ Microsoft.Dnx.ApplicationHost run"
        AppendAppbaseFromFile(value, expanded_args);
        expanded_args.push_back(allocate_and_copy(_X("Microsoft.Dnx.ApplicationHost")));
        return;
    }

    // "dnx run" --> "dnx --appbase . Microsoft.Dnx.ApplicationHost run"
    expanded_args.push_back(allocate_and_copy(_X("--appbase")));
    expanded_args.push_back(allocate_and_copy(_X(".")));
    expanded_args.push_back(allocate_and_copy(_X("Microsoft.Dnx.ApplicationHost")));
    expanded_args.push_back(allocate_and_copy(value));
}

bool ExpandCommandLineArguments(size_t nArgc, dnx::char_t** ppszArgv, size_t& nExpandedArgc, dnx::char_t**& ppszExpandedArgv)
{
    size_t pivot_parameter_idx = FindAppBaseOrNonHostOption(nArgc, ppszArgv);

    // either no non-bootstrapper option found or --appbase was found - in either case expansion is not needed
    if (pivot_parameter_idx >= nArgc || ppszArgv[pivot_parameter_idx][0] == _X('-'))
    {
        return false;
    }

    std::vector<const dnx::char_t*> expanded_args_temp;
    for (size_t source_idx = 0; source_idx < nArgc; source_idx++)
    {
        if (source_idx == pivot_parameter_idx)
        {
            ExpandArgument(ppszArgv[source_idx], expanded_args_temp);
        }
        else
        {
            expanded_args_temp.push_back(allocate_and_copy(ppszArgv[source_idx]));
        }
    }

    nExpandedArgc = expanded_args_temp.size();
    ppszExpandedArgv = new dnx::char_t*[nExpandedArgc];

    for (size_t i = 0; i < nExpandedArgc; i++)
    {
        ppszExpandedArgv[i] = const_cast<dnx::char_t*>(expanded_args_temp[i]);
    }

    return true;
}

void FreeExpandedCommandLineArguments(size_t nArgc, dnx::char_t** ppszArgv)
{
    for (size_t i = 0; i < nArgc; ++i)
    {
        delete[] ppszArgv[i];
    }
    delete[] ppszArgv;
}

bool GetApplicationBase(const dnx::xstring_t& currentDirectory, size_t argc, dnx::char_t* argv[], /*out*/ dnx::char_t* fullAppBasePath)
{
    dnx::char_t buffer[MAX_PATH];
    const dnx::char_t* appBase = GetAppBaseParameterValue(argc, argv);

    // Note: We use application base from DNX_APPBASE environment variable only if --appbase
    // did not exist. if neither --appBase nor DNX_APPBASE existed we use current directory
    if (!appBase)
    {
        appBase = GetAppBasePathFromEnvironment(buffer) ? buffer : currentDirectory.c_str();
    }

    // Prevent coreclr native bootstrapper from failing with relative appbase
    return GetFullPath(appBase, fullAppBasePath) != 0;
}

int CallApplicationProcessMain(size_t argc, dnx::char_t* argv[], dnx::trace_writer& trace_writer)
{
    // Set the DNX_CONOSLE_HOST flag which will print exceptions to stderr instead of throwing
    SetConsoleHost();

    auto currentDirectory = GetNativeBootstrapperDirectory();

    // Set the DEFAULT_LIB environment variable to be the same directory as the exe
    SetEnvironmentVariable(_X("DNX_DEFAULT_LIB"), currentDirectory.c_str());

    CALL_APPLICATION_MAIN_DATA data = { 0 };
    data.argc = static_cast<int>(argc);
    data.argv = const_cast<const dnx::char_t**>(argv);

    dnx::char_t appBaseBuffer[MAX_PATH];

    if (!GetApplicationBase(currentDirectory, argc, argv, appBaseBuffer))
    {
        return 1;
    }

    data.applicationBase = appBaseBuffer;

    try
    {
        const dnx::char_t* hostModuleName =
#if defined(CORECLR_WIN)
#if defined(ONECORE) || defined(ARM)
        _X("dnx.onecore.coreclr.dll");
#else
        _X("dnx.win32.coreclr.dll");
#endif
#elif defined(CORECLR_DARWIN)
        _X("dnx.coreclr.dylib");
#elif defined(CORECLR_LINUX)
        _X("dnx.coreclr.so");
#else
        _X("dnx.clr.dll");
        SetEnvironmentVariable(_X("DNX_IS_WINDOWS"), _X("1"));
#endif

        // Note: need to keep as ASCII as GetProcAddress function takes ASCII params
        return CallApplicationMain(hostModuleName, "CallApplicationMain", &data, trace_writer);
    }
    catch (const std::exception& ex)
    {
        xout << dnx::utils::to_xstring_t(ex.what()) << std::endl;
        return 1;
    }
}