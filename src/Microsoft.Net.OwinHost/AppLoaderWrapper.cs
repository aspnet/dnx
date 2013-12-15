using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using Microsoft.Net.Runtime;
using Microsoft.Owin.Hosting.Loader;
using Owin;

namespace Microsoft.Net.OwinHost
{
    using AppLoaderFunc = Func<string, IList<string>, Action<IAppBuilder>>;

    public class AppLoaderWrapper : IAppLoaderFactory
    {
        private readonly DefaultHost _host;

        public AppLoaderWrapper(DefaultHost host)
        {
            _host = host;
        }

        public int Order
        {
            get { return -150; }
        }

        public AppLoaderFunc Create(AppLoaderFunc nextLoader)
        {
            return (name, errors) =>
            {
                Assembly assembly = null;

                string error = null;

                try
                {
                    assembly = _host.GetEntryPoint();

                    if (assembly == null)
                    {
                        error = "Unable to resolve the application entry point.";
                    }
                }
                catch (Exception ex)
                {
                    error = ex.Message;
                }

                if (assembly == null)
                {
                    return app =>
                    {
                        app.Run(async context =>
                        {
                            context.Response.ContentType = "text/plain";
                            await context.Response.WriteAsync(error);
                        });
                    };
                }

                // determine actual startup class to load
                if (string.IsNullOrEmpty(name))
                {
                    var startupType = assembly.DefinedTypes.SingleOrDefault(t => string.Equals(t.Name, "Startup", StringComparison.OrdinalIgnoreCase));
                    if (startupType != null)
                    {
                        name = startupType.AssemblyQualifiedName;
                    }
                }

                // pass through to other loaders
                var config = nextLoader.Invoke(name, errors);

                // don't wrap null result
                if (config == null)
                {
                    return null;
                }

                // but do wrap configuration if startup was found
                return app =>
                {
                    app.Properties["host.AppName"] = assembly.GetName().Name;

                    var resolver = _host.GetService<Func<string, object>>(HostServices.ResolveAssemblyReference);

                    app.Properties[HostServices.ResolveAssemblyReference] = resolver;

                    config(app);
                };
            };
        }
    }
}
