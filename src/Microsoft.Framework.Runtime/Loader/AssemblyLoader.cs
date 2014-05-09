// Copyright (c) Microsoft Open Technologies, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;

namespace Microsoft.Framework.Runtime.Loader
{
    public class AssemblyLoader : IAssemblyLoader
    {
        private readonly IList<IAssemblyLoader> _loaders;

        public AssemblyLoader(IList<IAssemblyLoader> loaders)
        {
            _loaders = loaders;
        }

        public Assembly LoadAssembly(LoadContext loadContext)
        {
            var result = Load(loadContext);

            if (result == null)
            {
                return null;
            }

            if (result.Errors != null)
            {
                throw new Exception(String.Join(Environment.NewLine, result.Errors));
            }

            return result.Assembly;
        }

        public AssemblyLoadResult Load(LoadContext loadContext)
        {
            var sw = new Stopwatch();
            sw.Start();
            Trace.TraceInformation("Loading {0} for '{1}'.", loadContext.AssemblyName, loadContext.TargetFramework);
            var result = LoadImpl(loadContext, sw);
            sw.Stop();
            return result;
        }

        private AssemblyLoadResult LoadImpl(LoadContext loadContext, Stopwatch sw)
        {
            foreach (var loader in _loaders)
            {
                var loadResult = loader.Load(loadContext);

                if (loadResult != null)
                {
                    Trace.TraceInformation("[{0}]: Finished loading {1} in {2}ms", loader.GetType().Name, loadContext.AssemblyName, sw.ElapsedMilliseconds);

                    return loadResult;
                }
            }
            return null;
        }
    }
}
