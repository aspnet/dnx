using System;
using System.Reflection;

namespace System.Diagnostics
{
    public static class Trace
    {
        public static void TraceError(string message, params object[] args)
        {
            Console.WriteLine(message, args);
        }

        public static void TraceInformation(string message, params object[] args)
        {
            Console.WriteLine(message, args);
        }

        public static void TraceWarning(string message, params object[] args)
        {
            Console.WriteLine(message, args);
        }
    }
}