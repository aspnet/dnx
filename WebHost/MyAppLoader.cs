using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Microsoft.Owin.Hosting.Loader;
using Owin;

namespace WebHost
{
    public class MyAppLoader : IAppLoader
    {
        private string _assemblyName;

        public MyAppLoader(string assemblyName)
        {
            _assemblyName = assemblyName;
        }

        public Action<IAppBuilder> Load(string appName, IList<string> errors)
        {
            var asm = Assembly.Load(_assemblyName);
            var type = asm.GetType("Startup") ?? asm.GetTypes().FirstOrDefault(t => t.Name == "Startup");
            var config = type.GetMethod("Configuration");
            var obj = Activator.CreateInstance(type);

            return app =>
            {
                app.Properties["host.AppName"] = _assemblyName;

                config.Invoke(obj, new[] { app });
            };
        }
    }
}
