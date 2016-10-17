// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

// This file will be dynamically updated during build to generate a
// minimal trusted platform assemblies list

#include "stdafx.h"
#include <vector>
#include <string>
#include "xplat.h"

const std::vector<const dnx::char_t*> CreateTpaBase(bool native_images)
{
    return native_images
        ? std::vector<const dnx::char_t*>
        {
            _X("Microsoft.Dnx.Host.ni.dll"),
            _X("Microsoft.Dnx.Host.CoreClr.ni.dll"),
            _X("Microsoft.Dnx.Loader.ni.dll"),
            _X("Microsoft.Extensions.PlatformAbstractions.ni.dll"),
            _X("System.AppContext.ni.dll"),
            _X("System.Collections.ni.dll"),
            _X("System.Collections.Concurrent.ni.dll"),
            _X("System.ComponentModel.ni.dll"),
            _X("System.Console.ni.dll"),
            _X("System.Diagnostics.Debug.ni.dll"),
            _X("System.Diagnostics.Tracing.ni.dll"),
            _X("System.Globalization.ni.dll"),
            _X("System.IO.ni.dll"),
            _X("System.IO.FileSystem.ni.dll"),
            _X("System.IO.FileSystem.Primitives.ni.dll"),
            _X("System.Linq.ni.dll"),
            _X("System.Private.Uri.ni.dll"),
            _X("System.Reflection.ni.dll"),
            _X("System.Reflection.Extensions.ni.dll"),
            _X("System.Reflection.Primitives.ni.dll"),
            _X("System.Reflection.TypeExtensions.ni.dll"),
            _X("System.Resources.ResourceManager.ni.dll"),
            _X("System.Runtime.ni.dll"),
            _X("System.Runtime.Extensions.ni.dll"),
            _X("System.Runtime.Handles.ni.dll"),
            _X("System.Runtime.InteropServices.ni.dll"),
            _X("System.Runtime.InteropServices.RuntimeInformation.ni.dll"),
            _X("System.Runtime.Loader.ni.dll"),
            _X("System.Text.Encoding.ni.dll"),
            _X("System.Text.Encoding.Extensions.ni.dll"),
            _X("System.Threading.ni.dll"),
            _X("System.Threading.Overlapped.ni.dll"),
            _X("System.Threading.Tasks.ni.dll"),
        }
        : std::vector<const dnx::char_t*>
        {
            _X("Microsoft.Dnx.Host.dll"),
            _X("Microsoft.Dnx.Host.CoreClr.dll"),
            _X("Microsoft.Dnx.Loader.dll"),
            _X("Microsoft.Extensions.PlatformAbstractions.dll"),
            _X("System.AppContext.dll"),
            _X("System.Collections.dll"),
            _X("System.Collections.Concurrent.dll"),
            _X("System.ComponentModel.dll"),
            _X("System.Console.dll"),
            _X("System.Diagnostics.Debug.dll"),
            _X("System.Diagnostics.Tracing.dll"),
            _X("System.Globalization.dll"),
            _X("System.IO.dll"),
            _X("System.IO.FileSystem.dll"),
            _X("System.IO.FileSystem.Primitives.dll"),
            _X("System.Linq.dll"),
            _X("System.Private.Uri.dll"),
            _X("System.Reflection.dll"),
            _X("System.Reflection.Extensions.dll"),
            _X("System.Reflection.Primitives.dll"),
            _X("System.Reflection.TypeExtensions.dll"),
            _X("System.Resources.ResourceManager.dll"),
            _X("System.Runtime.dll"),
            _X("System.Runtime.Extensions.dll"),
            _X("System.Runtime.Handles.dll"),
            _X("System.Runtime.InteropServices.dll"),
            _X("System.Runtime.InteropServices.RuntimeInformation.dll"),
            _X("System.Runtime.Loader.dll"),
            _X("System.Text.Encoding.dll"),
            _X("System.Text.Encoding.Extensions.dll"),
            _X("System.Threading.dll"),
            _X("System.Threading.Overlapped.dll"),
            _X("System.Threading.Tasks.dll"),
        };
}
