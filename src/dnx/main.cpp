// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

#include "stdafx.h"
#include "pal.h"
#include "utils.h"

int CallApplicationProcessMain(size_t argc, dnx::char_t* argv[], dnx::trace_writer& trace_writer);
void FreeExpandedCommandLineArguments(size_t argc, dnx::char_t** ppszArgv);
bool ExpandCommandLineArguments(size_t argc, dnx::char_t** ppszArgv, size_t& expanded_argc, dnx::char_t**& ppszExpandedArgv);
bool strings_equal_ignore_case(const dnx::char_t* s1, const dnx::char_t* s2);

#if defined(ARM)
int wmain(int argc, wchar_t* argv[])
#elif defined(PLATFORM_UNIX)
int main(int argc, char* argv[])
#else
extern "C" int __stdcall DnxMain(int argc, wchar_t* argv[])
#endif
{
    // Check for the debug flag before doing anything else
    for (int i = 1; i < argc; ++i)
    {
        //anything without - or -- is appbase or non-dnx command
        if (argv[i][0] != _X('-'))
        {
            break;
        }
        if (strings_equal_ignore_case(argv[i], _X("--appbase")))
        {
            //skip path argument
            ++i;
            continue;
        }
        if (strings_equal_ignore_case(argv[i], _X("--debug")))
        {
            WaitForDebuggerToAttach();
            break;
        }
    }

    size_t nExpandedArgc = 0;
    dnx::char_t** ppszExpandedArgv = nullptr;
    auto expanded = ExpandCommandLineArguments(argc - 1, &(argv[1]), nExpandedArgc, ppszExpandedArgv);

    auto trace_writer = dnx::trace_writer{ IsTracingEnabled() };
    if (!expanded)
    {
        return CallApplicationProcessMain(argc - 1, &argv[1], trace_writer);
    }

    auto exitCode = CallApplicationProcessMain(nExpandedArgc, ppszExpandedArgv, trace_writer);
    FreeExpandedCommandLineArguments(nExpandedArgc, ppszExpandedArgv);
    return exitCode;
}
