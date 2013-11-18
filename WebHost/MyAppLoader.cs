using System;
using System.Collections.Generic;
using System.Linq;
using Loader;
using Microsoft.Owin.Hosting.Loader;
using Owin;

namespace WebHost
{
    public class MyAppLoader : IAppLoader
    {
        private readonly DefaultHost _host;

        public MyAppLoader(DefaultHost host)
        {
            _host = host;
        }

        public Action<IAppBuilder> Load(string appName, IList<string> errors)
        {
            var assembly = _host.GetEntryPoint();

            var type = assembly.GetType("Startup") ?? assembly.GetTypes().FirstOrDefault(t => t.Name == "Startup");

            if(type == null)
            {
                throw new InvalidOperationException("Unable to locate Startup class");
            }

            var config = type.GetMethod("Configuration");
            var obj = Activator.CreateInstance(type);

            return app =>
            {
                app.Properties["host.AppName"] = assembly.GetName().Name;

                var resolver = _host.GetService<Func<string, object>>(HostServices.ResolveAssemblyReference);

                app.Properties[HostServices.ResolveAssemblyReference] = resolver;

                config.Invoke(obj, new[] { app });
            };
        }
    }
}
