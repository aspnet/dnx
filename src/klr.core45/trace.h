#pragma once

#define MAX_BUFFER_TRACE (512+2)

// If HOST_TRACE_MESSAGE_ENABLED is not defined
// trace functions compile away to nothing
#ifndef HOST_TRACE_MESSAGE_ENABLED

#define TRACE_ODS_OUTPUT(s)
#define TRACE_ODS(s) 
#define TRACE_ODS1(s,p1) 
#define TRACE_ODS2(s,p1,p2) 
#define TRACE_ODS3(s,p1,p2,p3) 
#define TRACE_ODS4(s,p1,p2,p3,p4) 
#define TRACE_ODS5(s,p1,p2,p3,p4,p5) 
#define TRACE_ODS6(s,p1,p2,p3,p4,p5,p6) 
#define TRACE_ODS7(s,p1,p2,p3,p4,p5,p6,p7) 
#define TRACE_ODS8(s,p1,p2,p3,p4,p5,p6,p7,p8) 
#define TRACE_ODS9(s,p1,p2,p3,p4,p5,p6,p7,p8,p9) 
#define TRACE_ODS10(s,p1,p2,p3,p4,p5,p6,p7,p8,p9,p10) 
#define TRACE_ODS11(s,p1,p2,p3,p4,p5,p6,p7,p8,p9,p10,p11) 
#define TRACE_ODS12(s,p1,p2,p3,p4,p5,p6,p7,p8,p9,p10,p11,p12) 
#define TRACE_ODS14(s,p1,p2,p3,p4,p5,p6,p7,p8,p9,p10,p11,p12,p13,p14) 
#define TRACE_ODS15(s,p1,p2,p3,p4,p5,p6,p7,p8,p9,p10,p11,p12,p13,p14,p15) 

#else

extern bool g_fKiteSystemTrace;
extern bool g_fKiteSystemTraceDefault;

void TraceHr(wchar_t *strMessage, HRESULT hr);
void Trace_Console(const wchar_t *val);
void HResultToString(int val, wchar_t * strMessage, size_t cchMessage);

#define TRACE_ODS_OUTPUT(s) {  if (g_fKiteSystemTrace) {OutputDebugStringW(s);  Trace_Console(s);} }

#define TRACE_ODS(s) { WCHAR sTrace[MAX_BUFFER_TRACE]; swprintf_s(sTrace,_countof(sTrace), s); TRACE_ODS_OUTPUT(sTrace); } 
#define TRACE_ODS1(s,p1) { WCHAR sTrace[MAX_BUFFER_TRACE]; swprintf_s(sTrace,_countof(sTrace), s,p1); TRACE_ODS_OUTPUT(sTrace); } 
#define TRACE_ODS2(s,p1,p2) { WCHAR sTrace[MAX_BUFFER_TRACE]; swprintf_s(sTrace,_countof(sTrace), s,p1,p2); TRACE_ODS_OUTPUT(sTrace); } 
#define TRACE_ODS3(s,p1,p2,p3) { WCHAR sTrace[MAX_BUFFER_TRACE]; swprintf_s(sTrace,_countof(sTrace), s,p1,p2,p3); TRACE_ODS_OUTPUT(sTrace); } 
#define TRACE_ODS4(s,p1,p2,p3,p4) { WCHAR sTrace[MAX_BUFFER_TRACE]; swprintf_s(sTrace,_countof(sTrace), s,p1,p2,p3,p4); TRACE_ODS_OUTPUT(sTrace); } 
#define TRACE_ODS5(s,p1,p2,p3,p4,p5) { WCHAR sTrace[MAX_BUFFER_TRACE]; swprintf_s(sTrace,_countof(sTrace), s,p1,p2,p3,p4,p5); TRACE_ODS_OUTPUT(sTrace); } 
#define TRACE_ODS6(s,p1,p2,p3,p4,p5,p6) { WCHAR sTrace[MAX_BUFFER_TRACE]; swprintf_s(sTrace,_countof(sTrace), s,p1,p2,p3,p4,p5,p6); TRACE_ODS_OUTPUT(sTrace); } 
#define TRACE_ODS7(s,p1,p2,p3,p4,p5,p6,p7) { WCHAR sTrace[MAX_BUFFER_TRACE]; swprintf_s(sTrace,_countof(sTrace), s,p1,p2,p3,p4,p5,p6,p7); TRACE_ODS_OUTPUT(sTrace); } 
#define TRACE_ODS8(s,p1,p2,p3,p4,p5,p6,p7,p8) { WCHAR sTrace[MAX_BUFFER_TRACE]; swprintf_s(sTrace,_countof(sTrace), s,p1,p2,p3,p4,p5,p6,p7,p8); TRACE_ODS_OUTPUT(sTrace); } 
#define TRACE_ODS9(s,p1,p2,p3,p4,p5,p6,p7,p8,p9) { WCHAR sTrace[MAX_BUFFER_TRACE]; swprintf_s(sTrace,_countof(sTrace), s,p1,p2,p3,p4,p5,p6,p7,p8,p9); TRACE_ODS_OUTPUT(sTrace); } 
#define TRACE_ODS10(s,p1,p2,p3,p4,p5,p6,p7,p8,p9,p10) { WCHAR sTrace[MAX_BUFFER_TRACE]; swprintf_s(sTrace,_countof(sTrace), s,p1,p2,p3,p4,p5,p6,p7,p8,p9,p10); TRACE_ODS_OUTPUT(sTrace); } 
#define TRACE_ODS11(s,p1,p2,p3,p4,p5,p6,p7,p8,p9,p10,p11) { WCHAR sTrace[MAX_BUFFER_TRACE]; swprintf_s(sTrace,_countof(sTrace), s,p1,p2,p3,p4,p5,p6,p7,p8,p9,p10,p11); TRACE_ODS_OUTPUT(sTrace); } 
#define TRACE_ODS12(s,p1,p2,p3,p4,p5,p6,p7,p8,p9,p10,p11,p12) { WCHAR sTrace[MAX_BUFFER_TRACE]; swprintf_s(sTrace,_countof(sTrace), s,p1,p2,p3,p4,p5,p6,p7,p8,p9,p10,p11,p12); TRACE_ODS_OUTPUT(sTrace); } 
#define TRACE_ODS14(s,p1,p2,p3,p4,p5,p6,p7,p8,p9,p10,p11,p12,p13,p14) { WCHAR sTrace[MAX_BUFFER_TRACE]; swprintf_s(sTrace,_countof(sTrace), s,p1,p2,p3,p4,p5,p6,p7,p8,p9,p10,p11,p12,p13,p14); TRACE_ODS_OUTPUT(sTrace); } 
#define TRACE_ODS15(s,p1,p2,p3,p4,p5,p6,p7,p8,p9,p10,p11,p12,p13,p14,p15) { WCHAR sTrace[MAX_BUFFER_TRACE]; swprintf_s(sTrace,_countof(sTrace), s,p1,p2,p3,p4,p5,p6,p7,p8,p9,p10,p11,p12,p13,p14,p15); TRACE_ODS_OUTPUT(sTrace); } 

#define TRACE_ODS_HR(s,hr) { TraceHr(s,hr); }

#endif
