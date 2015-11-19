// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;

namespace Microsoft.Dnx.DesignTimeHost
{
    public class PluginMessageBroker : IPluginMessageBroker
    {
        private readonly Action<object> _sendMessageMethod;
        private readonly string _pluginId;

        public PluginMessageBroker(string pluginId, Action<object> sendMessageMethod)
        {
            _pluginId = pluginId;
            _sendMessageMethod = sendMessageMethod;
        }

        public void SendMessage(object data)
        {
            var wrapper = new PluginMessageWrapperData
            {
                PluginId = _pluginId,
                Data = data
            };

            _sendMessageMethod(wrapper);
        }

        // Internal for testing
        internal class PluginMessageWrapperData
        {
            public string PluginId { get; set; }
            public object Data { get; set; }
        }
    }
}