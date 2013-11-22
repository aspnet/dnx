using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using Loader;

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
                    "application argument format is not understood",
                    "application");
            }
        }

        public void Dispose()
        {
        }

        public Assembly GetEntryPoint()
        {
            Trace.TraceInformation("RootHost.GetEntryPoint GetEntryPoint {0}", _applicationName);
            return Assembly.Load(_applicationName);
        }

        public Assembly Load(string name)
        {
            Trace.TraceInformation("RootHost.Load name={0}", name);

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
                    Trace.TraceInformation("RootHost Assembly.LoadFile({0})", filePath);
                    assembly = Assembly.LoadFile(filePath);
                }
                catch (Exception ex)
                {
                    Trace.TraceWarning("Exception {0} loading {1}", ex.Message, filePath);
                }
            }

            _cache[name] = assembly;
            return assembly;
        }
    }
}
