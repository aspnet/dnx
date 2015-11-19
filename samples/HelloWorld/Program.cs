using System.Reflection;

public class Program
{
    public int Main(string[] args)
    {
        System.Console.WriteLine("Hello World!");

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