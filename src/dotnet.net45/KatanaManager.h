#pragma once

#include "stdafx.h"

#include "CriticalSection.h"
#include "ComObject.h"
#include "FileStream.h"

struct ApplicationMainInfo
{
    typedef int(*ApplicationMainDelegate)(int argc, PCWSTR* argv);

    /* in */ ApplicationMainDelegate ApplicationMain;

    /* out */ BSTR ApplicationBase;
};

class __declspec(uuid("7E9C5238-60DC-49D3-94AA-53C91FA79F7C")) IKatanaManager : public IUnknown
{
public:
    virtual HRESULT InitializeRuntime(LPCWSTR applicationBase) = 0;

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

    ICLRMetaHostPtr _MetaHost;
    ICLRMetaHostPolicyPtr _MetaHostPolicy;
    ICLRRuntimeHostPtr _RuntimeHost;

    _bstr_t _clrVersion;
    _bstr_t _appPoolName;
    _bstr_t _appHostFileName;
    _bstr_t _rootWebConfigFileName;
    _bstr_t _clrConfigFilePath;

    _bstr_t _applicationBase;

    ApplicationMainInfo _applicationMainInfo;

public:
    KatanaManager()
    {
        LPWSTR configFileName = L"dotnet.net45.config";
        TCHAR szPath[MAX_PATH];
        DWORD length = GetModuleFileName(NULL, szPath, MAX_PATH);

        _calledInitializeRuntime = false;
        _hrInitializeRuntime = E_PENDING;

        // TODO: Replace this with proper string functions
        int lastSlash = length - 1;
        while (lastSlash >= 0)
        {
            if (szPath[lastSlash] == '\\')
            {
                lastSlash++;
                break;
            }

            lastSlash--;
        }

        // Yoda conditions
        while (NULL != *configFileName && lastSlash < MAX_PATH)
        {
            // Overwrite the file name
            szPath[lastSlash++] = *(configFileName++);
        }
        szPath[lastSlash] = NULL;

        _clrConfigFilePath = szPath;
    }

    IUnknown* CastInterface(REFIID riid)
    {
        if (riid == __uuidof(IKatanaManager))
            return static_cast<IKatanaManager*>(this);
        if (riid == __uuidof(IHostControl))
            return static_cast<IHostControl*>(this);
        return NULL;
    }

    HRESULT InitializeRuntime(LPCWSTR applicationBase)
    {
        Lock lock(&_crit);
        if (_calledInitializeRuntime)
            return _hrInitializeRuntime;

        HRESULT hr = S_OK;

        _applicationBase = applicationBase;

        _HR(CLRCreateInstance(CLSID_CLRMetaHost, PPV(&_MetaHost)));
        _HR(CLRCreateInstance(CLSID_CLRMetaHostPolicy, PPV(&_MetaHostPolicy)));

        IStreamPtr cfgStream = new ComObject<FileStream>();
        _HR(static_cast<FileStream*>(cfgStream.GetInterfacePtr())->Open(_clrConfigFilePath));

        WCHAR wzVersion[130] = { 0 };
        DWORD cchVersion = 129;
        DWORD dwConfigFlags = 0;

        ICLRRuntimeInfoPtr runtimeInfo;
        _HR(_MetaHostPolicy->GetRequestedRuntime(
            METAHOST_POLICY_APPLY_UPGRADE_POLICY,
            NULL,
            cfgStream,
            wzVersion,
            &cchVersion,
            NULL,//wzImageVersion,
            NULL,//&cchImageVersion,
            &dwConfigFlags,
            PPV(&runtimeInfo)));

        cfgStream = NULL;

        _HR(runtimeInfo->SetDefaultStartupFlags(
            STARTUP_LOADER_OPTIMIZATION_MULTI_DOMAIN_HOST |
            STARTUP_SERVER_GC,
            _clrConfigFilePath));

        ICLRRuntimeHostPtr runtimeHost;
        _HR(runtimeInfo->GetInterface(CLSID_CLRRuntimeHost, PPV(&runtimeHost)));

        _HR(runtimeHost->SetHostControl(this));

        _HR(runtimeHost->Start());

        DWORD dwAppDomainId = 0;
        _HR(runtimeHost->GetCurrentAppDomainId(&dwAppDomainId));

        _RuntimeHost = runtimeHost;

        _hrInitializeRuntime = hr;
        _calledInitializeRuntime = TRUE;
        return hr;
    }

    HRESULT BindApplicationMain(ApplicationMainInfo* pInfo)
    {
        _applicationMainInfo = *pInfo;
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
        /* [in] */ DWORD dwAppDomainID,
        /* [in] */ IUnknown *pUnkAppDomainManager)
    {
        HRESULT hr = S_OK;
        return hr;
    }
};
