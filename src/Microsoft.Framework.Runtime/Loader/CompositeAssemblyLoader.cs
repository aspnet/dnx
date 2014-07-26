// Copyright (c) Microsoft Open Technologies, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;

namespace Microsoft.Framework.Runtime.Loader
{
    public class CompositeAssemblyLoader : IAssemblyLoader
    {
        private readonly IList<IAssemblyLoader> _loaders;

        public CompositeAssemblyLoader(IList<IAssemblyLoader> loaders)
        {
            _loaders = loaders;
        }

        public Assembly Load(string name)
        {
            var sw = new Stopwatch();
            sw.Start();
            var result = LoadImpl(name, sw);
            sw.Stop();
            return result;
        }

        private Assembly LoadImpl(string name, Stopwatch sw)
        {
            foreach (var loader in _loaders)
            {
                var assembly = loader.Load(name);

                if (assembly != null)
                {
                    return assembly;
                }
            }

            return null;
        }
    }
}
