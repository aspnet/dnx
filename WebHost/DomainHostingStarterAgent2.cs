// Copyright (c) Microsoft Open Technologies, Inc. All rights reserved. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Reflection;
using System.Runtime.Remoting;
using System.Runtime.Remoting.Lifetime;
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
    public class DomainHostingStarterAgent2 : IDisposable
    {
        private bool _disposed;
        private IDisposable _runningApp;
        private DefaultHost _host;

        /// <summary>
        /// Executes the IHostingEngine in a new AppDomain.
        /// </summary>
        /// <param name="options"></param>
        [SuppressMessage("Microsoft.Performance", "CA1822:MarkMembersAsStatic", Justification = "Non-static needed for calling across AppDomain")]
        public void Start(StartOptions options)
        {
            var context = new StartContext(options);

            // Project directory
            string path = options.Settings["directory"];

            _host = new DefaultHost(path);
            _host.OnChanged += () =>
            {
                Environment.Exit(250);
            };

            _host.Execute(name =>
            {
                IServiceProvider services = ServicesFactory.Create(context.Options.Settings, sp =>
                {
                    sp.AddInstance<IAppLoader>(new MyAppLoader(name));
                });

                var engine = services.GetService<IHostingEngine>();

                _runningApp = engine.Start(context);
            });
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