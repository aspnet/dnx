using System;
using System.Reflection;

#if !DESKTOP // CORECLR_TODO: Classic tracing.

namespace System.Diagnostics
{
    public static class Trace
    {
        public static void TraceError(string p1, params object[] p2)
        {
        }

        public static void TraceInformation(string p1, params object[] p2)
        {
        }

        public static void TraceWarning(string p1, params object[] p2)
        {
        }
    }
}

#endif
