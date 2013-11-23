using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Loader;
using Microsoft.Owin.Hosting.Loader;
using Owin;

namespace WebHost
{
    public class MyAppLoader : IAppLoader
    {
        private readonly Assembly _assembly;
        private readonly DefaultHost _host;

        public MyAppLoader(DefaultHost host, Assembly assembly)
        {
            _host = host;
            _assembly = assembly;
        }

        public Action<IAppBuilder> Load(string appName, IList<string> errors)
        {
            if (_assembly == null)
            {
                throw new InvalidOperationException("Unable to locate Startup class");
            }

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

                var resolver = _host.GetService<Func<string, object>>(HostServices.ResolveAssemblyReference);

                app.Properties[HostServices.ResolveAssemblyReference] = resolver;

                config.Invoke(obj, new[] { app });
            };
        }
    }
}
