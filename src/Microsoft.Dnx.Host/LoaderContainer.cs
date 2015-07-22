// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using Microsoft.Dnx.Runtime;

namespace Microsoft.Dnx.Host
{
    public class LoaderContainer : IAssemblyLoaderContainer
    {
        private readonly Stack<IAssemblyLoader> _loaders = new Stack<IAssemblyLoader>();

        public IDisposable AddLoader(IAssemblyLoader loader)
        {
            _loaders.Push(loader);

            return new DisposableAction(() =>
            {
                var removed = _loaders.Pop();
                if (!ReferenceEquals(loader, removed))
                {
                    throw new InvalidOperationException("TODO: Loader scopes being disposed in wrong order");
                }
            });
        }

        public Assembly Load(AssemblyName assemblyName)
        {
            Logger.TraceInformation("[{0}]: Load name={1}", GetType().Name, assemblyName);
            var sw = Stopwatch.StartNew();

            foreach (var loader in _loaders.Reverse())
            {
                var assembly = loader.Load(assemblyName);
                if (assembly != null)
                {
                    Logger.TraceInformation("[{0}]: Loaded name={1} in {2}ms", loader.GetType().Name, assemblyName, sw.ElapsedMilliseconds);
                    return assembly;
                }
            }

            return null;
        }

        private class DisposableAction : IDisposable
        {
            private readonly Action _action;
            public DisposableAction(Action action)
            {
                _action = action;
            }

            public void Dispose()
            {
                _action();
            }
        }
    }
}
