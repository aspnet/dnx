// Copyright (c) Microsoft Open Technologies, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using Microsoft.Framework.Runtime.Infrastructure;
#if ASPNET50
using System.Runtime.Remoting.Messaging;
using System.Runtime.Remoting;
#else
using System.Threading;
#endif

namespace dotnet.host
{
    internal class ServiceProviderLocator : IServiceProviderLocator
    {
#if ASPNET50
        private const string ServiceProviderDataName = "dotnet.host.ServiceProviderLocator.ServiceProvider";

        public IServiceProvider ServiceProvider
        {
            get
            {
                var handle = CallContext.LogicalGetData(ServiceProviderDataName) as ObjectHandle;

                if (handle == null)
                {
                    return null;
                }

                return handle.Unwrap() as IServiceProvider;
            }
            set
            {
                CallContext.LogicalSetData(ServiceProviderDataName, new ObjectHandle(value));
            }
        }
#else
        private readonly AsyncLocal<IServiceProvider> _serviceProvider = new AsyncLocal<IServiceProvider>();

        public IServiceProvider ServiceProvider
        {
            get { return _serviceProvider.Value; }
            set { _serviceProvider.Value = value; }
        }
#endif
    }
}
