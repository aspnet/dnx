// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using Microsoft.Dnx.DesignTimeHost.Models.IncomingMessages;
using Microsoft.Dnx.DesignTimeHost.Models.OutgoingMessages;
using Microsoft.Framework.Internal;
using Microsoft.Dnx.Runtime;
using Microsoft.Dnx.Runtime.Common.DependencyInjection;

namespace Microsoft.Dnx.DesignTimeHost
{
    public class PluginHandler
    {
        private const string RegisterPluginMessageName = "RegisterPlugin";
        private const string UnregisterPluginMessageName = "UnregisterPlugin";
        private const string PluginMessageMessageName = "PluginMessage";

        private readonly Action<object> _sendMessageMethod;
        private readonly IServiceProvider _hostServices;
        private readonly List<PluginMessage> _faultedRegisterPluginMessages;
        private readonly Queue<PluginMessage> _messageQueue;
        private readonly IDictionary<string, IPlugin> _registeredPlugins;
        private readonly IDictionary<string, Assembly> _assemblyCache;
        private readonly IDictionary<string, Type> _pluginTypeCache;

        public PluginHandler([NotNull] IServiceProvider hostServices, [NotNull] Action<object> sendMessageMethod)
        {
            _hostServices = hostServices;
            _sendMessageMethod = sendMessageMethod;
            _messageQueue = new Queue<PluginMessage>();
            _faultedRegisterPluginMessages = new List<PluginMessage>();
            _registeredPlugins = new Dictionary<string, IPlugin>(StringComparer.Ordinal);
            _assemblyCache = new Dictionary<string, Assembly>(StringComparer.Ordinal);
            _pluginTypeCache = new Dictionary<string, Type>(StringComparer.Ordinal);
        }

        public bool FaultedPluginRegistrations
        {
            get
            {
                return _faultedRegisterPluginMessages.Count > 0;
            }
        }

        public PluginHandlerOnReceiveResult OnReceive([NotNull] PluginMessage message)
        {
            _messageQueue.Enqueue(message);

            if (message.MessageName == RegisterPluginMessageName)
            {
                return PluginHandlerOnReceiveResult.RefreshDependencies;
            }

            return PluginHandlerOnReceiveResult.Default;
        }

        public void TryRegisterFaultedPlugins([NotNull] IAssemblyLoadContext assemblyLoadContext)
        {
            // Capture the messages here so we can clear the original list to potentially re-add messages if they
            // fail to recover.
            var faultedRegistrations = _faultedRegisterPluginMessages.ToArray();
            _faultedRegisterPluginMessages.Clear();

            foreach (var faultedRegisterPluginMessage in faultedRegistrations)
            {
                var response = RegisterPlugin(faultedRegisterPluginMessage, assemblyLoadContext);

                if (response.Success)
                {
                    SendMessage(faultedRegisterPluginMessage.PluginId, response);
                }
                else
                {
                    // We were unable to recover, re-add the faulted register plugin message.
                    _faultedRegisterPluginMessages.Add(faultedRegisterPluginMessage);
                }
            }
        }

        public void ProcessMessages([NotNull] IAssemblyLoadContext assemblyLoadContext)
        {
            while (_messageQueue.Count > 0)
            {
                var message = _messageQueue.Dequeue();

                switch (message.MessageName)
                {
                    case RegisterPluginMessageName:
                        RegisterMessage(message, assemblyLoadContext);
                        break;
                    case UnregisterPluginMessageName:
                        UnregisterMessage(message);
                        break;
                    case PluginMessageMessageName:
                        PluginMessage(message, assemblyLoadContext);
                        break;
                    default:
                        OnNoop(
                            message,
                            Resources.FormatPlugin_PluginHandlerCouldNotHandleMessage(
                                message.MessageName,
                                message.PluginId));
                        break;
                }
            }
        }

        private void RegisterMessage(PluginMessage message, IAssemblyLoadContext assemblyLoadContext)
        {
            var response = RegisterPlugin(message, assemblyLoadContext);

            if (!response.Success)
            {
                _faultedRegisterPluginMessages.Add(message);
            }

            SendMessage(message.PluginId, response);
        }

        private void UnregisterMessage(PluginMessage message)
        {
            if (!_registeredPlugins.Remove(message.PluginId))
            {
                var faultedRegistrationIndex = _faultedRegisterPluginMessages.FindIndex(
                    faultedMessage => faultedMessage.PluginId == message.PluginId);

                if (faultedRegistrationIndex == -1)
                {
                    OnError(
                        message,
                        errorMessage: Resources.FormatPlugin_UnregisteredPluginIdCannotUnregister(message.PluginId));

                    return;
                }
                else
                {
                    _faultedRegisterPluginMessages.RemoveAt(faultedRegistrationIndex);
                }
            }

            SendMessage(
                message.PluginId,
                new PluginResponseMessage
                {
                    MessageName = UnregisterPluginMessageName,
                });
        }

        private void PluginMessage(PluginMessage message, IAssemblyLoadContext assemblyLoadContext)
        {
            IPlugin plugin;
            if (_registeredPlugins.TryGetValue(message.PluginId, out plugin))
            {
                try
                {
                    if (!plugin.ProcessMessage(message.Data, assemblyLoadContext))
                    {
                        // Plugin didn't handle the message. Notify the client that we no-oped so it can respond
                        // accordingly.
                        OnNoop(
                            message,
                            Resources.FormatPlugin_PluginCouldNotHandleMessage(message.PluginId, message.MessageName));
                    }
                }
                catch (Exception exception)
                {
                    OnError(
                        message,
                        errorMessage: Resources.FormatPlugin_EncounteredExceptionWhenProcessingPluginMessage(
                            message.PluginId,
                            exception.Message));
                }
            }
            else
            {
                OnError(
                    message,
                    errorMessage: Resources.FormatPlugin_UnregisteredPluginIdCannotReceiveMessages(message.PluginId));
            }
        }

        private PluginResponseMessage RegisterPlugin(
            PluginMessage message,
            IAssemblyLoadContext assemblyLoadContext)
        {
            var registerData = message.Data.ToObject<PluginRegisterData>();
            var response = new RegisterPluginResponseMessage
            {
                MessageName = RegisterPluginMessageName
            };

            var pluginId = message.PluginId;
            var registerDataTypeCacheKey = registerData.GetFullTypeCacheKey();
            IPlugin plugin;
            Type pluginType;

            if (!_pluginTypeCache.TryGetValue(registerDataTypeCacheKey, out pluginType))
            {
                try
                {
                    Assembly assembly;
                    if (!_assemblyCache.TryGetValue(registerData.AssemblyName, out assembly))
                    {
                        assembly = assemblyLoadContext.Load(registerData.AssemblyName);
                    }

                    pluginType = assembly.GetType(registerData.TypeName);
                }
                catch (Exception exception)
                {
                    response.Error = exception.Message;

                    return response;
                }
            }

            if (pluginType == null)
            {
                response.Error = Resources.FormatPlugin_TypeCouldNotBeLocatedInAssembly(
                    pluginId,
                    registerData.TypeName,
                    registerData.AssemblyName);

                return response;
            }
            else
            {
                // We build out a custom plugin service provider to add a PluginMessageBroker and
                // IAssemblyLoadContext to the potential services that can be used to construct an IPlugin.
                var pluginServiceProvider = new PluginServiceProvider(
                    _hostServices,
                    messageBroker: new PluginMessageBroker(pluginId, _sendMessageMethod));

                plugin = ActivatorUtilities.CreateInstance(pluginServiceProvider, pluginType) as IPlugin;

                if (plugin == null)
                {
                    response.Error = Resources.FormatPlugin_CannotProcessMessageInvalidPluginType(
                        pluginId,
                        pluginType.FullName,
                        typeof(IPlugin).FullName);

                    return response;
                }
            }

            Debug.Assert(plugin != null);

            int resolvedProtocol;

            if (!registerData.Protocol.HasValue)
            {
                // Protocol wasn't provided, use the plugin's protocol.
                resolvedProtocol = plugin.Protocol;
            }
            else
            {
                // Client and plugin protocols are max values; meaning support is <= value. The goal in this method is
                // to return the maximum protocol supported by both parties (client and plugin).
                resolvedProtocol = Math.Min(registerData.Protocol.Value, plugin.Protocol);

                // Update the plugins protocol to be the resolved protocol.
                plugin.Protocol = resolvedProtocol;
            }

            _registeredPlugins[pluginId] = plugin;

            response.Protocol = resolvedProtocol;

            return response;
        }

        private void SendMessage(string pluginId, PluginResponseMessage message)
        {
            var messageBroker = new PluginMessageBroker(pluginId, _sendMessageMethod);

            messageBroker.SendMessage(message);
        }

        private void OnNoop(PluginMessage requestMessage, string errorMessage)
        {
            // We only want to send a no-op message if there's an associated message id on the request message.
            if (requestMessage.MessageId != null)
            {
                SendMessage(
                    requestMessage.PluginId,
                    message: new NoopPluginResponseMessage
                    {
                        MessageId = requestMessage.MessageId,
                        MessageName = requestMessage.MessageName,
                        Error = errorMessage
                    });
            }
        }

        private void OnError(PluginMessage requestMessage, string errorMessage)
        {
            SendMessage(
                requestMessage.PluginId,
                message: new PluginResponseMessage
                {
                    MessageId = requestMessage.MessageId,
                    MessageName = requestMessage.MessageName,
                    Error = errorMessage,
                });
        }

        private class PluginServiceProvider : IServiceProvider
        {
            private static readonly TypeInfo MessageBrokerTypeInfo = typeof(IPluginMessageBroker).GetTypeInfo();
            private readonly IServiceProvider _hostServices;
            private readonly PluginMessageBroker _messageBroker;

            public PluginServiceProvider(
                IServiceProvider hostServices,
                PluginMessageBroker messageBroker)
            {
                _hostServices = hostServices;
                _messageBroker = messageBroker;
            }

            public object GetService(Type serviceType)
            {
                var hostProvidedService = _hostServices.GetService(serviceType);

                if (hostProvidedService == null)
                {
                    var serviceTypeInfo = serviceType.GetTypeInfo();

                    if (MessageBrokerTypeInfo.IsAssignableFrom(serviceTypeInfo))
                    {
                        return _messageBroker;
                    }
                }

                return hostProvidedService;
            }
        }

        private class PluginRegisterData
        {
            public string AssemblyName { get; set; }
            public string TypeName { get; set; }
            public int? Protocol { get; set; }

            public string GetFullTypeCacheKey()
            {
                return $"{TypeName}, {AssemblyName}";
            }
        }
    }
}