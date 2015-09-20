using System;
using System.Reflection;

namespace EcmaEntryPoint
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var a = typeof(Program).GetTypeInfo().Assembly;
            Console.WriteLine("EntryPoint: {0}", a.EntryPoint.Name);
        }
    }
}
