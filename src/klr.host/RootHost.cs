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
        private readonly IDictionary<string, Assembly> _cache = new Dictionary<string, Assembly>();
        private readonly string[] _searchPaths;
        private readonly IAssemblyLoaderEngine _loaderEngine;

        public RootHost(IAssemblyLoaderEngine loaderEngine, string[] searchPaths)
        {
            _loaderEngine = loaderEngine;
            _searchPaths = searchPaths;
        }

        public void Dispose()
        {
        }

        public Assembly Load(string name)
        {
            Trace.TraceInformation("RootHost.Load name={0}", name);

            foreach (var path in _searchPaths)
            {
                var filePath = Path.Combine(path, name + ".dll");
                if (File.Exists(filePath))
                {
                    try
                    {
                        Trace.TraceInformation("RootHost Assembly.LoadFile({0})", filePath);

                        return _loaderEngine.LoadFile(filePath);
                    }
                    catch (Exception ex)
                    {
                        Trace.TraceWarning("Exception {0} loading {1}", ex.Message, filePath);
                    }
                }
            }

            return null;
        }
    }
}
