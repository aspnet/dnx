using System;
using Microsoft.Net.Runtime.Infrastructure;
#if NET45
using System.Runtime.Remoting.Messaging;
#else
using System.Threading;
#endif

namespace klr.host
{
    internal class ServiceProviderLocator : IServiceProviderLocator
    {
#if NET45
        public IServiceProvider ServiceProvider
        {
            get { return (IServiceProvider)CallContext.LogicalGetData(GetType().Name); }
            set { CallContext.LogicalSetData(GetType().Name, value); }
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