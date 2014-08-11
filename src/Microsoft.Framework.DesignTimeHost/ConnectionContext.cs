// Copyright (c) Microsoft Open Technologies, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Communication;
using Microsoft.Framework.DesignTimeHost.Models;
using Microsoft.Framework.Runtime;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.Versioning;
using Newtonsoft.Json.Linq;

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
            // Check the hostId to ensure it is from our host - throw it away if not
            //if (!message.HostId.Equals(_hostId, StringComparison.Ordinal))
            //{
            //    Trace.TraceInformation("[ConnectionContext]: Received message from unknown host {0}. Expected message from {1}. Ignoring", message.HostId, _hostId);
            //    return;
            //}

            if (message.MessageType == "EnumerateProjectContexts")
            {
                var projects = _contexts.Values.Select(p => new
                {
                    Id = p.Id,
                    ProjectPath = p.ApplicationPath
                });

                OnTransmit(new Message
                {
                    MessageType = "EnumerateProjectContexts",
                    Payload = JToken.FromObject(projects)
                });
            }
            else if (message.MessageType == "Compile")
            {
                var request = message.Payload.ToObject<CompileRequest>();

                var key = request.ProjectPath + request.TargetFramework + request.Configuration;

                var compiledResponse = _cache.Get<CompileResponse>(key, ctx =>
                {
                    var targetFramework = new FrameworkName(request.TargetFramework);

                    var applicationHostContext = new ApplicationHostContext(_services,
                                                                            request.ProjectPath,
                                                                            packagesDirectory: null,
                                                                            configuration: request.Configuration,
                                                                            targetFramework: targetFramework);

                    applicationHostContext.AddService(typeof(ICache), _cache);

                    var project = applicationHostContext.Project;
                    applicationHostContext.DependencyWalker.Walk(project.Name, project.Version, targetFramework);

                    var libraryManager = (ILibraryManager)applicationHostContext.ServiceProvider.GetService(typeof(ILibraryManager));

                    var export = libraryManager.GetLibraryExport(project.Name);
                    var projectReference = export.MetadataReferences.OfType<IMetadataProjectReference>()
                                                                    .First();

                    var embeddedReferences = export.MetadataReferences.OfType<IMetadataEmbeddedReference>().Select(r =>
                    {
                        return new
                        {
                            Name = r.Name,
                            Bytes = r.Contents
                        };
                    })
                    .ToDictionary(a => a.Name, a => a.Bytes);


                    var engine = new NonLoadingLoaderEngine();

                    try
                    {
                        projectReference.Load(engine);
                    }
                    catch (Exception ex)
                    {
                        Trace.TraceError(ex.ToString());
                    }

                    var diagnosticResult = projectReference.GetDiagnostics();

                    return new CompileResponse
                    {
                        Name = project.Name,
                        ProjectPath = project.ProjectDirectory,
                        Configuration = request.Configuration,
                        TargetFramework = request.TargetFramework,
                        Errors = diagnosticResult.Errors.ToList(),
                        Warnings = diagnosticResult.Warnings.ToList(),
                        EmbeddedReferences = embeddedReferences,
                        AssemblyBytes = engine.AssemblyBytes,
                        PdbBytes = engine.PdbBytes,
                        AssemblyPath = engine.AssemblyPath
                    };
                });

                _queue.WriteCustom(writer =>
                {
                    // TODO: Fix this
                    writer.Write(message.MessageType);
                    writer.Write(compiledResponse.Name);
                    writer.Write(compiledResponse.ProjectPath);
                    writer.Write(compiledResponse.Configuration);
                    writer.Write(compiledResponse.TargetFramework);
                    writer.Write(compiledResponse.Warnings.Count);
                    for (int i = 0; i < compiledResponse.Warnings.Count; i++)
                    {
                        writer.Write(compiledResponse.Warnings[i]);
                    }
                    writer.Write(compiledResponse.Errors.Count);
                    for (int i = 0; i < compiledResponse.Errors.Count; i++)
                    {
                        writer.Write(compiledResponse.Errors[i]);
                    }
                    writer.Write(compiledResponse.EmbeddedReferences.Count);
                    foreach (var pair in compiledResponse.EmbeddedReferences)
                    {
                        writer.Write(pair.Key);
                        writer.Write(pair.Value.Length);
                        writer.Write(pair.Value);
                    }
                    writer.Write(compiledResponse.AssemblyBytes.Length);
                    writer.Write(compiledResponse.AssemblyBytes);
                    writer.Write(compiledResponse.PdbBytes.Length);
                    writer.Write(compiledResponse.PdbBytes);
                });

                return;
            }


            ApplicationContext applicationContext;
            if (!_contexts.TryGetValue(message.ContextId, out applicationContext))
            {
                Trace.TraceInformation("[ConnectionContext]: Creating new application context for {0}", message.ContextId);

                applicationContext = new ApplicationContext(_services, message.ContextId, _cache);
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
