#include "stdafx.h"
#include "HostAssemblyStore.h"
#include "FileStream.h"
#include "ComObject.h"
#include <string>
#include "utils.h"

const wchar_t* AppDomainManagerAssemblyName = L"Microsoft.Dnx.Host.Clr, Version=1.0.0.0, Culture=neutral, PublicKeyToken=adb9793829ddae60, ProcessorArchitecture=MSIL";

// IHostAssemblyStore
HRESULT STDMETHODCALLTYPE HostAssemblyStore::ProvideAssembly(AssemblyBindInfo *pBindInfo, UINT64* pAssemblyId, UINT64* pContext,
    IStream **ppStmAssemblyImage, IStream **ppStmPDB)
{
    if (_wcsicmp(AppDomainManagerAssemblyName, pBindInfo->lpReferencedIdentity) == 0)
    {
        std::wstring path(m_runtimeDirectory);
        if (path.back() != L'\\')
        {
            path.append(L"\\");
        }
        path.append(L"Microsoft.Dnx.Host.Clr.dll");

        if (path.length() > MAX_PATH || !dnx::utils::file_exists(path))
        {
            return HRESULT_FROM_WIN32(ERROR_FILE_NOT_FOUND);
        }

        auto assembly_stream = new ComObject<FileStream>();
        if (FAILED(assembly_stream->QueryInterface(IID_PPV_ARGS(ppStmAssemblyImage))) ||
            FAILED((static_cast<FileStream*>(*ppStmAssemblyImage))->Open(path.c_str())))
        {
            *ppStmAssemblyImage = NULL;
            delete assembly_stream;

            return HRESULT_FROM_WIN32(ERROR_FILE_NOT_FOUND);
        }

        path.replace(path.length() - 3, 3, L"pdb");
        if (dnx::utils::file_exists(path))
        {
            auto pdb_stream = new ComObject<FileStream>();
            if (FAILED(pdb_stream->QueryInterface(IID_PPV_ARGS(ppStmPDB))) ||
                FAILED((static_cast<FileStream*>(*ppStmPDB))->Open(path.c_str())))
            {
                *ppStmPDB = NULL;
                delete pdb_stream;
            }
        }

        // This is an arbitrary id for the assembly loaded from a stream.
        *pAssemblyId = 42;

        *pContext = 0;

        return S_OK;
    }

    return HRESULT_FROM_WIN32(ERROR_FILE_NOT_FOUND);
}

HRESULT STDMETHODCALLTYPE HostAssemblyStore::ProvideModule(ModuleBindInfo* /*pBindInfo*/, DWORD* /*pdwModuleId*/, IStream** /*ppStmModuleImage*/,
    IStream** /*ppStmPDB*/)
{
    return HRESULT_FROM_WIN32(ERROR_FILE_NOT_FOUND);
}

// IUnknown
HRESULT STDMETHODCALLTYPE HostAssemblyStore::QueryInterface(const IID &/*iid*/, void **ppv)
{
    if (!ppv)
    {
        return E_POINTER;
    }

    *ppv = this;
    AddRef();
    return S_OK;
}

ULONG STDMETHODCALLTYPE HostAssemblyStore::AddRef()
{
    return InterlockedIncrement(&m_RefCount);
}

ULONG STDMETHODCALLTYPE HostAssemblyStore::Release()
{
    if (InterlockedDecrement(&m_RefCount) == 0)
    {
        delete this;
        return 0;
    }
    return m_RefCount;
}