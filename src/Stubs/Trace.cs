using System;
using System.Reflection;

namespace System.Diagnostics
{
    public static class Trace
    {
        public static void TraceError(string message, params object[] args)
        {
            Console.WriteLine("Error: " + message, args);
        }

        public static void TraceInformation(string message, params object[] args)
        {
            Console.WriteLine("Information: " + message, args);
        }

        public static void TraceWarning(string message, params object[] args)
        {
            Console.WriteLine("Warning: " + message, args);
        }
    }
}