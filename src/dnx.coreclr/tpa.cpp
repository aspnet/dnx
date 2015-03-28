// This file will be dynamically updated during build to generate a 
// minimal trusted platform assemblies list

#include "stdafx.h"
#include "tpa.h"

BOOL CreateTpaBase(LPCTSTR** ppNames, size_t* pcNames, bool bNative)
{
    const size_t count = 33;
    LPCTSTR* pArray = new LPCTSTR[count];

    if (bNative)
    {
        pArray[0] = _T("dnx.coreclr.managed.ni.dll");
        pArray[1] = _T("dnx.host.ni.dll");
        pArray[2] = _T("Internal.Uri.ni.dll");
        pArray[3] = _T("Microsoft.Framework.Runtime.Interfaces.ni.dll");
        pArray[4] = _T("Microsoft.Framework.Runtime.Loader.ni.dll");
        pArray[5] = _T("mscorlib.ni.dll");
        pArray[6] = _T("System.AppContext.ni.dll");
        pArray[7] = _T("System.Collections.ni.dll");
        pArray[8] = _T("System.Collections.Concurrent.ni.dll");
        pArray[9] = _T("System.ComponentModel.ni.dll");
        pArray[10] = _T("System.Console.ni.dll");
        pArray[11] = _T("System.Diagnostics.Debug.ni.dll");
        pArray[12] = _T("System.Diagnostics.Tracing.ni.dll");
        pArray[13] = _T("System.Globalization.ni.dll");
        pArray[14] = _T("System.IO.ni.dll");
        pArray[15] = _T("System.IO.FileSystem.ni.dll");
        pArray[16] = _T("System.IO.FileSystem.Primitives.ni.dll");
        pArray[17] = _T("System.Linq.ni.dll");
        pArray[18] = _T("System.Reflection.ni.dll");
        pArray[19] = _T("System.Reflection.Extensions.ni.dll");
        pArray[20] = _T("System.Reflection.Primitives.ni.dll");
        pArray[21] = _T("System.Resources.ResourceManager.ni.dll");
        pArray[22] = _T("System.Runtime.ni.dll");
        pArray[23] = _T("System.Runtime.Extensions.ni.dll");
        pArray[24] = _T("System.Runtime.Handles.ni.dll");
        pArray[25] = _T("System.Runtime.InteropServices.ni.dll");
        pArray[26] = _T("System.Runtime.Loader.ni.dll");
        pArray[27] = _T("System.Text.Encoding.ni.dll");
        pArray[28] = _T("System.Text.Encoding.Extensions.ni.dll");
        pArray[29] = _T("System.Threading.ni.dll");
        pArray[30] = _T("System.Threading.Overlapped.ni.dll");
        pArray[31] = _T("System.Threading.Tasks.ni.dll");
        pArray[32] = _T("System.Threading.ThreadPool.ni.dll");
    }
    else
    {
        pArray[0] = _T("dnx.coreclr.managed.dll");
        pArray[1] = _T("dnx.host.dll");
        pArray[2] = _T("Internal.Uri.dll");
        pArray[3] = _T("Microsoft.Framework.Runtime.Interfaces.dll");
        pArray[4] = _T("Microsoft.Framework.Runtime.Loader.dll");
        pArray[5] = _T("mscorlib.dll");
        pArray[6] = _T("System.AppContext.dll");
        pArray[7] = _T("System.Collections.dll");
        pArray[8] = _T("System.Collections.Concurrent.dll");
        pArray[9] = _T("System.ComponentModel.dll");
        pArray[10] = _T("System.Console.dll");
        pArray[11] = _T("System.Diagnostics.Debug.dll");
        pArray[12] = _T("System.Diagnostics.Tracing.dll");
        pArray[13] = _T("System.Globalization.dll");
        pArray[14] = _T("System.IO.dll");
        pArray[15] = _T("System.IO.FileSystem.dll");
        pArray[16] = _T("System.IO.FileSystem.Primitives.dll");
        pArray[17] = _T("System.Linq.dll");
        pArray[18] = _T("System.Reflection.dll");
        pArray[19] = _T("System.Reflection.Extensions.dll");
        pArray[20] = _T("System.Reflection.Primitives.dll");
        pArray[21] = _T("System.Resources.ResourceManager.dll");
        pArray[22] = _T("System.Runtime.dll");
        pArray[23] = _T("System.Runtime.Extensions.dll");
        pArray[24] = _T("System.Runtime.Handles.dll");
        pArray[25] = _T("System.Runtime.InteropServices.dll");
        pArray[26] = _T("System.Runtime.Loader.dll");
        pArray[27] = _T("System.Text.Encoding.dll");
        pArray[28] = _T("System.Text.Encoding.Extensions.dll");
        pArray[29] = _T("System.Threading.dll");
        pArray[30] = _T("System.Threading.Overlapped.dll");
        pArray[31] = _T("System.Threading.Tasks.dll");
        pArray[32] = _T("System.Threading.ThreadPool.dll");
    }

    *ppNames = pArray;
    *pcNames = count;

    return true;
}

BOOL FreeTpaBase(const LPCTSTR* values)
{
    delete[] values;

    return true;
}
