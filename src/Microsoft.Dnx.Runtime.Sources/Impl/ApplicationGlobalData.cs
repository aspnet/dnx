// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Microsoft.Dnx.Runtime.Sources.Impl
{
    // Helper class to completely centralize all of the application global data logic and avoid #if statements elsewhere.
    internal class ApplicationGlobalData
    {
        private readonly IApplicationEnvironment _hostEnvironment;
#if DNXCORE50
        private readonly object _lock;
        private readonly Dictionary<string, object> _appGlobalData;
#endif

        public ApplicationGlobalData(IApplicationEnvironment hostEnvironment)
        {
            _hostEnvironment = hostEnvironment;
#if DNXCORE50
            // If there is no host environment that we are delegating to, we need a place to store this data in CoreCLR
            if (hostEnvironment == null)
            {
                // Initialize a new place to store global data on CoreCLR
                _lock = new object();
                _appGlobalData = new Dictionary<string, object>(StringComparer.Ordinal); // Case-sensitive, just like AppDomain.Get/SetData
            }
#endif
        }

        public object GetData(string name)
        {
            return _hostEnvironment == null ?
                GetDataCore(name) :
                _hostEnvironment.GetData(name);
        }

        public void SetData(string name, object value)
        {
            if (_hostEnvironment == null)
            {
                SetDataCore(name, value);
            }
            else
            {
                _hostEnvironment.SetData(name, value);
            }
        }

#if DNX451
        private object GetDataCore(string name)
        {
            return AppDomain.CurrentDomain.GetData(name);
        }

        private void SetDataCore(string name, object value)
        {
            AppDomain.CurrentDomain.SetData(name, value);
        }
#else
        // NOTE(anurse): ConcurrentDictionary seems overkill here. This data is rarely used, and a global lock seems safer.
        private object GetDataCore(string name)
        {
            lock (_lock)
            {
                object val;
                if (_appGlobalData.TryGetValue(name, out val))
                {
                    return val;
                }
                return null;
            }
        }

        private void SetDataCore(string name, object value)
        {
            lock (_lock)
            {
                _appGlobalData[name] = value;
            }
        }
#endif
    }
}
