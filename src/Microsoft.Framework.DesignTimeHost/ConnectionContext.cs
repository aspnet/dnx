// Copyright (c) Microsoft Open Technologies, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Communication;
using Microsoft.Framework.DesignTimeHost.Models;
using Microsoft.Framework.Runtime;

namespace Microsoft.Framework.DesignTimeHost
{
    public class ConnectionContext
    {
        private readonly IDictionary<int, ApplicationContext> _contexts;
        private readonly ICache _cache;
        private readonly IServiceProvider _services;
        private readonly Stream _stream;
        private ProcessingQueue _queue;
        private string _hostId;

        public ConnectionContext(IDictionary<int, ApplicationContext> contexts,
                                 IServiceProvider services,
                                 ICache cache,
                                 Stream stream,
                                 string hostId)
        {
            _contexts = contexts;
            _services = services;
            _cache = cache;
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
            if (message.MessageType == "EnumerateProjectContexts")
            {
                var projects = _contexts.Values.Select(p => new
                {
                    Id = p.Id,
                    ProjectPath = p.ApplicationPath
                })
                .ToList();

                _queue.WriteCustom(writer =>
                {
                    writer.Write(message.MessageType);
                    writer.Write(projects.Count);
                    for (int i = 0; i < projects.Count; i++)
                    {
                        writer.Write(projects[i].ProjectPath);
                        writer.Write(projects[i].Id);
                    }
                });

                return;
            }

            ApplicationContext applicationContext;
            if (!_contexts.TryGetValue(message.ContextId, out applicationContext))
            {
                if (!message.HostId.Equals(_hostId, StringComparison.Ordinal))
                {
                    Trace.TraceInformation("[ConnectionContext]: Received message from unknown host {0}. Expected message from {1}. Ignoring", message.HostId, _hostId);
                    return;
                }

                Trace.TraceInformation("[ConnectionContext]: Creating new application context for {0}", message.ContextId);

                applicationContext = new ApplicationContext(_services, message.ContextId, _cache);
                applicationContext.OnTransmit += OnTransmit;
                _contexts.Add(message.ContextId, applicationContext);
            }

            if (message.MessageType == "GetCompiledAssembly")
            {
                _queue.WriteCustom(writer =>
                {
                    writer.Write("GetCompiledAssembly");
                    writer.Write(applicationContext.Id);
                    writer.Write(applicationContext.Local.Diagnostics.Warnings.Count);
                    foreach (var warning in applicationContext.Local.Diagnostics.Warnings)
                    {
                        writer.Write(warning);
                    }
                    writer.Write(applicationContext.Local.Diagnostics.Errors.Count);
                    foreach (var error in applicationContext.Local.Diagnostics.Errors)
                    {
                        writer.Write(error);
                    }
                    writer.Write(applicationContext.Local.CompiledBits.EmbeddedReferences.Count);
                    foreach (var pair in applicationContext.Local.CompiledBits.EmbeddedReferences)
                    {
                        writer.Write(pair.Key);
                        writer.Write(pair.Value.Length);
                        writer.Write(pair.Value);
                    }
                    writer.Write(applicationContext.Local.CompiledBits.AssemblyBytes.Length);
                    writer.Write(applicationContext.Local.CompiledBits.AssemblyBytes);
                    writer.Write(applicationContext.Local.CompiledBits.PdbBytes.Length);
                    writer.Write(applicationContext.Local.CompiledBits.PdbBytes);
                });
            }
            else
            {
                applicationContext.OnReceive(message);
            }
        }

        public void OnTransmit(Message message)
        {
            message.HostId = _hostId;
            _queue.Post(message);
        }
    }
}
