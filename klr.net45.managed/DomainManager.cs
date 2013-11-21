using System;
using System.Reflection;
using System.Runtime.InteropServices;

public class DomainManager : AppDomainManager
{
    private ApplicationMainInfo _info;

    public override void InitializeNewDomain(AppDomainSetup appDomainInfo)
    {
        _info.Main = Main;
        BindApplicationMain(ref _info);

        // TODO: edit appdomain startup info
    }

    private int Main(int argc, string[] argv)
    {
        try
        {
            var assembly = Assembly.Load("klr.host");
            var bootstrapperType = assembly.GetType("Bootstrapper");
            var mainMethod = bootstrapperType.GetMethod("Main");
            var bootstrapper = Activator.CreateInstance(bootstrapperType);
            var result = mainMethod.Invoke(bootstrapper, new object[] {argc, argv});
            return (int)result;
        }
        catch (Exception ex)
        {
            return 1;
        }
    }

    [DllImport("klr.net45.dll")]
    private extern static void BindApplicationMain(ref ApplicationMainInfo info);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    public struct ApplicationMainInfo
    {
        [MarshalAs(UnmanagedType.FunctionPtr)]
        public MainDelegate Main;
    }

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    public delegate int MainDelegate(
        int argc,
        [MarshalAs(UnmanagedType.LPArray, ArraySubType = UnmanagedType.LPWStr, SizeParamIndex = 0)] String[] argv);
}
