using System;

namespace klr.hosting
{
#if ASPNETCORE50
    internal static class Trace
    {
        public static void TraceError(string message, params object[] args)
        {
            if (IsEnabled)
            {
                Console.WriteLine("Error: " + message, args);
            }
        }

        public static void TraceInformation(string message, params object[] args)
        {
            if (IsEnabled)
            {
                Console.WriteLine("Information: " + message, args);
            }
        }

        public static void TraceWarning(string message, params object[] args)
        {
            if (IsEnabled)
            {
                Console.WriteLine("Warning: " + message, args);
            }
        }

        private static bool IsEnabled
        {
            get
            {
                return Environment.GetEnvironmentVariable("KRE_TRACE") == "1";
            }
        }
    }
#endif
}