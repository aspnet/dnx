// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.Dnx.Compilation;
using Microsoft.Dnx.DesignTimeHost.Models;
using Microsoft.Dnx.Runtime;
using Microsoft.Extensions.CompilationAbstractions;
using Microsoft.Extensions.PlatformAbstractions;

namespace Microsoft.Dnx.DesignTimeHost
{
    public class ConnectionContext
    {
        private readonly IDictionary<int, ApplicationContext> _contexts;
        private readonly IServiceProvider _services;
        private readonly ProtocolManager _protocolManager;
        private readonly CompilationEngine _compilationEngine;
        private ProcessingQueue _queue;
        private string _hostId;
        private readonly IApplicationEnvironment _applicationEnvironment;
        private readonly IRuntimeEnvironment _runtimeEnvironment;
        private readonly IAssemblyLoadContextAccessor _loadContextAccessor;
        private FrameworkReferenceResolver _frameworkResolver;

        public ConnectionContext(IDictionary<int, ApplicationContext> contexts,
                                 IServiceProvider services,
                                 IApplicationEnvironment applicationEnvironment,
                                 IRuntimeEnvironment runtimeEnvironment,
                                 IAssemblyLoadContextAccessor loadContextAccessor,
                                 FrameworkReferenceResolver frameworkResolver,
                                 ProcessingQueue queue,
                                 ProtocolManager protocolManager,
                                 CompilationEngine compilationEngine,
                                 string hostId)
        {
            _contexts = contexts;
            _services = services;
            _applicationEnvironment = applicationEnvironment;
            _runtimeEnvironment = runtimeEnvironment;
            _loadContextAccessor = loadContextAccessor;
            _frameworkResolver = frameworkResolver;
            _queue = queue;
            _compilationEngine = compilationEngine;
            _protocolManager = protocolManager;
            _compilationEngine = compilationEngine;
            _hostId = hostId;
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
                                                                _applicationEnvironment,
                                                                _runtimeEnvironment,
                                                                _loadContextAccessor,
                                                                _protocolManager,
                                                                _compilationEngine,
                                                                _frameworkResolver,
                                                                message.ContextId);

                    _contexts.Add(message.ContextId, applicationContext);
                }

                applicationContext.OnReceive(message);
            }
        }
    }
}