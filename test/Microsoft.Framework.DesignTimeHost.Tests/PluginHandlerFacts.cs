// Copyright (c) Microsoft Open Technologies, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Reflection;
using Microsoft.Framework.DesignTimeHost.Models.IncomingMessages;
using Microsoft.Framework.Runtime;
using Newtonsoft.Json.Linq;
using Xunit;

namespace Microsoft.Framework.DesignTimeHost
{
    public class PluginHandlerFacts
    {
        private const string RandomGuidId = "d81b8ad8-306d-474b-b8a9-b25c7f80be7e";

        [Fact]
        public void ProcessMessage_RegisterPlugin_CreatesPlugin()
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

            pluginHandler.ProcessMessage(pluginMessage, assemblyLoadContext);

            Assert.True(creationChecker.Created);
        }

        [Fact]
        public void ProcessMessage_RegisterPlugin_DoesNotCacheCreatedPlugin()
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

            pluginHandler.ProcessMessage(pluginMessage, assemblyLoadContext);
            Assert.True(creationChecker.Created);
            creationChecker.Created = false;
            pluginHandler.ProcessMessage(pluginMessage, assemblyLoadContext);
            Assert.True(creationChecker.Created);
        }

        [Fact]
        public void ProcessMessage_RegisterPlugin_CreatesPluginWithDefaultMessageBroker()
        {
            var assemblyLoadContext = CreateTestAssemblyLoadContext<MessageBrokerCreationTestPlugin>();
            var creationChecker = new PluginTypeCreationChecker();
            var serviceLookups = new Dictionary<Type, object>
            {
                { typeof(PluginTypeCreationChecker), creationChecker }
            };
            var serviceProvider = new TestServiceProvider(serviceLookups);
            PluginMessageBroker.PluginMessageWrapperData messageBrokerData = null;
            var pluginHandler = new PluginHandler(
                serviceProvider, (data) => messageBrokerData = (PluginMessageBroker.PluginMessageWrapperData)data);
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

            pluginHandler.ProcessMessage(pluginMessage, assemblyLoadContext);

            Assert.True(creationChecker.Created);
            Assert.NotNull(messageBrokerData);
            Assert.Equal(RandomGuidId, messageBrokerData.PluginId);
            Assert.Equal("Created", messageBrokerData.Data.ToString(), StringComparer.Ordinal);
        }

        [Fact]
        public void ProcessMessage_RegisterPlugin_CreatesPluginWithCustomMessageBroker()
        {
            var assemblyLoadContext = CreateTestAssemblyLoadContext<MessageBrokerCreationTestPlugin>();
            var creationChecker = new PluginTypeCreationChecker();
            PluginMessageBroker.PluginMessageWrapperData messageBrokerData = null;
            var pluginMessageBroker = new PluginMessageBroker(
                RandomGuidId, (data) => messageBrokerData = (PluginMessageBroker.PluginMessageWrapperData)data);
            var serviceLookups = new Dictionary<Type, object>
            {
                { typeof(PluginTypeCreationChecker), creationChecker },
                { typeof(IPluginMessageBroker),  pluginMessageBroker }
            };
            var serviceProvider = new TestServiceProvider(serviceLookups);
            var pluginHandler = new PluginHandler(serviceProvider, (_) => { });
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

            pluginHandler.ProcessMessage(pluginMessage, assemblyLoadContext);

            Assert.True(creationChecker.Created);
            Assert.NotNull(messageBrokerData);
            Assert.Equal(RandomGuidId, messageBrokerData.PluginId);
            Assert.Equal("Created", messageBrokerData.Data.ToString(), StringComparer.Ordinal);
        }

        [Fact]
        public void ProcessMessage_RegisterPlugin_CreatesPluginWithAssemblyLoadContext()
        {
            var assemblyLoadContext = CreateTestAssemblyLoadContext<AssemblyLoadContextRelayTestPlugin>();
            var serviceLookups = new Dictionary<Type, object>();
            var serviceProvider = new TestServiceProvider(serviceLookups);
            PluginMessageBroker.PluginMessageWrapperData wrappedData = null;
            var pluginHandler = new PluginHandler(
                serviceProvider, (data) => wrappedData = (PluginMessageBroker.PluginMessageWrapperData)data);
            var pluginMessage = new PluginMessage
            {
                Data = new JObject
                {
                    { "AssemblyName", "CustomAssembly" },
                    { "TypeName", typeof(AssemblyLoadContextRelayTestPlugin).FullName },
                },
                MessageName = "RegisterPlugin",
                PluginId = RandomGuidId
            };

            pluginHandler.ProcessMessage(pluginMessage, assemblyLoadContext);

            Assert.NotNull(wrappedData);
            Assert.Equal(RandomGuidId, wrappedData.PluginId);
            Assert.Same(assemblyLoadContext, wrappedData.Data);
        }

        [Fact]
        public void ProcessMessage_RegisterPlugin_DoesNotThrowOnInvalidPluginTypeNames()
        {
            var pluginType = typeof(CreationTestPlugin);
            var testAssembly = new TestAssembly();
            var assemblyLookups = new Dictionary<string, Assembly>
            {
                { "CustomAssembly", testAssembly }
            };
            var assemblyLoadContext = new TestAssemblyLoadContext(assemblyLookups);
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
                    { "TypeName", pluginType.FullName },
                },
                MessageName = "RegisterPlugin",
                PluginId = RandomGuidId
            };

            pluginHandler.ProcessMessage(pluginMessage, assemblyLoadContext);

            Assert.False(creationChecker.Created);
        }

        [Fact]
        public void ProcessMessage_RegisterPlugin_ThrowsOnInvalidPluginTypes()
        {
            var assemblyLoadContext = CreateTestAssemblyLoadContext<InvalidTestPlugin>();
            var serviceProvider = new TestServiceProvider();
            var pluginHandler = new PluginHandler(serviceProvider, (_) => { });
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
                "'Microsoft.Framework.DesignTimeHost.PluginHandlerFacts+InvalidTestPlugin' must be assignable " +
                "to type 'Microsoft.Framework.DesignTimeHost.IPlugin'.";

            var error = Assert.Throws<InvalidOperationException>(
                () => pluginHandler.ProcessMessage(pluginMessage, assemblyLoadContext));
            Assert.Equal(expectedErrorMessage, error.Message, StringComparer.Ordinal);
        }

        [Fact]
        public void ProcessMessage_UnregisterPlugin_UnregistersPlugin()
        {
            var assemblyLoadContext = CreateTestAssemblyLoadContext<TestPlugin>();
            var serviceProvider = new TestServiceProvider();
            var pluginHandler = new PluginHandler(serviceProvider, (_) => { });
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

            pluginHandler.ProcessMessage(registerPluginMessage, assemblyLoadContext);
            pluginHandler.ProcessMessage(unregisterPluginMessage, assemblyLoadContext);
        }

        [Fact]
        public void ProcessMessage_UnregisterPlugin_ThrowsWhenUnregisteringUnknownPlugin()
        {
            var assemblyLoadContext = CreateTestAssemblyLoadContext<TestPlugin>();
            var serviceProvider = new TestServiceProvider();
            var pluginHandler = new PluginHandler(serviceProvider, (_) => { });
            var unregisterPluginMessage = new PluginMessage
            {
                MessageName = "UnregisterPlugin",
                PluginId = RandomGuidId
            };
            var expectedErrorMessage =
                $"No plugin with id '{RandomGuidId}' has been registered. Cannot unregister plugin.";

            var error = Assert.Throws<InvalidOperationException>(
                () => pluginHandler.ProcessMessage(unregisterPluginMessage, assemblyLoadContext));
            Assert.Equal(expectedErrorMessage, error.Message, StringComparer.Ordinal);
        }

        [Fact]
        public void ProcessMessage_UnregisterPlugin_ThrowsWhenUnregisteringPluginMoreThanOnce()
        {
            var assemblyLoadContext = CreateTestAssemblyLoadContext<TestPlugin>();
            var serviceProvider = new TestServiceProvider();
            var pluginHandler = new PluginHandler(serviceProvider, (_) => { });
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

            pluginHandler.ProcessMessage(registerPluginMessage, assemblyLoadContext);
            pluginHandler.ProcessMessage(unregisterPluginMessage, assemblyLoadContext);

            var error = Assert.Throws<InvalidOperationException>(
                () => pluginHandler.ProcessMessage(unregisterPluginMessage, assemblyLoadContext));
            Assert.Equal(expectedErrorMessage, error.Message, StringComparer.Ordinal);
            error = Assert.Throws<InvalidOperationException>(
                () => pluginHandler.ProcessMessage(unregisterPluginMessage, assemblyLoadContext));
            Assert.Equal(expectedErrorMessage, error.Message, StringComparer.Ordinal);
        }

        [Fact]
        public void ProcessMessage_PluginMessage_ProcessesMessages()
        {
            var assemblyLoadContext = CreateTestAssemblyLoadContext<MessageTestPlugin>();
            var serviceProvider = new TestServiceProvider();
            PluginMessageBroker.PluginMessageWrapperData messageBrokerData = null;
            var pluginHandler = new PluginHandler(
                serviceProvider, (data) => messageBrokerData = (PluginMessageBroker.PluginMessageWrapperData)data);
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

            pluginHandler.ProcessMessage(registerPluginMessage, assemblyLoadContext);
            pluginHandler.ProcessMessage(pluginMessage, assemblyLoadContext);

            Assert.NotNull(messageBrokerData);
            Assert.Equal(RandomGuidId, messageBrokerData.PluginId);
            var actualMessage = (string)messageBrokerData.Data;
            Assert.Equal("Hello Plugin!", actualMessage, StringComparer.Ordinal);
        }

        [Fact]
        public void ProcessMessage_PluginMessage_ThrowsWhenPluginNotRegistered()
        {
            var assemblyLoadContext = CreateTestAssemblyLoadContext<MessageTestPlugin>();
            var serviceProvider = new TestServiceProvider();
            var pluginHandler = new PluginHandler(serviceProvider, (_) => { });
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

            var error = Assert.Throws<InvalidOperationException>(
                () => pluginHandler.ProcessMessage(pluginMessage, assemblyLoadContext));
            Assert.Equal(expectedErrorMessage, error.Message, StringComparer.Ordinal);
        }

        [Fact]
        public void ProcessMessage_PluginMessage_ThrowsWhenUnregistered()
        {
            var assemblyLoadContext = CreateTestAssemblyLoadContext<MessageTestPlugin>();
            var serviceProvider = new TestServiceProvider();
            var pluginHandler = new PluginHandler(serviceProvider, (_) => { });
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

            pluginHandler.ProcessMessage(registerPluginMessage, assemblyLoadContext);
            pluginHandler.ProcessMessage(unregisterPluginMessage, assemblyLoadContext);

            var error = Assert.Throws<InvalidOperationException>(
                () => pluginHandler.ProcessMessage(pluginMessage, assemblyLoadContext));
            Assert.Equal(expectedErrorMessage, error.Message, StringComparer.Ordinal);
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

            public void ProcessMessage(JObject data)
            {
                _messageBroker.SendMessage(data["Data"].ToString() + "!");
            }
        }

        private class TestPlugin : IPlugin
        {
            public void ProcessMessage(JObject data)
            {
                throw new NotImplementedException();
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

            public void ProcessMessage(JObject data)
            {
                throw new NotImplementedException();
            }
        }

        private class MessageBrokerCreationTestPlugin : CreationTestPlugin
        {
            public MessageBrokerCreationTestPlugin(
                IPluginMessageBroker messageBroker,
                PluginTypeCreationChecker creationChecker)
                : base(creationChecker)
            {
                messageBroker.SendMessage("Created");
            }
        }

        private class AssemblyLoadContextRelayTestPlugin : TestPlugin
        {
            public AssemblyLoadContextRelayTestPlugin(
                IPluginMessageBroker messageBroker, 
                IAssemblyLoadContext assemblyLoadContext)
            {
                messageBroker.SendMessage(assemblyLoadContext);
            }
        }

        private class PluginTypeCreationChecker
        {
            public bool Created { get; set; }
        }
    }
}