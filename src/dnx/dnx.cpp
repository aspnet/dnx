// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

#include "stdafx.h"
#include <vector>
#include "pal.h"
#include "utils.h"
#include "app_main.h"

bool string_ends_with_ignore_case(const dnx::char_t* s, const dnx::char_t* suffix)
{
    auto str_len = x_strlen(s);
    auto suffix_len = x_strlen(suffix);

    if (suffix_len > str_len)
    {
        return false;
    }

    return dnx::utils::strings_equal_ignore_case(s + str_len - suffix_len, suffix);
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

dnx::char_t* GetOptionValue(int argc, dnx::char_t* argv[], const dnx::char_t* optionName)
{
    auto index = dnx::utils::find_bootstrapper_option_index(argc, argv, optionName);

    // no parameters or '--{optionName}' is the last value in the array or `--{optionName}` not found
    if (index < 0 || index >= argc - 1)
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

void ExpandProject(const dnx::char_t* project_path, std::vector<const dnx::char_t*>& expanded_args)
{
    auto split_idx = split_path(project_path);

    // note that we split the path first and check the file name to handle paths like c:\MyApp\my_project.json
    // (`split_idx + 1` works fine since `split_path` returns -1 if it does not find `\` or '/')
    if (dnx::utils::strings_equal_ignore_case(project_path + split_idx + 1, _X("project.json")))
    {
        // "dnx /path/project.json run" --> "dnx --appbase /path/ Microsoft.Dnx.ApplicationHost run"
        AppendAppbaseFromFile(project_path, expanded_args);
        expanded_args.push_back(allocate_and_copy(_X("Microsoft.Dnx.ApplicationHost")));
        return;
    }

    expanded_args.push_back(allocate_and_copy(_X("--appbase")));
    expanded_args.push_back(allocate_and_copy(project_path));
    expanded_args.push_back(allocate_and_copy(_X("Microsoft.Dnx.ApplicationHost")));
}

void ExpandNonHostArgument(const dnx::char_t* value, std::vector<const dnx::char_t*>& expanded_args)
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

    // "dnx run" --> "dnx --appbase . Microsoft.Dnx.ApplicationHost run"
    expanded_args.push_back(allocate_and_copy(_X("--appbase")));
    expanded_args.push_back(allocate_and_copy(_X(".")));
    expanded_args.push_back(allocate_and_copy(_X("Microsoft.Dnx.ApplicationHost")));
    expanded_args.push_back(allocate_and_copy(value));
}

bool ExpandCommandLineArguments(int nArgc, dnx::char_t** ppszArgv, size_t& nExpandedArgc, dnx::char_t**& ppszExpandedArgv)
{
    // --appbase was found expansion not needed
    if (dnx::utils::find_bootstrapper_option_index(nArgc, ppszArgv, _X("--appbase")) >= 0)
    {
        return false;
    }

    // no non-bootstrapper option found expansion is not needed
    auto param_idx = dnx::utils::find_first_non_bootstrapper_param_index(nArgc, ppszArgv);
    if (param_idx < 0)
    {
        return false;
    }

    bool arg_expanded = false;
    std::vector<const dnx::char_t*> expanded_args_temp;
    for (int source_idx = 0; source_idx < nArgc; source_idx++)
    {
        if (!arg_expanded)
        {
            if (dnx::utils::strings_equal_ignore_case(_X("-p"), ppszArgv[source_idx]) ||
                dnx::utils::strings_equal_ignore_case(_X("--project"), ppszArgv[source_idx]))
            {
                // Note that ++source_idx is safe here since if we had a trailing -p/--project we would have exited
                // before entering the loop because we wouldn't have found any non host option
                ExpandProject(ppszArgv[++source_idx], expanded_args_temp);
                arg_expanded = true;
            }
            else if (source_idx == param_idx)
            {
                ExpandNonHostArgument(ppszArgv[source_idx], expanded_args_temp);
                arg_expanded = true;
            }
            else
            {
                expanded_args_temp.push_back(allocate_and_copy(ppszArgv[source_idx]));
            }
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

bool GetApplicationBase(const dnx::xstring_t& currentDirectory, int argc, dnx::char_t* argv[], /*out*/ dnx::char_t* fullAppBasePath)
{
    dnx::char_t buffer[MAX_PATH];
    const dnx::char_t* appBase = GetOptionValue(argc, argv, _X("--appbase"));

    // Note: We use application base from DNX_APPBASE environment variable only if --appbase
    // did not exist. if neither --appBase nor DNX_APPBASE existed we use current directory
    if (!appBase)
    {
        appBase = GetAppBasePathFromEnvironment(buffer) ? buffer : currentDirectory.c_str();
    }

    // Prevent coreclr native bootstrapper from failing with relative appbase
    return GetFullPath(appBase, fullAppBasePath) != 0;
}

int CallApplicationProcessMain(int argc, dnx::char_t* argv[], dnx::trace_writer& trace_writer)
{
    const auto currentDirectory = GetNativeBootstrapperDirectory();

    // Set the FRAMEWORK environment variable to the value provided on the command line
    //  (it needs to be available BEFORE the application main is called)
    auto frameworkName = GetOptionValue(argc, argv, _X("--framework"));
    if (frameworkName)
    {
        SetEnvironmentVariable(_X("DNX_FRAMEWORK"), frameworkName);
    }

    CALL_APPLICATION_MAIN_DATA data = { 0 };
    data.argc = argc;
    data.argv = const_cast<const dnx::char_t**>(argv);
    data.runtimeDirectory = currentDirectory.c_str();

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