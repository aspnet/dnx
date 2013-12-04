using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using Microsoft.Net.Runtime;

namespace klr.host
{
    public class RootHost : IHost
    {
        private readonly string _application;
        private readonly string _path;
        private readonly string _applicationName;
        readonly IDictionary<string, Assembly> _cache = new Dictionary<string, Assembly>();


        public RootHost(string application)
        {
            _application = application;

            if (_application.EndsWith(".dll"))
            {
                _path = Path.GetDirectoryName(_application);
                _applicationName = Path.GetFileNameWithoutExtension(application);
            }
            else
            {
                throw new ArgumentException(
                    String.Format("application '{0}' is not understood", application),
                    "application");
            }
        }

        public void Dispose()
        {
        }

        public Assembly GetEntryPoint()
        {
            TraceInformation("RootHost.GetEntryPoint GetEntryPoint {0}", _applicationName);
#if DESKTOP
            return Assembly.Load(_applicationName);
#else
            return Assembly.Load(new AssemblyName(_applicationName));
#endif
        }

        public Assembly Load(string name)
        {
            TraceInformation("RootHost.Load name={0}", name);

            Assembly assembly;
            if (_cache.TryGetValue(name, out assembly))
            {
                return assembly;
            }

            var filePath = Path.Combine(_path, name + ".dll");
            if (File.Exists(filePath))
            {
                try
                {
                    TraceInformation("RootHost Assembly.LoadFile({0})", filePath);
                    assembly = Assembly.LoadFile(filePath);
                }
                catch (Exception ex)
                {
                    TraceWarning("Exception {0} loading {1}", ex.Message, filePath);
                }
            }

            _cache[name] = assembly;
            return assembly;
        }

        private void TraceWarning(string format, params object[] args)
        {
#if DESKTOP
            Trace.TraceWarning(format, args);
#endif
        }

        private void TraceInformation(string format, params object[] args)
        {
#if DESKTOP
            Trace.TraceInformation(format, args);
#endif
        }
    }
}
