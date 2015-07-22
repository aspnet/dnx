// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using Microsoft.Dnx.DesignTimeHost.Models;
using Newtonsoft.Json.Linq;
using Xunit;

namespace Microsoft.Dnx.DesignTimeHost.Tests
{
    public class ProtocolManagerTests
    {
        [Fact]
        public void CreateProtocolManagerWithVersion()
        {
            var version = 101;
            var mgr = new ProtocolManager(version);

            Assert.NotNull(mgr);
            Assert.Equal(version, mgr.MaxVersion);
            Assert.Equal(1, mgr.CurrentVersion);
            Assert.False(mgr.EnvironmentOverridden);
        }

        [Theory]
        [InlineData(1, 1, 1)]
        [InlineData(2, 1, 1)]
        [InlineData(3, 1, 1)]
        [InlineData(3, 2, 2)]
        [InlineData(1, 2, 1)]
        [InlineData(1, 3, 1)]
        [InlineData(2, 3, 2)]
        public void NegotiateVersion(int highestVersion, int requestVersion, int expectResult)
        {
            var mgr = new ProtocolManager(highestVersion);
            var message = new Message
            {
                ContextId = 0,
                HostId = Guid.NewGuid().ToString(),
                MessageType = ProtocolManager.NegotiationMessageTypeName,
                Sender = null,  // send is allowed to be null
                Payload = JToken.FromObject(new { Version = requestVersion })
            };

            mgr.Negotiate(message);

            Assert.Equal(expectResult, mgr.CurrentVersion);
        }

        [Fact]
        public void NegotiateMissingVersion()
        {
            var mgr = new ProtocolManager(5);
            var message = new Message
            {
                MessageType = ProtocolManager.NegotiationMessageTypeName
            };

            Assert.Equal(1, mgr.CurrentVersion);
        }

        [Fact]
        public void NegotiateVersionZeroIsIgnored()
        {
            var mgr = new ProtocolManager(5);
            var message = new Message
            {
                MessageType = ProtocolManager.NegotiationMessageTypeName,
                Payload = JToken.FromObject(new { Version = 0 })
            };

            Assert.Equal(1, mgr.CurrentVersion);
        }

        [Fact]
        public void IsProtocolNegotiationPositive()
        {
            var mgr = new ProtocolManager(0);
            var message = new Message
            {
                MessageType = ProtocolManager.NegotiationMessageTypeName
            };

            Assert.True(mgr.IsProtocolNegotiation(message));
        }

        [Fact]
        public void IsProtocolNegotiationWrongMessageTypeName()
        {
            var mgr = new ProtocolManager(4);
            var message = new Message
            {
                MessageType = "Initialization"
            };

            Assert.False(mgr.IsProtocolNegotiation(message));
        }


        [Fact]
        public void IsProtocolNegotiationNullMessage()
        {
            var mgr = new ProtocolManager(4);

            Assert.False(mgr.IsProtocolNegotiation(null));
        }
    }
}
