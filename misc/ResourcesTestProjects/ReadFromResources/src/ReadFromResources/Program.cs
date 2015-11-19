using System;
using System.Globalization;
using System.Reflection;
using System.Resources;
using TestClassLibrary;

public class Program
{
    public static void Main(string[] args)
    {
        var resources = new ResourceManager("ReadFromResources.Program", Assembly.Load(new AssemblyName("ReadFromResources")));
        Console.WriteLine(resources.GetString("HelloWorld"));
        Console.WriteLine(resources.GetString("HelloWorld", new CultureInfo("fr-FR")));
        Console.WriteLine(resources.GetString("HelloWorld", new CultureInfo("de")));

        resources = new ResourceManager("ResourcesLibrary.Test", Assembly.Load(new AssemblyName("ResourcesLibrary")));
        Console.WriteLine(resources.GetString("Welcome", new CultureInfo("fr-FR")));

        var edmAssembly = Assembly.Load(new AssemblyName("Microsoft.Data.Edm"));
        var edmResource = new ResourceManager("Microsoft.Data.Edm", edmAssembly);
        Console.WriteLine(edmResource.GetString("Bad_AmbiguousElementBinding"));
        Console.WriteLine(edmResource.GetString("Bad_AmbiguousElementBinding", new CultureInfo("fr")));

        var testClass = new TestClass();
        Console.WriteLine(testClass.Print());
    }
}