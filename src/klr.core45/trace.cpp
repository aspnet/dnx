#include "stdafx.h"

bool     g_fDebugSkipDebugBreak = false;
bool     g_fDebugTriggerDebugBreak = false;
bool     g_fDebugSkipDebugSleep = false;
bool     g_fKiteSystemTrace = false;

void TraceHr(wchar_t *strMessage, HRESULT hr)
{
    wchar_t wszError[MAX_PATH];
    HResultToString(hr, wszError, _countof(wszError));

    TRACE_ODS3(L"%s. ERRORCODE: %08x/%s", strMessage, hr,  wszError);
}

void Trace_Console(const wchar_t *val) 
{
    // If val is longer than 2048 characters, wprintf will refuse to print it.
    // So write it in chunks.

    const size_t chunkSize = 1024;
    wchar_t chunk[chunkSize];
    auto valLength = ::wcslen(val);

    for (size_t i = 0 ; i < valLength ; i += chunkSize) 
    {
        ::wcsncpy_s(chunk, chunkSize, val + i, _TRUNCATE);
        ::wprintf(L"%s", chunk);
    }

    ::wprintf(L"\n\r");
}

void HResultToString(int val, wchar_t * strMessage, size_t cchMessage) 
{
    const wchar_t * str = nullptr;

    switch (val) {
    case 0x00000000: str = L"S_OK"; break;
    case 0x00000001: str = L"S_FALSE"; break;
    case 0x8000000B: str = L"E_BOUNDS"; break;
    case 0x8000000C: str = L"E_CHANGED_STATE"; break;
    case 0x80000013: str = L"RO_E_CLOSED"; break;
    case 0x8000211D: str = L"COR_E_AMBIGUOUSMATCH"; break;
    case 0x80004001: str = L"E_NOTIMPL"; break;
    case 0x80004002: str = L"COR_E_INVALIDCAST"; break;
        //case 0x80004002: str = L"E_NOINTERFACE"; break;
    case 0x80004003: str = L"COR_E_NULLREFERENCE"; break;
        //case 0x80004003: str = L"E_POINTER"; break;
    case 0x80004004: str = L"E_ABORT"; break;
    case 0x80004005: str = L"E_FAIL"; break;
    case 0x8000FFFF: str = L"E_UNEXPECTED"; break;
    case 0x8002000a: str = L"DISP_E_OVERFLOW"; break;
    case 0x8002000e: str = L"COR_E_TARGETPARAMCOUNT"; break;
    case 0x80020012: str = L"COR_E_DIVIDEBYZERO"; break;
    case 0x80028ca0: str = L"TYPE_E_TYPEMISMATCH"; break;
    case 0x80070005: str = L"COR_E_UNAUTHORIZEDACCESS"; break;
        //case 0x80070005: str = L"E_ACCESSDENIED"; break;
    case 0x80070006: str = L"E_HANDLE"; break;
    case 0x8007000B: str = L"COR_E_BADIMAGEFORMAT"; break;
    case 0x8007000E: str = L"COR_E_OUTOFMEMORY"; break;
        //case 0x8007000E: str = L"E_OUTOFMEMORY"; break;
    case 0x80070057: str = L"COR_E_ARGUMENT"; break;
        //case 0x80070057: str = L"E_INVALIDARG"; break;
    case 0x80070216: str = L"COR_E_ARITHMETIC"; break;
    case 0x800703E9: str = L"COR_E_STACKOVERFLOW"; break;
    case 0x80090020: str = L"NTE_FAIL"; break;
    case 0x80131013: str = L"COR_E_TYPEUNLOADED"; break;
    case 0x80131014: str = L"COR_E_APPDOMAINUNLOADED"; break;
    case 0x80131015: str = L"COR_E_CANNOTUNLOADAPPDOMAIN"; break;
    case 0x80131040: str = L"FUSION_E_REF_DEF_MISMATCH"; break;
    case 0x80131047: str = L"FUSION_E_INVALID_NAME"; break;
    case 0x80131416: str = L"CORSEC_E_POLICY_EXCEPTION"; break;
    case 0x80131417: str = L"CORSEC_E_MIN_GRANT_FAIL"; break;
    case 0x80131418: str = L"CORSEC_E_NO_EXEC_PERM"; break;
        //case 0x80131418: str = L"CORSEC_E_XMLSYNTAX"; break;
    case 0x80131430: str = L"CORSEC_E_CRYPTO"; break;
    case 0x80131431: str = L"CORSEC_E_CRYPTO_UNEX_OPER"; break;
    case 0x80131500: str = L"COR_E_EXCEPTION"; break;
    case 0x80131501: str = L"COR_E_SYSTEM"; break;
    case 0x80131502: str = L"COR_E_ARGUMENTOUTOFRANGE"; break;
    case 0x80131503: str = L"COR_E_ARRAYTYPEMISMATCH"; break;
    case 0x80131504: str = L"COR_E_CONTEXTMARSHAL"; break;
    case 0x80131505: str = L"COR_E_TIMEOUT"; break;
    case 0x80131506: str = L"COR_E_EXECUTIONENGINE"; break;
    case 0x80131507: str = L"COR_E_FIELDACCESS"; break;
    case 0x80131508: str = L"COR_E_INDEXOUTOFRANGE"; break;
    case 0x80131509: str = L"COR_E_INVALIDOPERATION"; break;
    case 0x8013150A: str = L"COR_E_SECURITY"; break;
    case 0x8013150C: str = L"COR_E_SERIALIZATION"; break;
    case 0x8013150D: str = L"COR_E_VERIFICATION"; break;
    case 0x80131510: str = L"COR_E_METHODACCESS"; break;
    case 0x80131511: str = L"COR_E_MISSINGFIELD"; break;
    case 0x80131512: str = L"COR_E_MISSINGMEMBER"; break;
    case 0x80131513: str = L"COR_E_MISSINGMETHOD"; break;
    case 0x80131514: str = L"COR_E_MULTICASTNOTSUPPORTED"; break;
    case 0x80131515: str = L"COR_E_NOTSUPPORTED"; break;
    case 0x80131516: str = L"COR_E_OVERFLOW"; break;
    case 0x80131517: str = L"COR_E_RANK"; break;
    case 0x80131518: str = L"COR_E_SYNCHRONIZATIONLOCK"; break;
    case 0x80131519: str = L"COR_E_THREADINTERRUPTED"; break;
    case 0x8013151A: str = L"COR_E_MEMBERACCESS"; break;
    case 0x80131520: str = L"COR_E_THREADSTATE"; break;
    case 0x80131521: str = L"COR_E_THREADSTOP"; break;
    case 0x80131522: str = L"COR_E_TYPELOAD"; break;
    case 0x80131523: str = L"COR_E_ENTRYPOINTNOTFOUND"; break;
        //case 0x80131523: str = L"COR_E_UNSUPPORTEDFORMAT"; break;
    case 0x80131524: str = L"COR_E_DLLNOTFOUND"; break;
    case 0x80131525: str = L"COR_E_THREADSTART"; break;
    case 0x80131527: str = L"COR_E_INVALIDCOMOBJECT"; break;
    case 0x80131528: str = L"COR_E_NOTFINITENUMBER"; break;
    case 0x80131529: str = L"COR_E_DUPLICATEWAITOBJECT"; break;
    case 0x8013152B: str = L"COR_E_SEMAPHOREFULL"; break;
    case 0x8013152C: str = L"COR_E_WAITHANDLECANNOTBEOPENED"; break;
    case 0x8013152D: str = L"COR_E_ABANDONEDMUTEX"; break;
    case 0x80131530: str = L"COR_E_THREADABORTED"; break;
    case 0x80131531: str = L"COR_E_INVALIDOLEVARIANTTYPE"; break;
    case 0x80131532: str = L"COR_E_MISSINGMANIFESTRESOURCE"; break;
    case 0x80131533: str = L"COR_E_SAFEARRAYTYPEMISMATCH"; break;
    case 0x80131534: str = L"COR_E_TYPEINITIALIZATION"; break;
    case 0x80131535: str = L"COR_E_COMEMULATE"; break;
        //case 0x80131535: str = L"COR_E_MARSHALDIRECTIVE"; break;
    case 0x80131536: str = L"COR_E_MISSINGSATELLITEASSEMBLY"; break;
    case 0x80131537: str = L"COR_E_FORMAT"; break;
    case 0x80131538: str = L"COR_E_SAFEARRAYRANKMISMATCH"; break;
    case 0x80131539: str = L"COR_E_PLATFORMNOTSUPPORTED"; break;
    case 0x8013153A: str = L"COR_E_INVALIDPROGRAM"; break;
    case 0x8013153B: str = L"COR_E_OPERATIONCANCELED"; break;
    case 0x8013153D: str = L"COR_E_INSUFFICIENTMEMORY"; break;
    case 0x8013153E: str = L"COR_E_RUNTIMEWRAPPED"; break;
    case 0x80131541: str = L"COR_E_DATAMISALIGNED"; break;
    case 0x80131543: str = L"COR_E_TYPEACCESS"; break;
    case 0x80131577: str = L"COR_E_KEYNOTFOUND"; break;
    case 0x80131578: str = L"COR_E_INSUFFICIENTEXECUTIONSTACK"; break;
    case 0x80131600: str = L"COR_E_APPLICATION"; break;
    case 0x80131601: str = L"COR_E_INVALIDFILTERCRITERIA"; break;
    case 0x80131602: str = L"COR_E_REFLECTIONTYPELOAD   "; break;
    case 0x80131603: str = L"COR_E_TARGET"; break;
    case 0x80131604: str = L"COR_E_TARGETINVOCATION"; break;
    case 0x80131605: str = L"COR_E_CUSTOMATTRIBUTEFORMAT"; break;
    case 0x80131622: str = L"COR_E_OBJECTDISPOSED"; break;
    case 0x80131623: str = L"COR_E_SAFEHANDLEMISSINGATTRIBUTE"; break;
    case 0x80131640: str = L"COR_E_HOSTPROTECTION"; break;
    default: str=L"unknown";break;
    }

    if (str != nullptr) 
    {
        ::wcsncpy_s(strMessage, cchMessage, str, wcslen(str));
    }

    //Host_Trace(L"0x%x/%0s",val, (str != nullptr ) ? str : L"");
}


/* -- copy and paste this fragment into code block to debug

Host_DebugBreakAndSkip();
g_fDebugSkipDebugBreak = FALSE;
*/

VOID Host_DebugBreakAndSkip()
{
    if (g_fDebugSkipDebugBreak == FALSE)
    {
        DebugBreak();
    }
}

// Tip from bilal to debug-attach-break into pico child process
// Let process spin in loop and then debug attach
VOID Host_DebugBreakPico()
{
    if (g_fDebugSkipDebugSleep == FALSE)
    {
        int x = 120; //retries
        while (x > 0)
        {
            Sleep(1000);
            //debug-attach to process and set breakpoint on line after Sleep
            x--;
        }
    }
}

VOID Host_DebugBreakTrigger_Pico_StringContains(LPWSTR pwszCompare1, size_t cchCompare1, LPWSTR pwszCompare2, size_t cchCompare2)
{
    UNREFERENCED_PARAMETER(cchCompare1);
    UNREFERENCED_PARAMETER(cchCompare2);

    //Does Compare1 contain Compare2 ?
    if (wcsstr(pwszCompare1,pwszCompare2) != NULL)
    {
        TRACE_ODS2(L"Seaching --%s-- for -%s- FOUND", pwszCompare1, pwszCompare2);
        g_fDebugTriggerDebugBreak = TRUE;
    }
    else
    {
        TRACE_ODS2(L"Seaching --%s-- for -%s- NOT FOUND", pwszCompare1, pwszCompare2);
    }
}

VOID Host_DebugBreakTrigger_Pico_Monitor()
{
    if (g_fDebugTriggerDebugBreak == TRUE)
    {
        Host_DebugBreakPico();
    }
}

VOID Host_DebugBreakPico_StringContains(LPWSTR pwszCompare1, size_t cchCompare1, LPWSTR pwszCompare2, size_t cchCompare2)
{
    Host_DebugBreakTrigger_Pico_StringContains(pwszCompare1, cchCompare1, pwszCompare2, cchCompare2);
    Host_DebugBreakTrigger_Pico_Monitor();
}
