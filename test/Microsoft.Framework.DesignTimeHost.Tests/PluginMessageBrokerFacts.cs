// Copyright (c) Microsoft Open Technologies, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using Xunit;

namespace Microsoft.Framework.DesignTimeHost
{
    public class PluginMessageBrokerFacts
    {
        [Fact]
        public void SendMessage_CallsIntoSendMessageMethod()
        {
            var called = false;
            var pluginMessageBroker = 
                new PluginMessageBroker(pluginId: Guid.NewGuid().ToString(), sendMessageMethod: (_) => called = true);

            pluginMessageBroker.SendMessage(string.Empty);

            Assert.True(called);
        }

        [Fact]
        public void SendMessage_WrapsData()
        {
            PluginMessageBroker.PluginMessageWrapperData calledWith = null;
            var expectedPluginId = "d81b8ad8-306d-474b-b8a9-b25c7f80be7e"; // Random, hardcoded GUID.
            var pluginMessageBroker = new PluginMessageBroker(
                expectedPluginId,
                sendMessageMethod: (data) => calledWith = (PluginMessageBroker.PluginMessageWrapperData)data);

            pluginMessageBroker.SendMessage("Hello World");

            Assert.NotNull(calledWith);
            Assert.Equal(expectedPluginId, calledWith.PluginId);
            Assert.Equal("Hello World", (string)calledWith.Data, StringComparer.Ordinal);
        }
    }
}