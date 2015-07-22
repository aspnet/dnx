// Copyright (c) .NET Foundation. All rights reserved. 
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information. 

using System.Collections.Generic;
using System.Linq;
using Microsoft.Dnx.Runtime.Common.DependencyInjection;
using Xunit;

namespace Microsoft.Dnx.Runtime.Tests
{
    public class ServiceProviderFacts
    {
        [Fact]
        public void RegisteredServicesCanBeIEnumerableResolved()
        {
            var serviceProvider = new ServiceProvider();
            var service = new Service();

            serviceProvider.Add(typeof(IService), service);

            var serviceList = (IEnumerable<IService>)serviceProvider.GetService(typeof(IEnumerable<IService>));

            Assert.NotNull(serviceList);
            var enumerator = serviceList.GetEnumerator();
            Assert.True(enumerator.MoveNext(), "The serviceList should have 1 element");
            Assert.Same(service, enumerator.Current);
            Assert.False(enumerator.MoveNext(), "The serviceList should have 1 element");
        }


        [Fact]
        public void NonRegisteredServicesCanBeIEnumerableResolved()
        {
            var serviceProvider = new ServiceProvider();

            var serviceList = (IEnumerable<IService>)serviceProvider.GetService(typeof(IEnumerable<IService>));

            Assert.NotNull(serviceList);
            Assert.False(serviceList.Any(), "The serviceList should have no elements.");
        }

        private interface IService
        {
        }

        private class Service : IService
        {
        }
    }
}
