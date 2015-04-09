// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

#pragma once

#include "xplat.h"
#include "TraceWriter.h"

dnx::xstring_t GetNativeBootstrapperDirectory();
void WaitForDebuggerToAttach();
bool IsTracingEnabled();
void SetConsoleHost();
BOOL GetAppBasePathFromEnvironment(LPTSTR szPath);
BOOL GetFullPath(LPCTSTR szPath, LPTSTR szFullPath);
int CallApplicationMain(const dnx::char_t* moduleName, const char* functionName, CALL_APPLICATION_MAIN_DATA* data, TraceWriter traceWriter);

#ifndef SetEnvironmentVariable
BOOL SetEnvironmentVariable(LPCTSTR lpName, LPCTSTR lpValue);
#endif //SetEnvironmentVariable
