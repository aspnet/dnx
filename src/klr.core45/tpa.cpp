// This file will be dynamically updated during build to generate a 
// minimal trusted platform assemblies list

#include "stdafx.h"
#include "tpa.h"

BOOL CreateTpaBase(LPWSTR** ppNames, size_t* pcNames, bool bNative)
{
    const size_t count = 34;
    LPWSTR* pArray = new LPWSTR[count];

    if (bNative)
    {
        pArray[0] = _wcsdup(L"Internal.Runtime.Uri.ni.dll");
        pArray[1] = _wcsdup(L"Internal.Uri.ni.dll");
        pArray[2] = _wcsdup(L"mscorlib.ni.dll");
        pArray[3] = _wcsdup(L"mscorlib.Extensions.ni.dll");
        pArray[4] = _wcsdup(L"System.ni.dll");
        pArray[5] = _wcsdup(L"System.AppContext.ni.dll");
        pArray[6] = _wcsdup(L"System.Collections.ni.dll");
        pArray[7] = _wcsdup(L"System.Collections.Concurrent.ni.dll");
        pArray[8] = _wcsdup(L"System.ComponentModel.ni.dll");
        pArray[9] = _wcsdup(L"System.Console.ni.dll");
        pArray[10] = _wcsdup(L"System.Core.ni.dll");
        pArray[11] = _wcsdup(L"System.Diagnostics.Debug.ni.dll");
        pArray[12] = _wcsdup(L"System.Diagnostics.Tracing.ni.dll");
        pArray[13] = _wcsdup(L"System.Globalization.ni.dll");
        pArray[14] = _wcsdup(L"System.IO.ni.dll");
        pArray[15] = _wcsdup(L"System.IO.FileSystem.ni.dll");
        pArray[16] = _wcsdup(L"System.IO.FileSystem.Primitives.ni.dll");
        pArray[17] = _wcsdup(L"System.Linq.ni.dll");
        pArray[18] = _wcsdup(L"System.Reflection.ni.dll");
        pArray[19] = _wcsdup(L"System.Reflection.Extensions.ni.dll");
        pArray[20] = _wcsdup(L"System.Reflection.Primitives.ni.dll");
        pArray[21] = _wcsdup(L"System.Resources.ResourceManager.ni.dll");
        pArray[22] = _wcsdup(L"System.Runtime.ni.dll");
        pArray[23] = _wcsdup(L"System.Runtime.Extensions.ni.dll");
        pArray[24] = _wcsdup(L"System.Runtime.Handles.ni.dll");
        pArray[25] = _wcsdup(L"System.Runtime.InteropServices.ni.dll");
        pArray[26] = _wcsdup(L"System.Runtime.Loader.ni.dll");
        pArray[27] = _wcsdup(L"System.Text.Encoding.ni.dll");
        pArray[28] = _wcsdup(L"System.Text.Encoding.Extensions.ni.dll");
        pArray[29] = _wcsdup(L"System.Text.RegularExpressions.ni.dll");
        pArray[30] = _wcsdup(L"System.Threading.ni.dll");
        pArray[31] = _wcsdup(L"System.Threading.Overlapped.ni.dll");
        pArray[32] = _wcsdup(L"System.Threading.Tasks.ni.dll");
        pArray[33] = _wcsdup(L"System.Threading.ThreadPool.ni.dll");
    }
    else
    {
        pArray[0] = _wcsdup(L"Internal.Runtime.Uri.dll");
        pArray[1] = _wcsdup(L"Internal.Uri.dll");
        pArray[2] = _wcsdup(L"mscorlib.dll");
        pArray[3] = _wcsdup(L"mscorlib.Extensions.dll");
        pArray[4] = _wcsdup(L"System.dll");
        pArray[5] = _wcsdup(L"System.AppContext.dll");
        pArray[6] = _wcsdup(L"System.Collections.dll");
        pArray[7] = _wcsdup(L"System.Collections.Concurrent.dll");
        pArray[8] = _wcsdup(L"System.ComponentModel.dll");
        pArray[9] = _wcsdup(L"System.Console.dll");
        pArray[10] = _wcsdup(L"System.Core.dll");
        pArray[11] = _wcsdup(L"System.Diagnostics.Debug.dll");
        pArray[12] = _wcsdup(L"System.Diagnostics.Tracing.dll");
        pArray[13] = _wcsdup(L"System.Globalization.dll");
        pArray[14] = _wcsdup(L"System.IO.dll");
        pArray[15] = _wcsdup(L"System.IO.FileSystem.dll");
        pArray[16] = _wcsdup(L"System.IO.FileSystem.Primitives.dll");
        pArray[17] = _wcsdup(L"System.Linq.dll");
        pArray[18] = _wcsdup(L"System.Reflection.dll");
        pArray[19] = _wcsdup(L"System.Reflection.Extensions.dll");
        pArray[20] = _wcsdup(L"System.Reflection.Primitives.dll");
        pArray[21] = _wcsdup(L"System.Resources.ResourceManager.dll");
        pArray[22] = _wcsdup(L"System.Runtime.dll");
        pArray[23] = _wcsdup(L"System.Runtime.Extensions.dll");
        pArray[24] = _wcsdup(L"System.Runtime.Handles.dll");
        pArray[25] = _wcsdup(L"System.Runtime.InteropServices.dll");
        pArray[26] = _wcsdup(L"System.Runtime.Loader.dll");
        pArray[27] = _wcsdup(L"System.Text.Encoding.dll");
        pArray[28] = _wcsdup(L"System.Text.Encoding.Extensions.dll");
        pArray[29] = _wcsdup(L"System.Text.RegularExpressions.dll");
        pArray[30] = _wcsdup(L"System.Threading.dll");
        pArray[31] = _wcsdup(L"System.Threading.Overlapped.dll");
        pArray[32] = _wcsdup(L"System.Threading.Tasks.dll");
        pArray[33] = _wcsdup(L"System.Threading.ThreadPool.dll");
    }

    *ppNames = pArray;
    *pcNames = count;

    return true;
}

BOOL FreeTpaBase(const LPWSTR* values, const size_t count)
{
    for (size_t idx = 0; idx < count; ++idx)
    {
        delete[] values[idx];
    }

    delete[] values;

    return true;
}
