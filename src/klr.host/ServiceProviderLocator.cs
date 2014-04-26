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
        // private const string ServiceProviderDataName = "klr.host.ServiceProviderLocator.ServiceProvider";

        public IServiceProvider ServiceProvider
        {
            // TODO: Figure out how we make this work well on desktop.
            // Since helios does cross app domain calls it means that everything
            // object in the graph of objects added to the service provider needs to be
            // marked [Serializabe]
            // We may need async local on desktop as well
            //get { return (IServiceProvider)CallContext.LogicalGetData(ServiceProviderDataName); }
            //set { CallContext.LogicalSetData(ServiceProviderDataName, value); }

            get { throw new NotSupportedException(); }
            set {  }
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