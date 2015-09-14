using System;
using System.Reflection;
using System.Runtime.Versioning;
using System.Threading.Tasks;

namespace Microsoft.Dnx.Host.Clr
{
    internal class RuntimeBootstrapper
    {
        public static int Execute(string[] argv, FrameworkName targetFramework)
        {
            var bootstrapperContext = GetBootstrapperContext();

            var executeMethodInfo = GetBootstrapperType(bootstrapperContext.GetType().Assembly)
                .GetMethod("Execute", BindingFlags.Static | BindingFlags.Public, null,
                    new[] { typeof(string[]), typeof(FrameworkName), bootstrapperContext.GetType() }, null);
            return (int)executeMethodInfo.Invoke(null, new object[] { argv, targetFramework, null });
        }

        // This method is only called by Helios
        public static Task<int> ExecuteAsync(string[] argv, FrameworkName targetFramework)
        {
            var bootstrapperContext = GetBootstrapperContext();

            var executeMethodInfo = GetBootstrapperType(bootstrapperContext.GetType().Assembly)
                .GetMethod("ExecuteAsync", BindingFlags.Static | BindingFlags.Public, null,
                    new[] { typeof(string[]), typeof(FrameworkName), bootstrapperContext.GetType() }, null);
            return (Task<int>)executeMethodInfo.Invoke(null, new object[] { argv, targetFramework, null });
        }

        private static Type GetBootstrapperType(Assembly dnxHost)
        {
            return dnxHost.GetType("Microsoft.Dnx.Host.RuntimeBootstrapper");
        }

        private static object GetBootstrapperContext()
        {
            var dnxHost = Assembly.Load("Microsoft.Dnx.Host");
            var bootstrapperContext = Activator.CreateInstance(dnxHost.GetType("Microsoft.Dnx.Host.BootstrapperContext"));
            return bootstrapperContext;
        }
    }
}
