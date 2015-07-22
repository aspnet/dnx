// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using Microsoft.Dnx.Runtime;
using Microsoft.Dnx.Runtime.Infrastructure;
#if DNX451
using System.Runtime.Remoting.Messaging;
using System.Runtime.Remoting;
#else
using System.Threading;
#endif

namespace Microsoft.Dnx.Host
{
    internal class ServiceProviderLocator : IServiceProviderLocator
    {
#if DNX451
        private const string ServiceProviderDataName = Constants.BootstrapperHostName + ".ServiceProviderLocator.ServiceProvider";

        public IServiceProvider ServiceProvider
        {
            get
            {
                var handle = CallContext.LogicalGetData(ServiceProviderDataName) as ObjectHandle;

                if (handle == null)
                {
                    // Using AppDomain because of an Execution Context bug in older versions of Mono
                    return (IServiceProvider)AppDomain.CurrentDomain.GetData("DNX_SERVICEPROVIDER");
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
