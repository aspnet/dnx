// Copyright (c) Microsoft Open Technologies, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using Microsoft.Framework.Runtime;

namespace klr.host
{
    public class RootHost : IHost
    {
        private static readonly string[] _extensions = new string[] { ".dll", ".exe" };

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
            var sw = Stopwatch.StartNew();
            Trace.TraceInformation("[{0}]: Load name={1}", GetType().Name, name);

            foreach (var path in _searchPaths)
            {
                foreach (var extension in _extensions)
                {
                    var filePath = Path.Combine(path, name + extension);

                    if (File.Exists(filePath))
                    {
                        var assembly = _loaderEngine.LoadFile(filePath);

                        sw.Stop();

                        Trace.TraceInformation("[{0}]: Loaded name={1} in {2}ms", GetType().Name, name, sw.ElapsedMilliseconds);

                        return assembly;
                    }
                }
            }

            sw.Stop();

            return null;
        }
    }
}
