// stdafx.h : include file for standard system include files,
// or project specific include files that are used frequently, but
// are changed infrequently
//

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
                int x = 5;
        }
}

#define _HR(x) if (SUCCEEDED(hr)) {hr = x; _HR_DEBUG(hr);}
#define _HR_CLEANUP(xcond,x) if(xcond) {HRESULT hrClean = x; hr = SUCCEEDED(hr) ? hrClean : hr;}
