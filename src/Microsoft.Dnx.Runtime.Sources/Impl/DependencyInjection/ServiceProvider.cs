// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Linq;
using System.Collections.Generic;
using System.Reflection;
using Microsoft.Extensions.PlatformAbstractions;

namespace Microsoft.Dnx.Runtime.Common.DependencyInjection
{
    internal class ServiceProvider : IServiceProvider
    {
        private readonly Dictionary<Type, ServiceEntry> _entries = new Dictionary<Type, ServiceEntry>();
        private readonly IServiceProvider _fallbackServiceProvider;

        public ServiceProvider()
        {
            Add(typeof(IServiceProvider), this, includeInManifest: false);
        }

        public ServiceProvider(IServiceProvider fallbackServiceProvider)
            : this()
        {
            _fallbackServiceProvider = fallbackServiceProvider;
        }

        public void Add(Type type, object instance)
        {
            Add(type, instance, includeInManifest: true);
        }

        public void Add(Type type, object instance, bool includeInManifest)
        {
            _entries[type] = new ServiceEntry
            {
                Instance = instance,
                IncludeInManifest = includeInManifest
            };
        }

        public bool TryAdd(Type type, object instance)
        {
            return TryAdd(type, instance, includeInManifest: true);
        }

        public bool TryAdd(Type type, object instance, bool includeInManifest)
        {
            if (GetService(type) == null)
            {
                Add(type, instance, includeInManifest);
                return true;
            }

            return false;
        }

        public object GetService(Type serviceType)
        {
            ServiceEntry entry;
            if (_entries.TryGetValue(serviceType, out entry))
            {
                return entry.Instance;
            }

            Array serviceArray = GetServiceArrayOrNull(serviceType);

            if (serviceArray != null && serviceArray.Length != 0)
            {
                return serviceArray;
            }

            if (_fallbackServiceProvider != null)
            {
                return _fallbackServiceProvider.GetService(serviceType);
            }

            return serviceArray;
        }

        private Array GetServiceArrayOrNull(Type serviceType)
        {
            var typeInfo = serviceType.GetTypeInfo();

            if (typeInfo.IsGenericType &&
                serviceType.GetGenericTypeDefinition() == typeof(IEnumerable<>))
            {
                var itemType = typeInfo.GenericTypeArguments[0];

                ServiceEntry entry;
                if (_entries.TryGetValue(itemType, out entry))
                {
                    var serviceArray = Array.CreateInstance(itemType, 1);
                    serviceArray.SetValue(entry.Instance, 0);
                    return serviceArray;
                }
                else
                {
                    return Array.CreateInstance(itemType, 0);
                }
            }

            return null;
        }

        private class ServiceEntry
        {
            public object Instance { get; set; }
            public bool IncludeInManifest { get; set; }
        }
    }
}
