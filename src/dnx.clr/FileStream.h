// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

#pragma once

#include <fileapi.h>
#include <winerror.h>

#pragma warning(push)
#pragma warning(disable: 4100)

class FileStream : public IStream
{
    HANDLE _handle;

public:
    FileStream()
    {
        _handle = INVALID_HANDLE_VALUE;
    }

    ~FileStream()
    {
        Close();
    }

    IUnknown* CastInterface(REFIID riid)
    {
        if (riid == __uuidof(IStream))
            return static_cast<IStream*>(this);
        if (riid == __uuidof(ISequentialStream))
            return static_cast<ISequentialStream*>(this);
        return NULL;
    }

    HRESULT Open(PCWSTR fileName)
    {
        Close();

        _handle = ::CreateFile(
            fileName,
            GENERIC_READ,
            FILE_SHARE_READ |
            FILE_SHARE_WRITE |
            FILE_SHARE_DELETE,
            NULL,
            OPEN_EXISTING,
            FILE_ATTRIBUTE_NORMAL |
            FILE_FLAG_SEQUENTIAL_SCAN,
            NULL);

        if (_handle == INVALID_HANDLE_VALUE)
        {
            return HRESULT_FROM_WIN32(GetLastError());
        }
        return S_OK;
    }

    void Close()
    {
        if (_handle != INVALID_HANDLE_VALUE)
        {
            ::CloseHandle(_handle);
            _handle = INVALID_HANDLE_VALUE;
        }
    }

    ////////////////////////////
    // ISequentialStream

        STDMETHODIMP Read(
            /* [annotation] */
            __out_bcount_part(cb, *pcbRead)  void *pv,
            /* [in] */ ULONG cb,
            /* [annotation] */
            __out_opt  ULONG *pcbRead)
        {
            BOOL result = ReadFile(
                _handle,
                pv,
                cb,
                pcbRead,
                NULL);

            if (result == FALSE)
            {
                return HRESULT_FROM_WIN32(GetLastError());
            }

            return (cb == *pcbRead) ? S_OK : S_FALSE;
        }

        STDMETHODIMP Write(
            /* [annotation] */
            __in_bcount(cb)  const void *pv,
            /* [in] */ ULONG cb,
            /* [annotation] */
            __out_opt  ULONG *pcbWritten)
        {
            return E_NOTIMPL;
        }

    ////////////////////////////
    // IStream

        STDMETHODIMP Seek(
            /* [in] */ LARGE_INTEGER dlibMove,
            /* [in] */ DWORD dwOrigin,
            /* [annotation] */
            __out_opt  ULARGE_INTEGER *plibNewPosition) { return E_NOTIMPL; }

        STDMETHODIMP SetSize(
            /* [in] */ ULARGE_INTEGER libNewSize) { return E_NOTIMPL; }

        STDMETHODIMP CopyTo(
            /* [unique][in] */ IStream *pstm,
            /* [in] */ ULARGE_INTEGER cb,
            /* [annotation] */
            __out_opt  ULARGE_INTEGER *pcbRead,
            /* [annotation] */
            __out_opt  ULARGE_INTEGER *pcbWritten) { return E_NOTIMPL; }

        STDMETHODIMP Commit(
            /* [in] */ DWORD grfCommitFlags) { return E_NOTIMPL; }

        STDMETHODIMP Revert( void) { return E_NOTIMPL; }

        STDMETHODIMP LockRegion(
            /* [in] */ ULARGE_INTEGER libOffset,
            /* [in] */ ULARGE_INTEGER cb,
            /* [in] */ DWORD dwLockType) { return E_NOTIMPL; }

        STDMETHODIMP UnlockRegion(
            /* [in] */ ULARGE_INTEGER libOffset,
            /* [in] */ ULARGE_INTEGER cb,
            /* [in] */ DWORD dwLockType) { return E_NOTIMPL; }

        STDMETHODIMP Stat(
            /* [out] */ __RPC__out STATSTG *pstatstg,
            /* [in] */ DWORD grfStatFlag)
        {
            if (GetFileSizeEx(_handle, (PLARGE_INTEGER)&pstatstg->cbSize) == 0)
            {
                return HRESULT_FROM_WIN32(GetLastError());
            }

            return S_OK;
        }

        STDMETHODIMP Clone(
            /* [out] */ __RPC__deref_out_opt IStream **ppstm) { return E_NOTIMPL; }

};

#pragma warning(pop)