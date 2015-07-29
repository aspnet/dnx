using System;
using System.Globalization;
using System.Reflection;
using System.Resources;

public class Program
{
    public static void Main(string[] args)
    {
        var resources = new ResourceManager("HelloWorld.Program", Assembly.Load(new AssemblyName("HelloWorld")));
        Console.WriteLine(resources.GetString("HelloWorld"));
        Console.WriteLine(resources.GetString("HelloWorld", new CultureInfo("fr-FR")));

        //resources = new ResourceManager("ResourcesLibrary.Test", Assembly.Load(new AssemblyName("ResourcesLibrary")));
        //Console.WriteLine(resources.GetString("Welcome", new CultureInfo("fr-FR")));

        var edmAssembly = Assembly.Load(new AssemblyName("Microsoft.Data.Edm"));
        var edmResource = new ResourceManager("Microsoft.Data.Edm", edmAssembly);
        Console.WriteLine(edmResource.GetString("Bad_CyclicEntity"));
        Console.WriteLine(edmResource.GetString("Bad_CyclicEntity", new CultureInfo("fr")));
    }
}