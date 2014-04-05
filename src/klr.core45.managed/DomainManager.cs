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

        TuneThreadPool();

        // TODO: Return a wait handle
        return RuntimeBootstrapper.Execute(arguments).Result;
    }

    private static void TuneThreadPool()
    {
        var threadPoolType = typeof(ThreadPool);
        int minWorker = 0;
        int minIOC = 0;

        // Obtain current MinIO Threads
        var argsGet = new object[] { minWorker, minIOC };
        threadPoolType.GetTypeInfo().GetDeclaredMethod("GetMinThreads").Invoke(null, argsGet);

        // Future: We can tune the minWorker thread count depending on loads we test
        minWorker = Environment.ProcessorCount * 64;
        minIOC = (int)argsGet[1];

        threadPoolType.GetTypeInfo().GetDeclaredMethod("SetMinThreads").Invoke(null, new object[] { minWorker, minIOC });
    }
}