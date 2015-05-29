using System;
using System.Reflection;
using System.Threading.Tasks;

namespace dnx.clr.managed
{
    internal class RuntimeBootstrapper
    {
        public static int Execute(string[] argv)
        {
            var executeMethodInfo = GetBootstrapperType()
                .GetMethod("Execute", BindingFlags.Static | BindingFlags.Public, null, new[] { argv.GetType() }, null);
            return (int)executeMethodInfo.Invoke(null, new object[] { argv });
        }

        public static Task<int> ExecuteAsync(string[] argv)
        {
            var executeMethodInfo = GetBootstrapperType()
                .GetMethod("ExecuteAsync", BindingFlags.Static | BindingFlags.Public, null, new[] { argv.GetType() }, null);
            return (Task<int>)executeMethodInfo.Invoke(null, new object[] { argv });
        }

        private static Type GetBootstrapperType()
        {
            var dnxHost = Assembly.Load("dnx.host");
            return dnxHost.GetType("dnx.host.RuntimeBootstrapper");
        }
    }
}
