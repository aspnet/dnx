// Copyright (c) Microsoft Open Technologies, Inc. All rights reserved. See License.txt in the project root for license information.

using System;
using System.Diagnostics.CodeAnalysis;
using System.IO;

namespace Microsoft.Owin.Hosting.Starter
{
    /// <summary>
    /// Creates a new AppDomain to run the IHostingEngine in.
    /// </summary>
    public class HostStarter
    {
        /// <summary>
        /// Creates a new AppDomain to run the IHostingEngine in.
        /// </summary>
        /// <param name="options"></param>
        /// <returns></returns>
        [SuppressMessage("Microsoft.Reliability", "CA2000:Dispose objects before losing scope", Justification = "Disposed by caller")]
        public virtual IDisposable Start(string path, string url)
        {
            var info = new AppDomainSetup
            {
                ApplicationBase = path,
                PrivateBinPath = "bin",
                PrivateBinPathProbe = "*",
                LoaderOptimization = LoaderOptimization.MultiDomainHost,
                ConfigurationFile = Path.Combine(path, "web.config")
            };

            AppDomain domain = AppDomain.CreateDomain("OWIN", null, info);

            Host agent = CreateAgent(domain);

            agent.ResolveAssembliesFromDirectory(AppDomain.CurrentDomain.SetupInformation.ApplicationBase);

            agent.Start(path, url);

            return agent;
        }

        [SuppressMessage("Microsoft.Design", "CA1031:DoNotCatchGeneralExceptionTypes", Justification = "Fallback code")]
        private static Host CreateAgent(AppDomain domain)
        {
            try
            {
                return (Host)domain.CreateInstanceAndUnwrap(
                    typeof(Host).Assembly.FullName,
                    typeof(Host).FullName);
            }
            catch
            {
                return (Host)domain.CreateInstanceFromAndUnwrap(
                    typeof(Host).Assembly.Location,
                    typeof(Host).FullName);
            }
        }
    }
}