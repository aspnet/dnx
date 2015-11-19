using System;
using System.Reflection;
using System.Runtime.Versioning;
using System.Threading.Tasks;

namespace Microsoft.Dnx.Host.Clr
{
    internal class RuntimeBootstrapper
    {
        public static int Execute(string[] argv, FrameworkName targetFramework, DomainManager.ApplicationMainInfo info)
        {
            var bootstrapperContext = GetBootstrapperContext(targetFramework, info);
            var bootstrapperType = bootstrapperContext.GetType().Assembly.GetType("Microsoft.Dnx.Host.RuntimeBootstrapper");

            var executeMethodInfo = bootstrapperType.GetMethod("Execute", BindingFlags.Static | BindingFlags.Public, null,
                    new[] { typeof(string[]), bootstrapperContext.GetType() }, null);

            return (int)executeMethodInfo.Invoke(null, new object[] { argv, bootstrapperContext });
        }

        private static object GetBootstrapperContext(FrameworkName targetFramework, DomainManager.ApplicationMainInfo info)
        {
            var dnxHost = Assembly.Load("Microsoft.Dnx.Host");
            var contextType = dnxHost.GetType("Microsoft.Dnx.Host.BootstrapperContext");
            var bootstrapperContext = Activator.CreateInstance(contextType);
            contextType.GetProperty("OperatingSystem").SetValue(bootstrapperContext, info.OperatingSystem);
            contextType.GetProperty("OsVersion").SetValue(bootstrapperContext, info.OsVersion);
            contextType.GetProperty("Architecture").SetValue(bootstrapperContext, info.Architecture);
            contextType.GetProperty("RuntimeDirectory").SetValue(bootstrapperContext, info.RuntimeDirectory);
            contextType.GetProperty("ApplicationBase").SetValue(bootstrapperContext, info.ApplicationBase);
            contextType.GetProperty("TargetFramework").SetValue(bootstrapperContext, targetFramework);
            contextType.GetProperty("RuntimeType").SetValue(bootstrapperContext, "Clr");

            return bootstrapperContext;
        }
    }
}
