// Copyright (c) Microsoft Open Technologies, Inc. All rights reserved. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Reflection;
using Loader;
using Microsoft.Owin.Hosting.Engine;
using Microsoft.Owin.Hosting.Loader;
using Microsoft.Owin.Hosting.Services;
using WebHost;

namespace Microsoft.Owin.Hosting.Starter
{
    /// <summary>
    /// Used for executing the IHostingEngine in a new AppDomain.
    /// </summary>
    public class Host : MarshalByRefObject, IDisposable
    {
        private bool _disposed;
        private IDisposable _runningApp;
        private DefaultHost _host;

        [SuppressMessage("Microsoft.Reliability", "CA2001:AvoidCallingProblematicMethods", MessageId = "System.Reflection.Assembly.LoadFile", Justification = "By design")]
        [SuppressMessage("Microsoft.Performance", "CA1822:MarkMembersAsStatic", Justification = "Invoked cross domain")]
        public void ResolveAssembliesFromDirectory(string directory)
        {
            var cache = new Dictionary<string, Assembly>();
            AppDomain.CurrentDomain.AssemblyResolve +=
                (a, b) =>
                {
                    Assembly assembly;
                    if (cache.TryGetValue(b.Name, out assembly))
                    {
                        return assembly;
                    }

                    string shortName = new AssemblyName(b.Name).Name;
                    string path = Path.Combine(directory, shortName + ".dll");
                    if (File.Exists(path))
                    {
                        assembly = Assembly.LoadFile(path);
                    }
                    cache[b.Name] = assembly;
                    if (assembly != null)
                    {
                        cache[assembly.FullName] = assembly;
                    }
                    return assembly;
                };
        }

        /// <summary>
        /// Executes the IHostingEngine in a new AppDomain.
        /// </summary>
        /// <param name="options"></param>
        [SuppressMessage("Microsoft.Performance", "CA1822:MarkMembersAsStatic", Justification = "Non-static needed for calling across AppDomain")]
        public void Start(string path, string url)
        {
            var options = new StartOptions();
            options.Urls.Add(url);

            var context = new StartContext(options);

            _host = new DefaultHost(path);
            _host.OnChanged += () =>
            {
                Environment.Exit(250);
            };

            try
            {
                IServiceProvider services = ServicesFactory.Create(context.Options.Settings, sp =>
                {
                    sp.AddInstance<IAppLoader>(new MyAppLoader(_host));
                });

                var engine = services.GetService<IHostingEngine>();

                _runningApp = engine.Start(context);
            }
            catch (Exception ex)
            {
                var inner = GetInnerException(ex);

                // Hacky: We need to have specific loader exceptions
                if (!(inner is InvalidDataException))
                {
                    throw;
                }

                Trace.TraceError(inner.Message);
            }
        }

        private Exception GetInnerException(Exception ex)
        {
            // If the most inner is recoverable then
            while (ex.InnerException != null)
            {
                ex = ex.InnerException;
            }

            return ex;
        }

        /// <summary>
        /// 
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="disposing"></param>
        protected virtual void Dispose(bool disposing)
        {
            if (disposing && !_disposed)
            {
                _disposed = true;
                _host.Dispose();
                _runningApp.Dispose();
            }
        }
    }
}