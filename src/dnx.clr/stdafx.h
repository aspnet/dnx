// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

#pragma once

#include "targetver.h"

#define WIN32_LEAN_AND_MEAN             // Exclude rarely-used stuff from Windows headers
// Windows Header Files:
#include <windows.h>

// msvc com support
#include <comdef.h>
#include <comdefsp.h>

// clr hosting apis
#include <MetaHost.h>
#pragma comment(lib, "mscoree.lib")

_COM_SMARTPTR_TYPEDEF(ICLRMetaHost, __uuidof(ICLRMetaHost));
_COM_SMARTPTR_TYPEDEF(ICLRMetaHostPolicy, __uuidof(ICLRMetaHostPolicy));
_COM_SMARTPTR_TYPEDEF(ICLRRuntimeHost, __uuidof(ICLRRuntimeHost));
_COM_SMARTPTR_TYPEDEF(ICLRRuntimeInfo, __uuidof(ICLRRuntimeInfo));
_COM_SMARTPTR_TYPEDEF(ICLRControl, __uuidof(ICLRControl));
_COM_SMARTPTR_TYPEDEF(ICLRDomainManager, __uuidof(ICLRDomainManager));

#define PPV(x) __uuidof(*x), (void**)x

inline void _HR_DEBUG(HRESULT hr)
{
        if (FAILED(hr))
        {
            return;
        }
}

#define _HR(x) if (SUCCEEDED(hr)) {hr = x; _HR_DEBUG(hr);}
#define _HR_CLEANUP(xcond,x) if(xcond) {HRESULT hrClean = x; hr = SUCCEEDED(hr) ? hrClean : hr;}
