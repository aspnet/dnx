// Copyright (c) Microsoft Open Technologies, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Reflection;
using Microsoft.Framework.DesignTimeHost.Models.IncomingMessages;
using Microsoft.Framework.Runtime;
using Microsoft.Framework.Runtime.Common.DependencyInjection;

namespace Microsoft.Framework.DesignTimeHost
{
    public class PluginHandler
    {
        private readonly Action<object> _sendMessageMethod;
        private readonly IServiceProvider _hostServices;
        private readonly IDictionary<string, IPlugin> _plugins;

        public PluginHandler(IServiceProvider hostServices, Action<object> sendMessageMethod)
        {
            _sendMessageMethod = sendMessageMethod;
            _hostServices = hostServices;
            _plugins = new Dictionary<string, IPlugin>(StringComparer.Ordinal);
        }

        public void ProcessMessage(PluginMessage data, IAssemblyLoadContext assemblyLoadContext)
        {
            switch (data.MessageName)
            {
                case "RegisterPlugin":
                    ProcessRegisterMessage(data, assemblyLoadContext);
                    break;
                case "UnregisterPlugin":
                    ProcessUnregisterMessage(data);
                    break;
                case "PluginMessage":
                    ProcessPluginMessage(data);
                    break;
            }
        }

        private void ProcessPluginMessage(PluginMessage data)
        {
            IPlugin plugin;
            if (_plugins.TryGetValue(data.PluginId, out plugin))
            {
                plugin.ProcessMessage(data.Data);
            }
            else
            {
                throw new InvalidOperationException(
                    Resources.FormatPlugin_UnregisteredPluginIdCannotReceiveMessages(data.PluginId));
            }
        }

        private void ProcessUnregisterMessage(PluginMessage data)
        {
            if (_plugins.ContainsKey(data.PluginId))
            {
                _plugins.Remove(data.PluginId);
            }
            else
            {
                throw new InvalidOperationException(
                    Resources.FormatPlugin_UnregisteredPluginIdCannotUnregister(data.PluginId));
            }
        }

        private void ProcessRegisterMessage(PluginMessage data, IAssemblyLoadContext assemblyLoadContext)
        {
            var pluginId = data.PluginId;
            var registerData = data.Data.ToObject<PluginRegisterData>();
            var assembly = assemblyLoadContext.Load(registerData.AssemblyName);
            var pluginType = assembly.GetType(registerData.TypeName);

            if (pluginType != null)
            {
                // We build out a custom plugin service provider to add a PluginMessageBroker to the potential
                // services that can be used to construct an IPlugin.
                var pluginServiceProvider = new PluginServiceProvider(
                    _hostServices,
                    assemblyLoadContext,
                    messageBroker: new Lazy<PluginMessageBroker>(
                        () => new PluginMessageBroker(pluginId, _sendMessageMethod)));

                var plugin = ActivatorUtilities.CreateInstance(pluginServiceProvider, pluginType) as IPlugin;

                if (plugin == null)
                {
                    throw new InvalidOperationException(
                        Resources.FormatPlugin_CannotProcessMessageInvalidPluginType(
                            pluginId, pluginType.FullName, typeof(IPlugin).FullName));
                }

                _plugins[pluginId] = plugin;
            }
        }

        private class PluginServiceProvider : IServiceProvider
        {
            private static readonly TypeInfo MessageBrokerTypeInfo = typeof(IPluginMessageBroker).GetTypeInfo();
            private static readonly TypeInfo AssemblyLoadContextTypeInfo = typeof(IAssemblyLoadContext).GetTypeInfo();
            private readonly IServiceProvider _hostServices;
            private readonly IAssemblyLoadContext _assemblyLoadContext;
            private readonly Lazy<PluginMessageBroker> _messageBroker;

            public PluginServiceProvider(
                IServiceProvider hostServices, 
                IAssemblyLoadContext assemblyLoadContext,
                Lazy<PluginMessageBroker> messageBroker)
            {
                _hostServices = hostServices;
                _assemblyLoadContext = assemblyLoadContext;
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
                        return _messageBroker.Value;
                    }
                    else if (AssemblyLoadContextTypeInfo.IsAssignableFrom(serviceTypeInfo))
                    {
                        return _assemblyLoadContext;
                    }
                }

                return hostProvidedService;
            }
        }

        private class PluginRegisterData
        {
            public string AssemblyName { get; set; }
            public string TypeName { get; set; }
        }
    }
}