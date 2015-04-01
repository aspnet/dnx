using System;
using System.Globalization;
using System.Reflection;
using System.Resources;

namespace HelloWorld
{
    public class Program
    {
        public int Main(string[] args)
        {
            var resources = new ResourceManager(typeof(Program));
            Console.WriteLine(resources.GetString("HelloWorld"));
            Console.WriteLine(resources.GetString("HelloWorld", new CultureInfo("fr-FR")));

            var edmAssembly = Assembly.Load(new AssemblyName("Microsoft.Data.Edm"));
            var edmResource = new ResourceManager("Microsoft.Data.Edm", edmAssembly);
            Console.WriteLine(edmResource.GetString("Bad_CyclicEntity"));
            Console.WriteLine(edmResource.GetString("Bad_CyclicEntity", new CultureInfo("fr-FR")));

            var resourceStream = typeof(Program).GetTypeInfo().Assembly.GetManifestResourceStream("HelloWorld.compiler.resources.HTMLPage1.html");

            if (resourceStream == null)
            {
                return 1;
            }
            // System.Console.WriteLine(new Foo().Message);
            System.Console.WriteLine(HelloShared.HelloSharedCode.SharedMethod());
            foreach (var arg in args)
            {
                System.Console.WriteLine(arg);
            }

            return 0;
        }
    }
}