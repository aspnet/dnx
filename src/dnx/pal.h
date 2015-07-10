// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

#pragma once

#include "xplat.h"
#include "trace_writer.h"
#include "app_main.h"

dnx::xstring_t GetNativeBootstrapperDirectory();
void WaitForDebuggerToAttach();
bool IsTracingEnabled();
void SetConsoleHost();
bool GetAppBasePathFromEnvironment(dnx::char_t* szPath);
bool GetFullPath(const dnx::char_t* szPath, dnx::char_t*  szFullPath);
int CallApplicationMain(const dnx::char_t* moduleName, const char* functionName, CALL_APPLICATION_MAIN_DATA* data, dnx::trace_writer& trace_writer);

#ifndef SetEnvironmentVariable
bool SetEnvironmentVariable(const dnx::char_t* lpName, const dnx::char_t* lpValue);
#endif //SetEnvironmentVariable
