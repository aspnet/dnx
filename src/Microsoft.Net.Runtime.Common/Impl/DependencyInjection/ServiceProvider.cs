using System;
using System.Linq;
using System.Collections.Generic;

namespace Microsoft.Net.Runtime.Common.DependencyInjection
{
    internal class ServiceProvider : IServiceProvider
    {
        private readonly Dictionary<Type, object> _instances = new Dictionary<Type, object>();

        public ServiceProvider()
        {
            _instances[typeof(IServiceProvider)] = this;
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

            return null;
        }
    }
}
