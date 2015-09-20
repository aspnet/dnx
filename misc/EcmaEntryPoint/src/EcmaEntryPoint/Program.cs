using System;

namespace EcmaEntryPoint
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var a = typeof(Program).Assembly;
            Console.WriteLine("EntryPoint: {0}", a.EntryPoint.Name);
        }
    }
}
