// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

#pragma once

#include "stdafx.h"
#include <unordered_map>
#include "trace_writer.h"
#include "app_main.h"

dnx::xstring_t GetNativeBootstrapperDirectory() { return L""; }
void WaitForDebuggerToAttach() {}
bool IsTracingEnabled() { return true; }
void SetConsoleHost() {}
bool GetAppBasePathFromEnvironment(wchar_t* /*szPath*/) { return false; };
bool GetFullPath(const wchar_t* /*szPath*/, wchar_t* /*szFullPath*/) { return false; }
int CallApplicationMain(const wchar_t* /*moduleName*/, const char* /*functionName*/, CALL_APPLICATION_MAIN_DATA* /*data*/, dnx::trace_writer& /*trace_writer*/) { return 3; }

#ifndef SetEnvironmentVariable
BOOL SetEnvironmentVariable(LPCTSTR lpName, LPCTSTR lpValue);
#endif //SetEnvironmentVariable
