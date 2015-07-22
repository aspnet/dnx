// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Reflection;
using Microsoft.Dnx.DesignTimeHost.Models.IncomingMessages;
using Microsoft.Dnx.DesignTimeHost.Models.OutgoingMessages;
using Microsoft.Dnx.Runtime;
using Newtonsoft.Json.Linq;
using Xunit;

namespace Microsoft.Dnx.DesignTimeHost
{
    public class PluginHandlerFacts
    {
        private const string RandomGuidId = "d81b8ad8-306d-474b-b8a9-b25c7f80be7e";

        [Fact]
        public void ProcessMessages_UnregisterPlugin_RemovesFaultedPlugins()
        {
            var pluginType = typeof(MessageBrokerCreationTestPlugin);
            var typeNameLookups = new Dictionary<string, Type>
            {
                { pluginType.FullName, pluginType }
            };
            var testAssembly = new TestAssembly(typeNameLookups);
            var assemblyLookups = new Dictionary<string, Assembly>
            {
                { "CustomAssembly", testAssembly }
            };
            var assemblyLoadContext = new FailureAssemblyLoadContext(assemblyLookups);
            var creationChecker = new PluginTypeCreationChecker();
            var serviceLookups = new Dictionary<Type, object>
            {
                { typeof(PluginTypeCreationChecker), creationChecker }
            };
            var serviceProvider = new TestServiceProvider(serviceLookups);
            object rawMessageBrokerData = null;
            var pluginHandler = new PluginHandler(serviceProvider, (data) => rawMessageBrokerData = data);
            var registerPluginMessage = new PluginMessage
            {
                Data = new JObject
                {
                    { "AssemblyName", "CustomAssembly" },
                    { "TypeName", typeof(MessageBrokerCreationTestPlugin).FullName },
                },
                MessageName = "RegisterPlugin",
                PluginId = RandomGuidId
            };
            var unregisterPluginMessage = new PluginMessage
            {
                MessageName = "UnregisterPlugin",
                PluginId = RandomGuidId
            };

            pluginHandler.OnReceive(registerPluginMessage);
            pluginHandler.ProcessMessages(assemblyLoadContext);

            var messageBrokerData = Assert.IsType<PluginMessageBroker.PluginMessageWrapperData>(rawMessageBrokerData);
            Assert.Equal(RandomGuidId, messageBrokerData.PluginId);
            PluginResponseMessage responseMessage =
                Assert.IsType<RegisterPluginResponseMessage>(messageBrokerData.Data);
            Assert.False(responseMessage.Success);
            Assert.Equal("RegisterPlugin", responseMessage.MessageName, StringComparer.Ordinal);
            Assert.NotEmpty(responseMessage.Error);
            Assert.False(creationChecker.Created);

            pluginHandler.OnReceive(unregisterPluginMessage);
            pluginHandler.ProcessMessages(assemblyLoadContext);

            messageBrokerData = Assert.IsType<PluginMessageBroker.PluginMessageWrapperData>(rawMessageBrokerData);
            Assert.Equal(RandomGuidId, messageBrokerData.PluginId);
            responseMessage = Assert.IsType<PluginResponseMessage>(messageBrokerData.Data);
            Assert.True(responseMessage.Success);
            Assert.Equal("UnregisterPlugin", responseMessage.MessageName, StringComparer.Ordinal);
            Assert.Null(responseMessage.Error);
            Assert.False(creationChecker.Created);
        }

        [Fact]
        public void TryRegisterFaultedPlugins_RecoversFaultedPluginRegistrations()
        {
            var pluginType = typeof(MessageBrokerCreationTestPlugin);
            var typeNameLookups = new Dictionary<string, Type>
            {
                { pluginType.FullName, pluginType }
            };
            var testAssembly = new TestAssembly(typeNameLookups);
            var assemblyLookups = new Dictionary<string, Assembly>
            {
                { "CustomAssembly", testAssembly }
            };
            var assemblyLoadContext = new FailureAssemblyLoadContext(assemblyLookups);
            var creationChecker = new PluginTypeCreationChecker();
            var serviceLookups = new Dictionary<Type, object>
            {
                { typeof(PluginTypeCreationChecker), creationChecker }
            };
            var serviceProvider = new TestServiceProvider(serviceLookups);
            object rawMessageBrokerData = null;
            var pluginHandler = new PluginHandler(serviceProvider, (data) => rawMessageBrokerData = data);
            var registerPluginMessage = new PluginMessage
            {
                Data = new JObject
                {
                    { "AssemblyName", "CustomAssembly" },
                    { "TypeName", typeof(MessageBrokerCreationTestPlugin).FullName },
                },
                MessageName = "RegisterPlugin",
                PluginId = RandomGuidId
            };

            pluginHandler.OnReceive(registerPluginMessage);
            pluginHandler.ProcessMessages(assemblyLoadContext);

            var messageBrokerData = Assert.IsType<PluginMessageBroker.PluginMessageWrapperData>(rawMessageBrokerData);
            Assert.Equal(RandomGuidId, messageBrokerData.PluginId);
            var responseMessage = Assert.IsType<RegisterPluginResponseMessage>(messageBrokerData.Data);
            Assert.False(responseMessage.Success);
            Assert.Equal("RegisterPlugin", responseMessage.MessageName, StringComparer.Ordinal);
            Assert.NotEmpty(responseMessage.Error);
            Assert.False(creationChecker.Created);

            pluginHandler.TryRegisterFaultedPlugins(assemblyLoadContext);

            messageBrokerData = Assert.IsType<PluginMessageBroker.PluginMessageWrapperData>(rawMessageBrokerData);
            Assert.Equal(RandomGuidId, messageBrokerData.PluginId);
            responseMessage = Assert.IsType<RegisterPluginResponseMessage>(messageBrokerData.Data);
            Assert.True(responseMessage.Success);
            Assert.Equal("RegisterPlugin", responseMessage.MessageName, StringComparer.Ordinal);
            Assert.Equal(1, responseMessage.Protocol);
            Assert.Null(responseMessage.Error);
            Assert.True(creationChecker.Created);
        }

        [Fact]
        public void ProcessMessages_NoOpsWithoutMessagesEnqueued()
        {
            var assemblyLoadContext = CreateTestAssemblyLoadContext<TestPlugin>();
            var serviceProvider = new TestServiceProvider();
            object rawMessageBrokerData = null;
            var pluginHandler = new PluginHandler(serviceProvider, (data) => rawMessageBrokerData = data);

            pluginHandler.ProcessMessages(assemblyLoadContext);

            Assert.Null(rawMessageBrokerData);
        }

        [Fact]
        public void TryRegisterFaultedPlugins_NoOpsWithoutMessagesEnqueued()
        {
            var assemblyLoadContext = CreateTestAssemblyLoadContext<CreationTestPlugin>();
            var serviceProvider = new TestServiceProvider();
            object rawMessageBrokerData = null;
            var pluginHandler = new PluginHandler(serviceProvider, (data) => rawMessageBrokerData = data);

            pluginHandler.TryRegisterFaultedPlugins(assemblyLoadContext);

            Assert.Null(rawMessageBrokerData);
        }

        [Fact]
        public void OnReceive_DoesNotProcessMessages()
        {
            object rawMessageBrokerData = null;
            var serviceProvider = new TestServiceProvider();
            var pluginHandler = new PluginHandler(serviceProvider, (data) => rawMessageBrokerData = data);
            var registerPluginMessage = new PluginMessage
            {
                Data = new JObject
                {
                    { "AssemblyName", "CustomAssembly" },
                    { "TypeName", typeof(TestPlugin).FullName },
                },
                MessageName = "RegisterPlugin",
                PluginId = RandomGuidId
            };
            var unregisterPluginMessage = new PluginMessage
            {
                MessageName = "UnregisterPlugin",
                PluginId = RandomGuidId
            };
            var pluginMessage = new PluginMessage
            {
                Data = new JObject
                {
                    { "Data", "Hello Plugin" },
                },
                MessageName = "PluginMessage",
                PluginId = RandomGuidId
            };

            pluginHandler.OnReceive(registerPluginMessage);
            pluginHandler.OnReceive(pluginMessage);
            pluginHandler.OnReceive(unregisterPluginMessage);

            Assert.Null(rawMessageBrokerData);
        }

        [Fact]
        public void ProcessMessages_RegisterPlugin_CreatesPlugin()
        {
            var assemblyLoadContext = CreateTestAssemblyLoadContext<CreationTestPlugin>();
            var creationChecker = new PluginTypeCreationChecker();
            var serviceLookups = new Dictionary<Type, object>
            {
                { typeof(PluginTypeCreationChecker), creationChecker }
            };
            var serviceProvider = new TestServiceProvider(serviceLookups);
            var pluginHandler = new PluginHandler(serviceProvider, (_) => { });
            var pluginMessage = new PluginMessage
            {
                Data = new JObject
                {
                    { "AssemblyName", "CustomAssembly" },
                    { "TypeName", typeof(CreationTestPlugin).FullName },
                },
                MessageName = "RegisterPlugin",
                PluginId = RandomGuidId
            };

            pluginHandler.OnReceive(pluginMessage);
            pluginHandler.ProcessMessages(assemblyLoadContext);

            Assert.True(creationChecker.Created);
        }

        [Fact]
        public void ProcessMessages_RegisterPlugin_DoesNotCacheCreatedPlugin()
        {
            var assemblyLoadContext = CreateTestAssemblyLoadContext<CreationTestPlugin>();
            var creationChecker = new PluginTypeCreationChecker();
            var serviceLookups = new Dictionary<Type, object>
            {
                { typeof(PluginTypeCreationChecker), creationChecker }
            };
            var serviceProvider = new TestServiceProvider(serviceLookups);
            var pluginHandler = new PluginHandler(serviceProvider, (_) => { });
            var pluginMessage = new PluginMessage
            {
                Data = new JObject
                {
                    { "AssemblyName", "CustomAssembly" },
                    { "TypeName", typeof(CreationTestPlugin).FullName },
                },
                MessageName = "RegisterPlugin",
                PluginId = RandomGuidId
            };

            pluginHandler.OnReceive(pluginMessage);
            pluginHandler.ProcessMessages(assemblyLoadContext);
            Assert.True(creationChecker.Created);
            creationChecker.Created = false;
            pluginHandler.OnReceive(pluginMessage);
            pluginHandler.ProcessMessages(assemblyLoadContext);
            Assert.True(creationChecker.Created);
        }

        [Fact]
        public void ProcessMessages_RegisterPlugin_CreatesPluginWithDefaultMessageBroker()
        {
            var assemblyLoadContext = CreateTestAssemblyLoadContext<MessageBrokerCreationTestPlugin>();
            var creationChecker = new PluginTypeCreationChecker();
            var serviceLookups = new Dictionary<Type, object>
            {
                { typeof(PluginTypeCreationChecker), creationChecker }
            };
            var serviceProvider = new TestServiceProvider(serviceLookups);
            object rawMessageBrokerData = null;
            var pluginHandler = new PluginHandler(
                serviceProvider, (data) => rawMessageBrokerData = data);
            var pluginMessage = new PluginMessage
            {
                Data = new JObject
                {
                    { "AssemblyName", "CustomAssembly" },
                    { "TypeName", typeof(MessageBrokerCreationTestPlugin).FullName },
                },
                MessageName = "RegisterPlugin",
                PluginId = RandomGuidId
            };

            pluginHandler.OnReceive(pluginMessage);
            pluginHandler.ProcessMessages(assemblyLoadContext);

            Assert.True(creationChecker.Created);
            Assert.NotNull(rawMessageBrokerData);
            var messageBrokerData = Assert.IsType<PluginMessageBroker.PluginMessageWrapperData>(rawMessageBrokerData);
            Assert.Equal(RandomGuidId, messageBrokerData.PluginId);
            var responseMessage = Assert.IsType<RegisterPluginResponseMessage>(messageBrokerData.Data);
            Assert.True(responseMessage.Success);
            Assert.Equal("RegisterPlugin", responseMessage.MessageName, StringComparer.Ordinal);
            Assert.Equal(1, responseMessage.Protocol);
            Assert.Null(responseMessage.Error);
        }

        [Fact]
        public void ProcessMessages_RegisterPlugin_CreatesPluginWithCustomMessageBroker()
        {
            var assemblyLoadContext = CreateTestAssemblyLoadContext<MessageBrokerCreationTestPlugin>();
            var creationChecker = new PluginTypeCreationChecker();
            object rawMessageBrokerData = null;
            var pluginMessageBroker = new PluginMessageBroker(RandomGuidId, (data) => rawMessageBrokerData = data);
            var serviceLookups = new Dictionary<Type, object>
            {
                { typeof(PluginTypeCreationChecker), creationChecker },
                { typeof(IPluginMessageBroker),  pluginMessageBroker }
            };
            var serviceProvider = new TestServiceProvider(serviceLookups);
            var pluginHandler = new PluginHandler(serviceProvider, (_) => { });
            var registerPluginMessage = new PluginMessage
            {
                Data = new JObject
                {
                    { "AssemblyName", "CustomAssembly" },
                    { "TypeName", typeof(MessageBrokerCreationTestPlugin).FullName },
                },
                MessageName = "RegisterPlugin",
                PluginId = RandomGuidId
            };
            var pluginMessage = new PluginMessage
            {
                Data = new JObject
                {
                    { "Data", "Hello Plugin" },
                },
                MessageName = "PluginMessage",
                PluginId = RandomGuidId
            };

            pluginHandler.OnReceive(registerPluginMessage);
            pluginHandler.OnReceive(pluginMessage);
            pluginHandler.ProcessMessages(assemblyLoadContext);

            Assert.True(creationChecker.Created);
            Assert.NotNull(rawMessageBrokerData);
            var messageBrokerData = Assert.IsType<PluginMessageBroker.PluginMessageWrapperData>(rawMessageBrokerData);
            Assert.Equal(RandomGuidId, messageBrokerData.PluginId);
            Assert.Equal("Created", messageBrokerData.Data.ToString(), StringComparer.Ordinal);
        }

        [Fact]
        public void ProcessMessages_RegisterPlugin_CreatesPluginWithAssemblyLoadContext()
        {
            var assemblyLoadContext = CreateTestAssemblyLoadContext<AssemblyLoadContextRelayTestPlugin>();
            var serviceLookups = new Dictionary<Type, object>();
            var serviceProvider = new TestServiceProvider(serviceLookups);
            object rawWrappedData = null;
            var pluginHandler = new PluginHandler(
                serviceProvider, (data) => rawWrappedData = data);
            var registerPluginMessage = new PluginMessage
            {
                Data = new JObject
                {
                    { "AssemblyName", "CustomAssembly" },
                    { "TypeName", typeof(AssemblyLoadContextRelayTestPlugin).FullName },
                },
                MessageName = "RegisterPlugin",
                PluginId = RandomGuidId
            };
            var pluginMessage = new PluginMessage
            {
                Data = new JObject
                {
                    { "Data", "Hello Plugin" },
                },
                MessageName = "PluginMessage",
                PluginId = RandomGuidId
            };

            pluginHandler.OnReceive(registerPluginMessage);
            pluginHandler.OnReceive(pluginMessage);
            pluginHandler.ProcessMessages(assemblyLoadContext);

            Assert.NotNull(rawWrappedData);
            var wrappedData = Assert.IsType<PluginMessageBroker.PluginMessageWrapperData>(rawWrappedData);
            Assert.Equal(RandomGuidId, wrappedData.PluginId);
            Assert.Same(assemblyLoadContext, wrappedData.Data);
        }

        [Theory]
        [InlineData(1, 1)]
        [InlineData(2, 2)]
        [InlineData(3, 3)]
        [InlineData(4, 3)]
        [InlineData(5, 3)]
        public void ProcessMessages_RegisterPlugin_SendsCorrectProtocol(int clientProtocol, int expectedProtocol)
        {
            var assemblyLoadContext = CreateTestAssemblyLoadContext<TestPlugin>();
            var serviceProvider = new TestServiceProvider();
            object rawWrappedData = null;
            var pluginHandler = new PluginHandler(serviceProvider, (data) => rawWrappedData = data);
            var pluginMessage = new PluginMessage
            {
                Data = new JObject
                {
                    { "AssemblyName", "CustomAssembly" },
                    { "TypeName", typeof(TestPlugin).FullName },
                    { "Protocol", clientProtocol }
                },
                MessageName = "RegisterPlugin",
                PluginId = RandomGuidId
            };

            pluginHandler.OnReceive(pluginMessage);
            pluginHandler.ProcessMessages(assemblyLoadContext);

            var messageBrokerData = Assert.IsType<PluginMessageBroker.PluginMessageWrapperData>(rawWrappedData);
            Assert.Equal(RandomGuidId, messageBrokerData.PluginId);
            var responseMessage = Assert.IsType<RegisterPluginResponseMessage>(messageBrokerData.Data);
            Assert.True(responseMessage.Success);
            Assert.Equal("RegisterPlugin", responseMessage.MessageName, StringComparer.Ordinal);
            Assert.Equal(expectedProtocol, responseMessage.Protocol);
        }

        [Fact]
        public void ProcessMessages_RegisterPlugin_SendsPluginsProtocolWhenClientProtocolNotProvided()
        {
            var assemblyLoadContext = CreateTestAssemblyLoadContext<TestPlugin>();
            var serviceProvider = new TestServiceProvider();
            object rawWrappedData = null;
            var pluginHandler = new PluginHandler(serviceProvider, (data) => rawWrappedData = data);
            var pluginMessage = new PluginMessage
            {
                Data = new JObject
                {
                    { "AssemblyName", "CustomAssembly" },
                    { "TypeName", typeof(TestPlugin).FullName },
                },
                MessageName = "RegisterPlugin",
                PluginId = RandomGuidId
            };

            pluginHandler.OnReceive(pluginMessage);
            pluginHandler.ProcessMessages(assemblyLoadContext);

            var messageBrokerData = Assert.IsType<PluginMessageBroker.PluginMessageWrapperData>(rawWrappedData);
            Assert.Equal(RandomGuidId, messageBrokerData.PluginId);
            var responseMessage = Assert.IsType<RegisterPluginResponseMessage>(messageBrokerData.Data);
            Assert.True(responseMessage.Success);
            Assert.Equal("RegisterPlugin", responseMessage.MessageName, StringComparer.Ordinal);
            Assert.Equal(3, responseMessage.Protocol);
        }

        [Fact]
        public void ProcessMessages_RegisterPlugin_SendsErrorWhenUnableToLocatePluginType()
        {
            var assemblyLoadContext = CreateTestAssemblyLoadContext<TestPlugin>();
            var serviceProvider = new TestServiceProvider();
            object rawWrappedData = null;
            var pluginHandler = new PluginHandler(serviceProvider, (data) => rawWrappedData = data);
            var pluginMessage = new PluginMessage
            {
                Data = new JObject
                {
                    { "AssemblyName", "CustomAssembly" },
                    { "TypeName", typeof(TestPlugin).FullName + "_" },
                },
                MessageName = "RegisterPlugin",
                PluginId = RandomGuidId
            };
            var expectedErrorMessage =
                $"Could not locate plugin id '{RandomGuidId}' of type '{typeof(TestPlugin).FullName + "_"}' " +
                "in assembly 'CustomAssembly'.";

            pluginHandler.OnReceive(pluginMessage);
            pluginHandler.ProcessMessages(assemblyLoadContext);

            var messageBrokerData = Assert.IsType<PluginMessageBroker.PluginMessageWrapperData>(rawWrappedData);
            Assert.Equal(RandomGuidId, messageBrokerData.PluginId);
            var responseMessage = Assert.IsType<RegisterPluginResponseMessage>(messageBrokerData.Data);
            Assert.False(responseMessage.Success);
            Assert.Equal("RegisterPlugin", responseMessage.MessageName, StringComparer.Ordinal);
            Assert.Equal(expectedErrorMessage, responseMessage.Error, StringComparer.Ordinal);
        }

        [Fact]
        public void ProcessMessages_RegisterPlugin_SendsErrorOnInvalidPluginTypes()
        {
            var assemblyLoadContext = CreateTestAssemblyLoadContext<InvalidTestPlugin>();
            var serviceProvider = new TestServiceProvider();
            object rawWrappedData = null;
            var pluginHandler = new PluginHandler(serviceProvider, (data) => rawWrappedData = data);
            var pluginMessage = new PluginMessage
            {
                Data = new JObject
                {
                    { "AssemblyName", "CustomAssembly" },
                    { "TypeName", typeof(InvalidTestPlugin).FullName },
                },
                MessageName = "RegisterPlugin",
                PluginId = RandomGuidId
            };
            var expectedErrorMessage =
                $"Cannot process plugin message. Plugin id '{RandomGuidId}' of type " +
                "'Microsoft.Dnx.DesignTimeHost.PluginHandlerFacts+InvalidTestPlugin' must be assignable " +
                "to type 'Microsoft.Dnx.DesignTimeHost.IPlugin'.";

            pluginHandler.OnReceive(pluginMessage);
            pluginHandler.ProcessMessages(assemblyLoadContext);

            var messageBrokerData = Assert.IsType<PluginMessageBroker.PluginMessageWrapperData>(rawWrappedData);
            Assert.Equal(RandomGuidId, messageBrokerData.PluginId);
            var responseMessage = Assert.IsType<RegisterPluginResponseMessage>(messageBrokerData.Data);
            Assert.False(responseMessage.Success);
            Assert.Equal("RegisterPlugin", responseMessage.MessageName, StringComparer.Ordinal);
            Assert.Equal(expectedErrorMessage, responseMessage.Error, StringComparer.Ordinal);
        }

        [Fact]
        public void ProcessMessages_UnregisterPlugin_UnregistersPlugin()
        {
            var assemblyLoadContext = CreateTestAssemblyLoadContext<TestPlugin>();
            var serviceProvider = new TestServiceProvider();
            object rawWrappedData = null;
            var pluginHandler = new PluginHandler(serviceProvider, (data) => rawWrappedData = data);
            var expectedErrorMessage =
                $"Message received for unregistered plugin id '{RandomGuidId}'. Plugins must first be registered " +
                "before they can receive messages.";
            var registerPluginMessage = new PluginMessage
            {
                Data = new JObject
                {
                    { "AssemblyName", "CustomAssembly" },
                    { "TypeName", typeof(TestPlugin).FullName },
                },
                MessageName = "RegisterPlugin",
                PluginId = RandomGuidId
            };
            var unregisterPluginMessage = new PluginMessage
            {
                MessageName = "UnregisterPlugin",
                PluginId = RandomGuidId
            };
            var pluginMessage = new PluginMessage
            {
                Data = new JObject
                {
                    { "Data", "Hello Plugin" },
                },
                MessageName = "PluginMessage",
                PluginId = RandomGuidId
            };

            pluginHandler.OnReceive(registerPluginMessage);
            pluginHandler.OnReceive(pluginMessage);
            pluginHandler.OnReceive(unregisterPluginMessage);
            pluginHandler.ProcessMessages(assemblyLoadContext);

            var messageBrokerData = Assert.IsType<PluginMessageBroker.PluginMessageWrapperData>(rawWrappedData);
            Assert.Equal(RandomGuidId, messageBrokerData.PluginId);
            var responseMessage = Assert.IsType<PluginResponseMessage>(messageBrokerData.Data);
            Assert.True(responseMessage.Success);
            Assert.Equal("UnregisterPlugin", responseMessage.MessageName, StringComparer.Ordinal);
            Assert.Null(responseMessage.Error);
        }

        [Fact]
        public void ProcessMessages_UnregisterPlugin_SendsErrorWhenUnregisteringUnknownPlugin()
        {
            var assemblyLoadContext = CreateTestAssemblyLoadContext<TestPlugin>();
            var serviceProvider = new TestServiceProvider();
            object rawWrappedData = null;
            var pluginHandler = new PluginHandler(serviceProvider, (data) => rawWrappedData = data);
            var unregisterPluginMessage = new PluginMessage
            {
                MessageName = "UnregisterPlugin",
                PluginId = RandomGuidId
            };
            var expectedErrorMessage =
                $"No plugin with id '{RandomGuidId}' has been registered. Cannot unregister plugin.";

            pluginHandler.OnReceive(unregisterPluginMessage);
            pluginHandler.ProcessMessages(assemblyLoadContext);

            var messageBrokerData = Assert.IsType<PluginMessageBroker.PluginMessageWrapperData>(rawWrappedData);
            Assert.Equal(RandomGuidId, messageBrokerData.PluginId);
            var responseMessage = Assert.IsType<PluginResponseMessage>(messageBrokerData.Data);
            Assert.False(responseMessage.Success);
            Assert.Equal("UnregisterPlugin", responseMessage.MessageName, StringComparer.Ordinal);
            Assert.Equal(expectedErrorMessage, responseMessage.Error, StringComparer.Ordinal);
        }

        [Fact]
        public void ProcessMessages_UnregisterPlugin_SendsErrorWhenUnregisteringPluginMoreThanOnce()
        {
            var assemblyLoadContext = CreateTestAssemblyLoadContext<TestPlugin>();
            var serviceProvider = new TestServiceProvider();
            object rawWrappedData = null;
            var pluginHandler = new PluginHandler(serviceProvider, (data) => rawWrappedData = data);
            var registerPluginMessage = new PluginMessage
            {
                Data = new JObject
                {
                    { "AssemblyName", "CustomAssembly" },
                    { "TypeName", typeof(TestPlugin).FullName },
                },
                MessageName = "RegisterPlugin",
                PluginId = RandomGuidId
            };
            var unregisterPluginMessage = new PluginMessage
            {
                MessageName = "UnregisterPlugin",
                PluginId = RandomGuidId
            };
            var expectedErrorMessage =
                $"No plugin with id '{RandomGuidId}' has been registered. Cannot unregister plugin.";

            pluginHandler.OnReceive(registerPluginMessage);
            pluginHandler.OnReceive(unregisterPluginMessage);
            pluginHandler.OnReceive(unregisterPluginMessage);
            pluginHandler.ProcessMessages(assemblyLoadContext);

            var messageBrokerData = Assert.IsType<PluginMessageBroker.PluginMessageWrapperData>(rawWrappedData);
            Assert.Equal(RandomGuidId, messageBrokerData.PluginId);
            var responseMessage = Assert.IsType<PluginResponseMessage>(messageBrokerData.Data);
            Assert.False(responseMessage.Success);
            Assert.Equal("UnregisterPlugin", responseMessage.MessageName, StringComparer.Ordinal);
            Assert.Equal(expectedErrorMessage, responseMessage.Error, StringComparer.Ordinal);
        }

        [Fact]
        public void ProcessMessages_PluginMessage_ProcessesMessages()
        {
            var assemblyLoadContext = CreateTestAssemblyLoadContext<MessageTestPlugin>();
            var serviceProvider = new TestServiceProvider();
            object rawMessageBrokerData = null;
            var pluginHandler = new PluginHandler(serviceProvider, (data) => rawMessageBrokerData = data);
            var registerPluginMessage = new PluginMessage
            {
                Data = new JObject
                {
                    { "AssemblyName", "CustomAssembly" },
                    { "TypeName", typeof(MessageTestPlugin).FullName },
                },
                MessageName = "RegisterPlugin",
                PluginId = RandomGuidId
            };
            var pluginMessage = new PluginMessage
            {
                Data = new JObject
                {
                    { "Data", "Hello Plugin" },
                },
                MessageName = "PluginMessage",
                PluginId = RandomGuidId
            };

            pluginHandler.OnReceive(registerPluginMessage);
            pluginHandler.OnReceive(pluginMessage);
            pluginHandler.ProcessMessages(assemblyLoadContext);

            Assert.NotNull(rawMessageBrokerData);
            var messageBrokerData = Assert.IsType<PluginMessageBroker.PluginMessageWrapperData>(rawMessageBrokerData);
            Assert.Equal(RandomGuidId, messageBrokerData.PluginId);
            var actualMessage = (string)messageBrokerData.Data;
            Assert.Equal("Hello Plugin!", actualMessage, StringComparer.Ordinal);
        }

        [Fact]
        public void ProcessMessages_PluginMessage_SendsErrorWhenPluginNotRegistered()
        {
            var assemblyLoadContext = CreateTestAssemblyLoadContext<TestPlugin>();
            var serviceProvider = new TestServiceProvider();
            object rawWrappedData = null;
            var pluginHandler = new PluginHandler(serviceProvider, (data) => rawWrappedData = data);
            var pluginMessage = new PluginMessage
            {
                Data = new JObject
                {
                    { "Data", "Hello Plugin" },
                },
                MessageName = "PluginMessage",
                PluginId = RandomGuidId
            };
            var expectedErrorMessage =
                $"Message received for unregistered plugin id '{RandomGuidId}'. Plugins must first be registered " +
                "before they can receive messages.";

            pluginHandler.OnReceive(pluginMessage);
            pluginHandler.ProcessMessages(assemblyLoadContext);

            var messageBrokerData = Assert.IsType<PluginMessageBroker.PluginMessageWrapperData>(rawWrappedData);
            Assert.Equal(RandomGuidId, messageBrokerData.PluginId);
            var responseMessage = Assert.IsType<PluginResponseMessage>(messageBrokerData.Data);
            Assert.False(responseMessage.Success);
            Assert.Equal("PluginMessage", responseMessage.MessageName, StringComparer.Ordinal);
            Assert.Equal(expectedErrorMessage, responseMessage.Error, StringComparer.Ordinal);
        }

        [Fact]
        public void ProcessMessages_PluginMessage_SendsErrorForPluginExceptions()
        {
            var assemblyLoadContext = CreateTestAssemblyLoadContext<CreationTestPlugin>();
            var creationChecker = new PluginTypeCreationChecker();
            var serviceLookups = new Dictionary<Type, object>
            {
                { typeof(PluginTypeCreationChecker), creationChecker }
            };
            var serviceProvider = new TestServiceProvider(serviceLookups);
            object rawWrappedData = null;
            var pluginHandler = new PluginHandler(serviceProvider, (data) => rawWrappedData = data);
            var registerPluginMessage = new PluginMessage
            {
                Data = new JObject
                {
                    { "AssemblyName", "CustomAssembly" },
                    { "TypeName", typeof(CreationTestPlugin).FullName },
                },
                MessageName = "RegisterPlugin",
                PluginId = RandomGuidId
            };
            var pluginMessage = new PluginMessage
            {
                Data = new JObject
                {
                    { "Data", "Hello Plugin" },
                },
                MessageName = "PluginMessage",
                PluginId = RandomGuidId
            };
            var expectedErrorMessage =
                $"Plugin '{RandomGuidId}' encountered an exception when processing a message. Exception message: 'Cannot process messages.'";

            pluginHandler.OnReceive(registerPluginMessage);
            pluginHandler.OnReceive(pluginMessage);
            pluginHandler.ProcessMessages(assemblyLoadContext);

            var messageBrokerData = Assert.IsType<PluginMessageBroker.PluginMessageWrapperData>(rawWrappedData);
            Assert.Equal(RandomGuidId, messageBrokerData.PluginId);
            var responseMessage = Assert.IsType<PluginResponseMessage>(messageBrokerData.Data);
            Assert.False(responseMessage.Success);
            Assert.Equal("PluginMessage", responseMessage.MessageName, StringComparer.Ordinal);
            Assert.Equal(expectedErrorMessage, responseMessage.Error, StringComparer.Ordinal);
        }

        [Fact]
        public void ProcessMessages_PluginMessage_SendsErrorWhenUnregistered()
        {
            var assemblyLoadContext = CreateTestAssemblyLoadContext<MessageTestPlugin>();
            var serviceProvider = new TestServiceProvider();
            object rawWrappedData = null;
            var pluginHandler = new PluginHandler(serviceProvider, (data) => rawWrappedData = data);
            var registerPluginMessage = new PluginMessage
            {
                Data = new JObject
                {
                    { "AssemblyName", "CustomAssembly" },
                    { "TypeName", typeof(MessageTestPlugin).FullName },
                },
                MessageName = "RegisterPlugin",
                PluginId = RandomGuidId
            };
            var unregisterPluginMessage = new PluginMessage
            {
                MessageName = "UnregisterPlugin",
                PluginId = RandomGuidId
            };
            var pluginMessage = new PluginMessage
            {
                Data = new JObject
                {
                    { "Data", "Hello Plugin" },
                },
                MessageName = "PluginMessage",
                PluginId = RandomGuidId
            };
            var expectedErrorMessage =
                $"Message received for unregistered plugin id '{RandomGuidId}'. Plugins must first be registered " +
                "before they can receive messages.";

            pluginHandler.OnReceive(registerPluginMessage);
            pluginHandler.OnReceive(unregisterPluginMessage);
            pluginHandler.OnReceive(pluginMessage);
            pluginHandler.ProcessMessages(assemblyLoadContext);

            var messageBrokerData = Assert.IsType<PluginMessageBroker.PluginMessageWrapperData>(rawWrappedData);
            Assert.Equal(RandomGuidId, messageBrokerData.PluginId);
            var responseMessage = Assert.IsType<PluginResponseMessage>(messageBrokerData.Data);
            Assert.False(responseMessage.Success);
            Assert.Equal("PluginMessage", responseMessage.MessageName, StringComparer.Ordinal);
            Assert.Equal(expectedErrorMessage, responseMessage.Error, StringComparer.Ordinal);
        }

        private static IAssemblyLoadContext CreateTestAssemblyLoadContext<TPlugin>()
        {
            var pluginType = typeof(TPlugin);
            var typeNameLookups = new Dictionary<string, Type>
            {
                { pluginType.FullName, pluginType }
            };
            var testAssembly = new TestAssembly(typeNameLookups);
            var assemblyLookups = new Dictionary<string, Assembly>
            {
                { "CustomAssembly", testAssembly }
            };

            return new TestAssemblyLoadContext(assemblyLookups);
        }

        private class MessageTestPlugin : IPlugin
        {
            private readonly IPluginMessageBroker _messageBroker;

            public MessageTestPlugin(IPluginMessageBroker messageBroker)
            {
                _messageBroker = messageBroker;
            }

            public int Protocol { get; set; } = 1;

            public bool ProcessMessage(JObject data, IAssemblyLoadContext assemblyLoadContext)
            {
                _messageBroker.SendMessage(data["Data"].ToString() + "!");

                return true;
            }
        }

        private class TestPlugin : IPlugin
        {
            public int Protocol { get; set; } = 3;

            public virtual bool ProcessMessage(JObject data, IAssemblyLoadContext assemblyLoadContext)
            {
                return true;
            }
        }

        private class InvalidTestPlugin
        {
        }

        private class CreationTestPlugin : IPlugin
        {
            public CreationTestPlugin(PluginTypeCreationChecker creationChecker)
            {
                creationChecker.Created = true;
            }

            public int Protocol { get; set; } = 1;

            public virtual bool ProcessMessage(JObject data, IAssemblyLoadContext assemblyLoadContext)
            {
                throw new InvalidOperationException("Cannot process messages.");
            }
        }

        private class MessageBrokerCreationTestPlugin : CreationTestPlugin
        {
            private readonly IPluginMessageBroker _messageBroker;

            public MessageBrokerCreationTestPlugin(
                IPluginMessageBroker messageBroker,
                PluginTypeCreationChecker creationChecker)
                : base(creationChecker)
            {
                _messageBroker = messageBroker;
            }

            public override bool ProcessMessage(JObject data, IAssemblyLoadContext assemblyLoadContext)
            {
                _messageBroker.SendMessage("Created");

                return true;
            }
        }

        private class AssemblyLoadContextRelayTestPlugin : TestPlugin
        {
            private readonly IPluginMessageBroker _messageBroker;

            public AssemblyLoadContextRelayTestPlugin(
                IPluginMessageBroker messageBroker)
            {
                _messageBroker = messageBroker;
            }

            public override bool ProcessMessage(JObject data, IAssemblyLoadContext assemblyLoadContext)
            {
                _messageBroker.SendMessage(assemblyLoadContext);

                return true;
            }
        }

        private class PluginTypeCreationChecker
        {
            public bool Created { get; set; }
        }

        private class FailureAssemblyLoadContext : TestAssemblyLoadContext
        {
            public bool _firstLoad;

            public FailureAssemblyLoadContext(IReadOnlyDictionary<string, Assembly> assemblyNameLookups)
                : base(assemblyNameLookups)
            {
            }

            public override Assembly Load(AssemblyName assemblyName)
            {
                if (!_firstLoad)
                {
                    _firstLoad = true;

                    throw new InvalidOperationException();
                }

                return base.Load(assemblyName);
            }
        }
    }
}