using System;
using System.Reflection;

namespace EcmaEntryPoint
{
    public static class Startup
    {
        public static void Main(string[] args)
        {
            var a = typeof(Startup).GetTypeInfo().Assembly;
            Console.WriteLine("ECMA EntryPoint Ran");
        }
    }
}
