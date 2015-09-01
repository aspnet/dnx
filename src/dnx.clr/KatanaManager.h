// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

#pragma once

#include "stdafx.h"

#include "CriticalSection.h"
#include "ComObject.h"
#include "FileStream.h"
#include "HostAssemblyManager.h"

extern const wchar_t* AppDomainManagerAssemblyName;

struct ApplicationMainInfo
{
    typedef int(*ApplicationMainDelegate)(int argc, PCWSTR* argv);

    /* in */ ApplicationMainDelegate ApplicationMain;

    /* out */ BSTR RuntimeDirectory;

    /* out */ BSTR ApplicationBase;
};

class __declspec(uuid("7E9C5238-60DC-49D3-94AA-53C91FA79F7C")) IKatanaManager : public IUnknown
{
public:
    virtual HRESULT InitializeRuntime(LPCWSTR runtimeDirectory, LPCWSTR applicationBase) = 0;

    virtual HRESULT BindApplicationMain(ApplicationMainInfo* pInfo) = 0;

    virtual HRESULT CallApplicationMain(int argc, PCWSTR* argv) = 0;
};

_COM_SMARTPTR_TYPEDEF(IKatanaManager, __uuidof(IKatanaManager));

class KatanaManager :
    public IKatanaManager,
    public IHostControl
{
    CriticalSection _crit;
    bool _calledInitializeRuntime;
    HRESULT _hrInitializeRuntime;

    ICLRMetaHostPolicyPtr _MetaHostPolicy;
    ICLRRuntimeHostPtr _RuntimeHost;

    IHostAssemblyManager* m_pHostAssemblyManager;

    _bstr_t _clrVersion;
    _bstr_t _appPoolName;
    _bstr_t _appHostFileName;
    _bstr_t _rootWebConfigFileName;

    _bstr_t _applicationBase;
    _bstr_t _runtimeDirectory;

    ApplicationMainInfo _applicationMainInfo;

public:

    KatanaManager()
        : m_pHostAssemblyManager{nullptr}
    {
        _calledInitializeRuntime = false;
        _hrInitializeRuntime = E_PENDING;
    }

    ~KatanaManager()
    {
        if (m_pHostAssemblyManager)
        {
            m_pHostAssemblyManager->Release();
        }
    }

    IUnknown* CastInterface(REFIID riid)
    {
        if (riid == __uuidof(IKatanaManager))
            return static_cast<IKatanaManager*>(this);
        if (riid == __uuidof(IHostControl))
            return static_cast<IHostControl*>(this);
        if (riid == __uuidof(IHostAssemblyManager))
            return m_pHostAssemblyManager;

        return NULL;
    }

    HRESULT InitializeRuntime(LPCWSTR runtimeDirectory, LPCWSTR applicationBase)
    {
        Lock lock(&_crit);
        if (_calledInitializeRuntime)
            return _hrInitializeRuntime;

        HRESULT hr = S_OK;

        _applicationBase = applicationBase;
        _runtimeDirectory = runtimeDirectory;

        m_pHostAssemblyManager = new HostAssemblyManager(runtimeDirectory);
        m_pHostAssemblyManager->AddRef();

        _HR(CLRCreateInstance(CLSID_CLRMetaHostPolicy, PPV(&_MetaHostPolicy)));

        WCHAR wzVersion[130] = L"v4.0.30319";
        DWORD cchVersion = 129;
        DWORD dwConfigFlags = 0;

        ICLRRuntimeInfoPtr runtimeInfo;
        _HR(_MetaHostPolicy->GetRequestedRuntime(
            METAHOST_POLICY_APPLY_UPGRADE_POLICY,
            NULL,
            NULL,
            wzVersion,
            &cchVersion,
            NULL,//wzImageVersion,
            NULL,//&cchImageVersion,
            &dwConfigFlags,
            PPV(&runtimeInfo)));

        _HR(runtimeInfo->SetDefaultStartupFlags(
            STARTUP_LOADER_OPTIMIZATION_MULTI_DOMAIN_HOST | STARTUP_SERVER_GC,
            NULL));

        ICLRRuntimeHostPtr runtimeHost;
        _HR(runtimeInfo->GetInterface(CLSID_CLRRuntimeHost, PPV(&runtimeHost)));

        _HR(runtimeHost->SetHostControl(this));

        ICLRControl *pCLRControl = NULL;
        _HR(runtimeHost->GetCLRControl(&pCLRControl));
        _HR(pCLRControl->SetAppDomainManagerType(AppDomainManagerAssemblyName, L"DomainManager"));

        _HR(runtimeHost->Start());

        _RuntimeHost = runtimeHost;

        _hrInitializeRuntime = hr;
        _calledInitializeRuntime = TRUE;
        return hr;
    }

    HRESULT BindApplicationMain(ApplicationMainInfo* pInfo)
    {
        _applicationMainInfo = *pInfo;
        pInfo->RuntimeDirectory = _runtimeDirectory.copy();
        pInfo->ApplicationBase = _applicationBase.copy();
        return S_OK;
    }

    HRESULT CallApplicationMain(int argc, PCWSTR* argv)
    {
        return _applicationMainInfo.ApplicationMain(argc, argv);
    }

    //////////////////////////
    // IHostControl

    STDMETHODIMP GetHostManager(
        /* [in] */ REFIID riid,
        /* [out] */ void **ppObject)
    {
        HRESULT hr = S_OK;
        _HR(static_cast<IKatanaManager*>(this)->QueryInterface(riid, ppObject));
        return hr;
    }

    STDMETHODIMP SetAppDomainManager(
        /* [in] */ DWORD /*dwAppDomainID*/,
        /* [in] */ IUnknown* /*pUnkAppDomainManager*/)
    {
        return S_OK;
    }
};
