using System;
using System.Reflection;
using System.Security;
using System.Threading;
using klr.hosting;

[SecurityCritical]
sealed class DomainManager
{
    [SecurityCritical]
    unsafe static int Execute(int argc, char** argv)
    {
        if (argc == 0)
        {
            return 1;
        }

        // Pack arguments
        var arguments = new string[argc];
        for (var i = 0; i < arguments.Length; i++)
        {
            arguments[i] = new string(argv[i]);
        }

        // TODO: Return a wait handle
        return RuntimeBootstrapper.Execute(arguments).Result;
    }
}