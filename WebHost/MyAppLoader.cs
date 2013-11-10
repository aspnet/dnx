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
        private Assembly _assembly;

        public MyAppLoader(Assembly assembly)
        {
            _assembly = assembly;
        }

        public Action<IAppBuilder> Load(string appName, IList<string> errors)
        {
            var type = _assembly.GetType("Startup") ?? _assembly.GetTypes().FirstOrDefault(t => t.Name == "Startup");

            if(type == null)
            {
                throw new InvalidOperationException("Unable to locate Startup class");
            }

            var config = type.GetMethod("Configuration");
            var obj = Activator.CreateInstance(type);

            return app =>
            {
                app.Properties["host.AppName"] = _assembly.GetName().Name;

                config.Invoke(obj, new[] { app });
            };
        }
    }
}
