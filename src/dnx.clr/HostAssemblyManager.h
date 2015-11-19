// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

#pragma once

#include <mscoree.h>
#include "HostAssemblyStore.h"

class HostAssemblyManager : public IHostAssemblyManager
{
public:
    HRESULT STDMETHODCALLTYPE GetNonHostStoreAssemblies(ICLRAssemblyReferenceList **ppReferenceList);
    HRESULT STDMETHODCALLTYPE GetAssemblyStore(IHostAssemblyStore **ppAssemblyStore);
    HRESULT STDMETHODCALLTYPE GetHostApplicationPolicy(DWORD dwPolicy, DWORD dwAppDomainId, DWORD *pcbBufferSize, BYTE *pbBuffer);

    virtual HRESULT STDMETHODCALLTYPE QueryInterface(const IID &iid, void **ppv);
    virtual ULONG STDMETHODCALLTYPE AddRef();
    virtual ULONG STDMETHODCALLTYPE Release();

    HostAssemblyManager(const wchar_t* runtimeDirectory);
    virtual ~HostAssemblyManager();

private:
    long m_RefCount;
    HostAssemblyStore *m_pAssemblyStore;
};