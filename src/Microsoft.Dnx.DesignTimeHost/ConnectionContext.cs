// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.Dnx.DesignTimeHost.Models;
using Microsoft.Dnx.Runtime;
using Microsoft.Dnx.Runtime.Caching;

namespace Microsoft.Dnx.DesignTimeHost
{
    public class ConnectionContext
    {
        private readonly IDictionary<int, ApplicationContext> _contexts;
        private readonly IServiceProvider _services;
        private readonly ICache _cache;
        private readonly ICacheContextAccessor _cacheContextAccessor;
        private readonly INamedCacheDependencyProvider _namedDependencyProvider;
        private readonly ProtocolManager _protocolManager;
        private ProcessingQueue _queue;
        private string _hostId;

        public ConnectionContext(IDictionary<int, ApplicationContext> contexts,
                                 IServiceProvider services,
                                 ICache cache,
                                 ICacheContextAccessor cacheContextAccessor,
                                 INamedCacheDependencyProvider namedDependencyProvider,
                                 ProcessingQueue queue,
                                 ProtocolManager protocolManager,
                                 string hostId)
        {
            _contexts = contexts;
            _services = services;
            _cache = cache;
            _cacheContextAccessor = cacheContextAccessor;
            _namedDependencyProvider = namedDependencyProvider;
            _queue = queue;
            _hostId = hostId;
            _protocolManager = protocolManager;
        }

        public bool Transmit(Message message)
        {
            message.HostId = _hostId;
            return _queue.Send(message);
        }

        // This is temporary until we change everything to the new message format
        public bool Transmit(Action<BinaryWriter> writer)
        {
            return _queue.Send(writer);
        }

        public void OnReceive(Message message)
        {
            // Mark the sender on the incoming message
            message.Sender = this;

            if (_protocolManager.IsProtocolNegotiation(message))
            {
                _protocolManager.Negotiate(message);
            }
            else
            {
                ApplicationContext applicationContext;
                if (!_contexts.TryGetValue(message.ContextId, out applicationContext))
                {
                    Logger.TraceInformation("[ConnectionContext]: Creating new application context for {0}", message.ContextId);

                    applicationContext = new ApplicationContext(_services,
                                                                _cache,
                                                                _cacheContextAccessor,
                                                                _namedDependencyProvider,
                                                                _protocolManager,
                                                                message.ContextId);

                    _contexts.Add(message.ContextId, applicationContext);
                }

                applicationContext.OnReceive(message);
            }
        }
    }
}