// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

#pragma once

template<class T, class TUnknown = IUnknown>
class ComObject :
    public T
{
    DWORD m_dwRef;
public:
    ComObject()
    {
        m_dwRef = 0;
    }

    STDMETHODIMP QueryInterface(
                /* [in] */ REFIID riid,
                /* [iid_is][out] */ __RPC__deref_out void __RPC_FAR *__RPC_FAR *ppvObject)
    {
        if (riid == __uuidof(IUnknown))
            *ppvObject = static_cast<TUnknown*>(this);
        else
            *ppvObject = CastInterface(riid);

        if (*ppvObject != NULL)
            return ((IUnknown*)*ppvObject)->AddRef(), S_OK;
        return E_NOINTERFACE;
    }

    STDMETHODIMP_(ULONG) AddRef( void)
    {
        return InterlockedIncrement(&m_dwRef);
    }

    STDMETHODIMP_(ULONG) Release( void)
    {
        DWORD dwRef = InterlockedDecrement(&m_dwRef);
        if (dwRef == 0)
        {
            delete this;
        }
        return dwRef;
    }
};
