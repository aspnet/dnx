using System;
using System.Runtime.CompilerServices;

namespace Microsoft.Framework.Runtime
{
    // Tracer for the new runtime. Eventually this will replace Logger :)
    internal static class RuntimeTrace
    {
        public static void TraceError(string message, [CallerMemberName] string member = null, [CallerFilePath] string file = null, [CallerLineNumber] int line = 0)
        {
            Logger.TraceError("[{0}] {1}", message, member);
        }

        public static void TraceInformation(string message, [CallerMemberName] string member = null, [CallerFilePath] string file = null, [CallerLineNumber] int line = 0)
        {
            Logger.TraceInformation("[{0}] {1}", message, member);
        }

        public static void TraceWarning(string message, [CallerMemberName] string member = null, [CallerFilePath] string file = null, [CallerLineNumber] int line = 0)
        {
            Logger.TraceWarning("[{0}] {1}", message, member);
        }
    }
}