// Copyright (c) Microsoft Open Technologies, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Linq;
using System.Collections.Generic;
using System.Reflection;
using Microsoft.Framework.DependencyInjection.ServiceLookup;

namespace Microsoft.Framework.Runtime.Common.DependencyInjection
{
    internal class ServiceProvider : IServiceProvider
    {
        private readonly Dictionary<Type, object> _instances = new Dictionary<Type, object>();
        private readonly IServiceProvider _fallbackServiceProvider;

        public ServiceProvider()
        {
            _instances[typeof(IServiceProvider)] = this;
            _instances[typeof(IServiceManifest)] = new ServiceManifest(this);
        }

        public ServiceProvider(IServiceProvider fallbackServiceProvider)
            : this()
        {
            _fallbackServiceProvider = fallbackServiceProvider;
        }

        public void Add(Type type, object instance)
        {
            _instances[type] = instance;
        }

        public object GetService(Type serviceType)
        {
            object instance;
            if (_instances.TryGetValue(serviceType, out instance))
            {
                return instance;
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

                object instance;
                if (_instances.TryGetValue(itemType, out instance))
                {
                    var serviceArray = Array.CreateInstance(itemType, 1);
                    serviceArray.SetValue(instance, 0);
                    return serviceArray;
                }
                else
                {
                    return Array.CreateInstance(itemType, 0);
                }
            }

            return null;
        }

        private IEnumerable<Type> GetAllServices()
        {
            var services = _instances.Keys;

            var fallbackManifest = _fallbackServiceProvider?.GetService(typeof(IServiceManifest)) as IServiceManifest;

            if (fallbackManifest != null)
            {
                return fallbackManifest.Services.Concat(services);
            }

            return services;
        }

        private class ServiceManifest : IServiceManifest
        {
            private readonly ServiceProvider _serviceProvider;

            public ServiceManifest(ServiceProvider serviceProvider)
            {
                _serviceProvider = serviceProvider;
            }

            public IEnumerable<Type> Services
            {
                get
                {
                    return _serviceProvider.GetAllServices().Distinct();
                }
            }
        }
    }
}
