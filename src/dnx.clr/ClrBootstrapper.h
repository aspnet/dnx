// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

#pragma once

#include "stdafx.h"

#include "CriticalSection.h"
#include "ComObject.h"
#include "FileStream.h"
#include "HostAssemblyManager.h"
#include "utils.h"

extern const wchar_t* AppDomainManagerAssemblyName;

struct ApplicationMainInfo
{
    typedef int(*ApplicationMainDelegate)(int argc, PCWSTR* argv);

    /* in */ ApplicationMainDelegate ApplicationMain;

    /* out */ BSTR OperatingSystem;

    /* out */ BSTR OsVersion;

    /* out */ BSTR Architecture;

    /* out */ BSTR RuntimeDirectory;

    /* out */ BSTR ApplicationBase;

    /* out */ BSTR Framework;
};

class __declspec(uuid("7E9C5238-60DC-49D3-94AA-53C91FA79F7C")) IClrBootstrapper : public IUnknown
{
public:
    virtual HRESULT InitializeRuntime(LPCWSTR runtimeDirectory, LPCWSTR applicationBase, LPCWSTR framework) = 0;

    virtual HRESULT BindApplicationMain(ApplicationMainInfo* pInfo) = 0;

    virtual HRESULT CallApplicationMain(int argc, PCWSTR* argv) = 0;
};

_COM_SMARTPTR_TYPEDEF(IClrBootstrapper, __uuidof(IClrBootstrapper));

class ClrBootstrapper :
    public IClrBootstrapper,
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
    _bstr_t _framework;

    ApplicationMainInfo _applicationMainInfo;

public:

    ClrBootstrapper()
        : m_pHostAssemblyManager{nullptr}
    {
        _calledInitializeRuntime = false;
        _hrInitializeRuntime = E_PENDING;
    }

    ~ClrBootstrapper()
    {
        if (m_pHostAssemblyManager)
        {
            m_pHostAssemblyManager->Release();
        }
    }

    IUnknown* CastInterface(REFIID riid)
    {
        if (riid == __uuidof(IClrBootstrapper))
            return static_cast<IClrBootstrapper*>(this);
        if (riid == __uuidof(IHostControl))
            return static_cast<IHostControl*>(this);
        if (riid == __uuidof(IHostAssemblyManager))
            return m_pHostAssemblyManager;

        return NULL;
    }

    HRESULT InitializeRuntime(LPCWSTR runtimeDirectory, LPCWSTR applicationBase, LPCWSTR framework)
    {
        Lock lock(&_crit);
        if (_calledInitializeRuntime)
        {
            return _hrInitializeRuntime;
        }

        HRESULT hr = S_OK;

        _applicationBase = applicationBase;
        _runtimeDirectory = runtimeDirectory;
        _framework = framework;

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
        pInfo->OperatingSystem = _bstr_t(L"Windows").copy();
        pInfo->OsVersion = _bstr_t(dnx::utils::get_windows_version().c_str()).copy();
        pInfo->Architecture =
#if defined(AMD64)
            _bstr_t(L"x64")
#else
            _bstr_t(L"x86")
#endif
            .copy();

        pInfo->Framework = _framework.copy();

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
        _HR(static_cast<IClrBootstrapper*>(this)->QueryInterface(riid, ppObject));
        return hr;
    }

    STDMETHODIMP SetAppDomainManager(
        /* [in] */ DWORD /*dwAppDomainID*/,
        /* [in] */ IUnknown* /*pUnkAppDomainManager*/)
    {
        return S_OK;
    }
};
