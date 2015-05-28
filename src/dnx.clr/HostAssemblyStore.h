#pragma once

#include<mscoree.h>

class HostAssemblyStore : public IHostAssemblyStore
{
public:
    HRESULT STDMETHODCALLTYPE ProvideAssembly(AssemblyBindInfo *pBindInfo, UINT64 *pAssemblyId, UINT64 *pContext,
        IStream **ppStmAssemblyImage, IStream **ppStmPDB);
    HRESULT STDMETHODCALLTYPE ProvideModule(ModuleBindInfo *pBindInfo, DWORD *pdwModuleId, IStream **ppStmModuleImage,
        IStream **ppStmPDB);

    virtual HRESULT STDMETHODCALLTYPE	QueryInterface(const IID &iid, void **ppv);
    virtual ULONG STDMETHODCALLTYPE		AddRef();
    virtual ULONG STDMETHODCALLTYPE		Release();

    HostAssemblyStore() : m_RefCount(0)
    {};

    virtual ~HostAssemblyStore() = default;

private:
    long m_RefCount;
};