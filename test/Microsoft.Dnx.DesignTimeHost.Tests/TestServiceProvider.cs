// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Reflection;

namespace Microsoft.Dnx.DesignTimeHost
{
    public class TestServiceProvider : IServiceProvider
    {
        private readonly IReadOnlyDictionary<Type, object> _getServiceLookup;

        public TestServiceProvider()
            : this(new Dictionary<Type, object>())
        {
        }

        public TestServiceProvider(IReadOnlyDictionary<Type, object> getServiceLookup)
        {
            _getServiceLookup = getServiceLookup;
        }

        public object GetService(Type serviceType)
        {
            object service;
            _getServiceLookup.TryGetValue(serviceType, out service);

            return service;
        }
    }
}