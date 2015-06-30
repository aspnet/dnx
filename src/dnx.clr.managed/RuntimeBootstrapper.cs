using System;
using System.Reflection;
using System.Runtime.Versioning;
using System.Threading.Tasks;

namespace dnx.clr.managed
{
    internal class RuntimeBootstrapper
    {
        public static int Execute(string[] argv, FrameworkName targetFramework)
        {
            var executeMethodInfo = GetBootstrapperType()
                .GetMethod("Execute", BindingFlags.Static | BindingFlags.Public, null, new[] { typeof(string[]), typeof(FrameworkName) }, null);
            return (int)executeMethodInfo.Invoke(null, new object[] { argv, targetFramework });
        }

        public static Task<int> ExecuteAsync(string[] argv, FrameworkName targetFramework)
        {
            var executeMethodInfo = GetBootstrapperType()
                .GetMethod("ExecuteAsync", BindingFlags.Static | BindingFlags.Public, null, new[] { typeof(string[]), typeof(FrameworkName) }, null);
            return (Task<int>)executeMethodInfo.Invoke(null, new object[] { argv, targetFramework });
        }

        private static Type GetBootstrapperType()
        {
            var dnxHost = Assembly.Load("dnx.host");
            return dnxHost.GetType("dnx.host.RuntimeBootstrapper");
        }
    }
}
