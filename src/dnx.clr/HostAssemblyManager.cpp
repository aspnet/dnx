// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

#include "stdafx.h"
#include "HostAssemblyManager.h"

HostAssemblyManager::HostAssemblyManager(const wchar_t* runtimeDirectory)
{
    m_pAssemblyStore = new HostAssemblyStore(runtimeDirectory);
    m_pAssemblyStore->AddRef();
}

HostAssemblyManager::~HostAssemblyManager()
{
    m_pAssemblyStore->Release();
}

// IHostAssemblyManager
HRESULT STDMETHODCALLTYPE HostAssemblyManager::GetNonHostStoreAssemblies(ICLRAssemblyReferenceList **ppReferenceList)
{
    ppReferenceList = NULL;
    return S_OK;
}

HRESULT STDMETHODCALLTYPE HostAssemblyManager::GetAssemblyStore(IHostAssemblyStore **ppAssemblyStore)
{
    *ppAssemblyStore = m_pAssemblyStore;
    return S_OK;
}

HRESULT STDMETHODCALLTYPE HostAssemblyManager::GetHostApplicationPolicy(DWORD /*dwPolicy*/, DWORD /*dwAppDomainId*/, DWORD *pcbBufferSize, BYTE* /*pbBuffer*/)
{
    *pcbBufferSize = 0;
    return S_OK;
}

// IUnknown

HRESULT STDMETHODCALLTYPE HostAssemblyManager::QueryInterface(const IID &/*iid*/, void **ppv)
{
    if (!ppv)
    {
        return E_POINTER;
    }

    *ppv = this;
    AddRef();
    return S_OK;
}

ULONG STDMETHODCALLTYPE HostAssemblyManager::AddRef()
{
    return InterlockedIncrement(&m_RefCount);
}

ULONG STDMETHODCALLTYPE HostAssemblyManager::Release()
{
    if (InterlockedDecrement(&m_RefCount) == 0)
    {
        delete this;
        return 0;
    }
    return m_RefCount;
}