// Copyright (c) Microsoft Open Technologies, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.Versioning;
using System.Threading;
using Microsoft.Framework.DesignTimeHost.Models;
using Microsoft.Framework.DesignTimeHost.Models.IncomingMessages;
using Microsoft.Framework.DesignTimeHost.Models.OutgoingMessages;
using Microsoft.Framework.Runtime;
using Microsoft.Framework.Runtime.FileSystem;
using Microsoft.Framework.Runtime.Roslyn;
using Newtonsoft.Json.Linq;
using NuGet;

namespace Microsoft.Framework.DesignTimeHost
{
    public class ApplicationContext
    {
        private readonly IAssemblyLoaderEngine _loaderEngine;

        public class State
        {
            public string Path { get; set; }
            public FrameworkName TargetFramework { get; set; }

            public string Configuration { get; set; }

            public Project Project { get; set; }

            public RoslynMetadataProvider MetadataProvider { get; set; }
            public IDictionary<string, ReferenceDescription> Dependencies { get; set; }
            public FrameworkReferenceResolver FrameworkResolver { get; set; }
        }

        public class Trigger<TValue>
        {
            private TValue _value;

            public bool WasAssigned { get; private set; }

            public void ClearAssigned()
            {
                WasAssigned = false;
            }

            public TValue Value
            {
                get { return _value; }
                set
                {
                    WasAssigned = true;
                    _value = value;
                }
            }
        }

        public struct Nada
        {
        }

        private readonly Queue<Message> _inbox = new Queue<Message>();
        private readonly object _processingLock = new object();

        private readonly Trigger<string> _appPath = new Trigger<string>();
        private readonly Trigger<FrameworkName> _targetFramework = new Trigger<FrameworkName>();
        private readonly Trigger<string> _configuration = new Trigger<string>();
        private readonly Trigger<Nada> _filesChanged = new Trigger<Nada>();

        private readonly Trigger<State> _state = new Trigger<State>();

        private World _remote = new World();
        private World _local = new World();

        public ApplicationContext(IAssemblyLoaderEngine loaderEngine, int id)
        {
            _loaderEngine = loaderEngine;
            Id = id;
        }

        public int Id { get; private set; }

        public event Action<Message> OnTransmit;

        public void OnReceive(Message message)
        {
            lock (_inbox)
            {
                _inbox.Enqueue(message);
            }

            ThreadPool.QueueUserWorkItem(ProcessLoop);
        }

        public void ProcessLoop(object state)
        {
            if (!Monitor.TryEnter(_processingLock))
            {
                return;
            }

            try
            {
                lock (_inbox)
                {
                    if (_inbox.IsEmpty())
                    {
                        return;
                    }
                }

                DoProcessLoop();
            }
            catch (Exception ex)
            {
                // Unhandled errors
                var error = new ErrorMessage
                {
                    Message = ex.ToString()
                };

                OnTransmit(new Message
                {
                    ContextId = Id,
                    MessageType = "Error",
                    Payload = JToken.FromObject(error)
                });
            }
            finally
            {
                Monitor.Exit(_processingLock);
            }
        }

        public void DoProcessLoop()
        {
            DrainInbox();
            Calculate();
            Reconcile();
        }

        private void DrainInbox()
        {
            // Process all of the messages in the inbox
            while (ProcessMessage()) { }
        }

        private bool ProcessMessage()
        {
            Message message;

            lock (_inbox)
            {
                if (_inbox.IsEmpty())
                {
                    return false;
                }

                message = _inbox.Dequeue();

                // REVIEW: Can this ever happen?
                if (message == null)
                {
                    return false;
                }
            }

            Trace.TraceInformation("[ApplicationContext]: Received {0}", message.MessageType);

            switch (message.MessageType)
            {
                case "Initialize":
                    {
                        var data = message.Payload.ToObject<InitializeMessage>();
                        _appPath.Value = data.ProjectFolder;
                        _configuration.Value = data.Configuration ?? "debug";

                        SetTargetFramework(data.TargetFramework);
                    }
                    break;
                case "Teardown":
                    {
                        // TODO: Implement
                    }
                    break;
                case "ChangeTargetFramework":
                    {
                        var data = message.Payload.ToObject<ChangeTargetFrameworkMessage>();
                        SetTargetFramework(data.TargetFramework);
                    }
                    break;
                case "ChangeConfiguration":
                    {
                        var data = message.Payload.ToObject<ChangeConfigurationMessage>();
                        _configuration.Value = data.Configuration;
                    }
                    break;
                case "FilesChanged":
                    {
                        _filesChanged.Value = default(Nada);
                    }
                    break;
            }

            return true;
        }

        private void SetTargetFramework(string targetFrameworkValue)
        {
            var targetFramework = VersionUtility.ParseFrameworkName(targetFrameworkValue ?? "net45");
            _targetFramework.Value = targetFramework == VersionUtility.UnsupportedFrameworkName ? new FrameworkName(targetFrameworkValue) : targetFramework;
        }

        private void Calculate()
        {
            if (_appPath.WasAssigned ||
                _targetFramework.WasAssigned ||
                _configuration.WasAssigned ||
                _filesChanged.WasAssigned)
            {
                _appPath.ClearAssigned();
                _targetFramework.ClearAssigned();
                _configuration.ClearAssigned();
                _filesChanged.ClearAssigned();

                _state.Value = Initialize(_appPath.Value, _targetFramework.Value, _configuration.Value);
            }

            var state = _state.Value;
            if (_state.WasAssigned && state != null)
            {
                _state.ClearAssigned();

                var frameworks = state.Project.GetTargetFrameworks().Select(frameworkInfo => new FrameworkData
                {
                    CompilationSettings = state.Project.GetCompilationSettings(frameworkInfo.FrameworkName, state.Configuration),
                    FrameworkName = VersionUtility.GetShortFrameworkName(frameworkInfo.FrameworkName),
                    LongFrameworkName = frameworkInfo.FrameworkName.ToString(),
                    FriendlyFrameworkName = GetFriendlyFrameworkName(state.FrameworkResolver, frameworkInfo.FrameworkName)
                })
                .ToList();

                var configurations = state.Project.GetConfigurations().Select(config => new ConfigurationData
                {
                    Name = config,
                    CompilationSettings = state.Project.GetCompilationSettings(state.TargetFramework, config)
                })
                .ToList();

                _local.Configurations = new ProjectInformationMessage
                {
                    ProjectName = state.Project.Name,

                    // REVIEW: For backwards compatibility
                    Configurations = frameworks,

                    // All target framework information
                    Frameworks = frameworks,

                    // debug/release etc
                    ProjectConfigurations = configurations,

                    Commands = state.Project.Commands
                };

                var metadata = state.MetadataProvider.GetMetadata(state.Project.Name, state.TargetFramework, state.Configuration);

                _local.References = new ReferencesMessage
                {
                    RootDependency = state.Project.Name,
                    LongFrameworkName = state.TargetFramework.ToString(),
                    FriendlyFrameworkName = GetFriendlyFrameworkName(state.FrameworkResolver, state.TargetFramework),
                    ProjectReferences = metadata.ProjectReferences,
                    FileReferences = metadata.References,
                    RawReferences = metadata.RawReferences,
                    Dependencies = state.Dependencies
                };

                _local.Diagnostics = new DiagnosticsMessage
                {
                    Warnings = metadata.Warnings.ToList(),
                    Errors = metadata.Errors.ToList(),
                };

                _local.Sources = new SourcesMessage
                {
                    Files = metadata.SourceFiles
                };
            }
        }

        private string GetFriendlyFrameworkName(FrameworkReferenceResolver frameworkResolver, FrameworkName targetFramework)
        {
            // We don't have a friendly name for this anywhere on the machine so hard code it
            if (targetFramework.Identifier.Equals("K", StringComparison.OrdinalIgnoreCase))
            {
                // REVIEW: 4.5?
                return ".NET Core Framework 4.5";
            }

            return frameworkResolver.GetFriendlyFrameworkName(targetFramework) ?? targetFramework.ToString();
        }

        private void Reconcile()
        {
            if (IsDifferent(_local.Configurations, _remote.Configurations))
            {
                Trace.TraceInformation("[ApplicationContext]: OnTransmit(Configurations)");

                OnTransmit(new Message
                {
                    ContextId = Id,
                    MessageType = "Configurations",
                    Payload = JToken.FromObject(_local.Configurations)
                });

                _remote.Configurations = _local.Configurations;
            }

            if (IsDifferent(_local.References, _remote.References))
            {
                Trace.TraceInformation("[ApplicationContext]: OnTransmit(References)");

                OnTransmit(new Message
                {
                    ContextId = Id,
                    MessageType = "References",
                    Payload = JToken.FromObject(_local.References)
                });

                _remote.References = _local.References;
            }

            if (IsDifferent(_local.Diagnostics, _remote.Diagnostics))
            {
                Trace.TraceInformation("[ApplicationContext]: OnTransmit(Diagnostics)");

                OnTransmit(new Message
                {
                    ContextId = Id,
                    MessageType = "Diagnostics",
                    Payload = JToken.FromObject(_local.Diagnostics)
                });

                _remote.Diagnostics = _local.Diagnostics;
            }

            if (IsDifferent(_local.Sources, _remote.Sources))
            {
                Trace.TraceInformation("[ApplicationContext]: OnTransmit(Sources)");

                OnTransmit(new Message
                {
                    ContextId = Id,
                    MessageType = "Sources",
                    Payload = JToken.FromObject(_local.Sources)
                });

                _remote.Sources = _local.Sources;
            }
        }

        private bool IsDifferent(ProjectInformationMessage local, ProjectInformationMessage remote)
        {
            return true;
        }

        private bool IsDifferent(ReferencesMessage local, ReferencesMessage remote)
        {
            return true;
        }

        private bool IsDifferent(DiagnosticsMessage local, DiagnosticsMessage remote)
        {
            return true;
        }

        private bool IsDifferent(SourcesMessage local, SourcesMessage remote)
        {
            return true;
        }

        public State Initialize(string appPath, FrameworkName targetFramework, string configuration)
        {
            var state = new State
            {
                Path = appPath,
                TargetFramework = targetFramework,
                Configuration = configuration
            };

            Project project;
            if (!Project.TryGetProject(appPath, out project))
            {
                // package.json sad
                return state;
            }

            var projectDir = project.ProjectDirectory;
            var rootDirectory = ProjectResolver.ResolveRootDirectory(projectDir);
            var projectResolver = new ProjectResolver(projectDir, rootDirectory);

            var referenceAssemblyDependencyResolver = new ReferenceAssemblyDependencyResolver();
            var nugetDependencyResolver = new NuGetDependencyResolver(projectDir, referenceAssemblyDependencyResolver.FrameworkResolver);
            var gacDependencyResolver = new GacDependencyResolver();

            var compositeDependencyExporter = new CompositeLibraryExportProvider(new ILibraryExportProvider[] {
                referenceAssemblyDependencyResolver,
                gacDependencyResolver,
                nugetDependencyResolver,
            });

            var roslynCompiler = new RoslynCompiler(projectResolver,
                                                    NoopWatcher.Instance,
                                                    compositeDependencyExporter);

            state.MetadataProvider = new RoslynMetadataProvider(roslynCompiler);
            state.Project = project;
            state.FrameworkResolver = referenceAssemblyDependencyResolver.FrameworkResolver;

            var dependencyWalker = new DependencyWalker(new IDependencyProvider[] {
                new ProjectReferenceDependencyProvider(projectResolver),
                referenceAssemblyDependencyResolver,
                gacDependencyResolver,
                nugetDependencyResolver,
                new UnresolvedDependencyProvider() // A catch all for unresolved dependencies
            });

            dependencyWalker.Walk(state.Project.Name, state.Project.Version, targetFramework);

            Func<LibraryDescription, ReferenceDescription> referenceFactory = library =>
            {
                return new ReferenceDescription
                {
                    Name = library.Identity.Name,
                    Version = library.Identity.Version == null ? null : library.Identity.Version.ToString(),
                    Type = library.Type ?? "Unresolved",
                    Path = library.Path,
                    Dependencies = library.Dependencies.Select(lib => new ReferenceItem
                    {
                        Name = lib.Name,
                        Version = lib.Version == null ? null : lib.Version.ToString()
                    })
                };
            };

            state.Dependencies = dependencyWalker.Libraries
                                                 .Select(referenceFactory)
                                                 .ToDictionary(d => d.Name);

            return state;
        }
    }
}