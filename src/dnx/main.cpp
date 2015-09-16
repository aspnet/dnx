// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

#include "stdafx.h"
#include "pal.h"
#include "utils.h"

int CallApplicationProcessMain(int argc, dnx::char_t* argv[], dnx::trace_writer& trace_writer);
void FreeExpandedCommandLineArguments(size_t argc, dnx::char_t** ppszArgv);
bool ExpandCommandLineArguments(int argc, dnx::char_t** ppszArgv, size_t& expanded_argc, dnx::char_t**& ppszExpandedArgv);

void WaitForDebugger(int argc, dnx::char_t** argv)
{
    if (dnx::utils::find_bootstrapper_option_index(argc, argv, _X("--bootstrapper-debug")) >= 0
#if !defined(CORECLR_WIN) && !defined(CORECLR_LINUX) && !defined(CORECLR_DARWIN)
        || dnx::utils::find_bootstrapper_option_index(argc, argv, _X("--debug")) >= 0
#endif
        )
    {
        WaitForDebuggerToAttach();
    }
}

#if defined(ARM)
int wmain(int argc, wchar_t* argv[])
#elif defined(PLATFORM_UNIX)
int main(int argc, char* argv[])
#else
extern "C" int __stdcall DnxMain(int argc, wchar_t* argv[])
#endif
{
    // Check for the debug flag before doing anything else
    WaitForDebugger(argc - 1, &(argv[1]));

    size_t nExpandedArgc = 0;
    dnx::char_t** ppszExpandedArgv = nullptr;
    auto expanded = ExpandCommandLineArguments(argc - 1, &(argv[1]), nExpandedArgc, ppszExpandedArgv);

    auto trace_writer = dnx::trace_writer{ IsTracingEnabled() };
    if (!expanded)
    {
        return CallApplicationProcessMain(argc - 1, &argv[1], trace_writer);
    }

    auto exitCode = CallApplicationProcessMain(static_cast<int>(nExpandedArgc), ppszExpandedArgv, trace_writer);
    FreeExpandedCommandLineArguments(nExpandedArgc, ppszExpandedArgv);
    return exitCode;
}
