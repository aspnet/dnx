// Copyright (c) Microsoft Open Technologies, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using Communication;
using Microsoft.Framework.DesignTimeHost.Models;
using Microsoft.Framework.Runtime;

namespace Microsoft.Framework.DesignTimeHost
{
    public class ConnectionContext
    {
        private readonly IDictionary<int, ApplicationContext> _contexts = new Dictionary<int, ApplicationContext>();
        private readonly Cache _cache = new Cache();
        private readonly IAssemblyLoaderEngine _loaderEngine;
        private readonly IServiceProvider _services;
        private readonly Stream _stream;
        private ProcessingQueue _queue;
        private string _hostId;

        public ConnectionContext(IServiceProvider services, IAssemblyLoaderEngine loaderEngine, Stream stream, string hostId)
        {
            _services = services;
            _loaderEngine = loaderEngine;
            _stream = stream;
            _hostId = hostId;
        }

        public void Start()
        {
            _queue = new ProcessingQueue(_stream);
            _queue.OnReceive += OnReceive;
            _queue.Start();
        }

        public void OnReceive(Message message)
        {
            // Check the hostId to ensure it is from our host - throw it away if not
            if (!message.HostId.Equals(_hostId, StringComparison.Ordinal))
            {
                Trace.TraceInformation("[ConnectionContext]: Received message from unknown host {0}. Expected message from {1}. Ignoring", message.HostId, _hostId);
                return;
            }

            ApplicationContext applicationContext;
            if (!_contexts.TryGetValue(message.ContextId, out applicationContext))
            {
                Trace.TraceInformation("[ConnectionContext]: Creating new application context for {0}", message.ContextId);

                applicationContext = new ApplicationContext(_services, _loaderEngine, message.ContextId, _cache);
                applicationContext.OnTransmit += OnTransmit;
                _contexts.Add(message.ContextId, applicationContext);
            }

            applicationContext.OnReceive(message);
        }

        public void OnTransmit(Message message)
        {
            message.HostId = _hostId;
            _queue.Post(message);
        }
    }
}
