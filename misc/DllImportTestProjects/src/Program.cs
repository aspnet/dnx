using System;
using System.Runtime.InteropServices;
using Microsoft.Extensions.PlatformAbstractions;

namespace ProjectReferenceTest
{
    public class Program
    {
        public static void Main()
        {
            Console.WriteLine($"The answer is {get_number()}");
        }

        private static int get_number()
        {
            var runtimeEnvironment = PlatformServices.Default.Runtime;

            if (runtimeEnvironment.OperatingSystem == "Mac OS X" && runtimeEnvironment.RuntimeType == "Mono")
            {
                return NativeLibDarwinMono.get_number();
            }
            return NativeLib.get_number();
        }

        private static class NativeLib
        {
            [DllImport("nativelib")]
            public static extern int get_number();
        }

        private static class NativeLibDarwinMono
        {
            [DllImport("__Internal")]
            public static extern int get_number();
        }
    }
}
