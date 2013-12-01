using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.InteropServices;

public class DomainManager : AppDomainManager
{
    private ApplicationMainInfo _info;
    private string _originalApplicationBase;

    public override void InitializeNewDomain(AppDomainSetup appDomainInfo)
    {
        _info.Main = Main;
        BindApplicationMain(ref _info);
        
        if (!string.IsNullOrEmpty(_info.ApplicationBase))
        {
            _originalApplicationBase = appDomainInfo.ApplicationBase;
            appDomainInfo.ApplicationBase = _info.ApplicationBase;
        }
    }

    private int Main(int argc, string[] argv)
    {
        try
        {
            if (_originalApplicationBase != null)
            {
                AppDomain.CurrentDomain.AssemblyResolve += CurrentDomainOnAssemblyResolve;
            }
            Assembly.Load("Microsoft.Net.Runtime.Interfaces, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null");
            var assembly = Assembly.Load("klr.host");
            if (_originalApplicationBase != null)
            {
                AppDomain.CurrentDomain.AssemblyResolve -= CurrentDomainOnAssemblyResolve;
            }

            var bootstrapperType = assembly.GetType("Bootstrapper");
            var mainMethod = bootstrapperType.GetMethod("Main");
            var bootstrapper = Activator.CreateInstance(bootstrapperType);
            var result = mainMethod.Invoke(bootstrapper, new object[] { argc, argv });
            return (int)result;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(String.Join(Environment.NewLine, GetExceptions(ex)));
            return 1;
        }
    }

    private Assembly CurrentDomainOnAssemblyResolve(object sender, ResolveEventArgs args)
    {
        var name = new AssemblyName(args.Name).Name;
        var path = System.IO.Path.Combine(_originalApplicationBase, name + ".dll");
        Assembly assembly = Assembly.LoadFile(path);
        return assembly;
    }

    private static IEnumerable<string> GetExceptions(Exception ex)
    {
        if (ex.InnerException != null)
        {
            foreach (var e in GetExceptions(ex.InnerException))
            {
                yield return e;
            }
        }

        if (!(ex is TargetInvocationException))
        {
            yield return ex.ToString();
        }
    }

    [DllImport("klr.net45.dll")]
    private extern static void BindApplicationMain(ref ApplicationMainInfo info);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode, Pack = 1)]
    public struct ApplicationMainInfo
    {
        [MarshalAs(UnmanagedType.FunctionPtr)]
        public MainDelegate Main;

        [MarshalAs(UnmanagedType.BStr)]
        public String ApplicationBase;
    }

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    public delegate int MainDelegate(
        int argc,
        [MarshalAs(UnmanagedType.LPArray, ArraySubType = UnmanagedType.LPWStr, SizeParamIndex = 0)] String[] argv);
}
