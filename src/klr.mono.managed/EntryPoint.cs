using System;
using System.IO;
using System.Collections.Generic;
using System.Reflection;
using klr.hosting;

public class EntryPoint
{
    public static int Main(string[] arguments)
    {
        return RuntimeBootstrapper.Execute(arguments).Result;
    }
}