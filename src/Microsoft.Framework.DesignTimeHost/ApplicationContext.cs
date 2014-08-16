// Copyright (c) Microsoft Open Technologies, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.Versioning;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Framework.DesignTimeHost.Models;
using Microsoft.Framework.DesignTimeHost.Models.IncomingMessages;
using Microsoft.Framework.DesignTimeHost.Models.OutgoingMessages;
using Microsoft.Framework.Runtime;
using Microsoft.Framework.Runtime.Common.DependencyInjection;
using Microsoft.Framework.Runtime.Roslyn;
using Microsoft.Framework.TestAdapter;
using Newtonsoft.Json.Linq;
using NuGet;

namespace Microsoft.Framework.DesignTimeHost
{
    public class ApplicationContext
    {
        private readonly IServiceProvider _hostServices;
        private readonly ICache _cache;
        private readonly Queue<Message> _inbox = new Queue<Message>();
        private readonly object _processingLock = new object();

        private readonly Trigger<string> _appPath = new Trigger<string>();
        private readonly Trigger<FrameworkName> _targetFramework = new Trigger<FrameworkName>();
        private readonly Trigger<string> _configuration = new Trigger<string>();
        private readonly Trigger<Nada> _filesChanged = new Trigger<Nada>();

        private readonly Trigger<State> _state = new Trigger<State>();

        private World _remote = new World();
        private World _local = new World();

        private ConnectionContext _initializedContext;
        private readonly List<CompiledAssemblyState> _waitingForCompiledAssemblies = new List<CompiledAssemblyState>();
        private readonly List<ConnectionContext> _waitingForDiagnostics = new List<ConnectionContext>();

        public ApplicationContext(IServiceProvider services, ICache cache, int id)
        {
            _hostServices = services;
            _cache = cache;
            Id = id;
        }

        public int Id { get; private set; }

        public string ApplicationPath { get { return _appPath.Value; } }

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
                Trace.TraceError("[ApplicationContext]: Error occured: {0}", ex);

                // Unhandled errors
                var error = new ErrorMessage
                {
                    Message = ex.ToString()
                };

                _initializedContext.Transmit(new Message
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
                        // This should only be sent once
                        if (_initializedContext == null)
                        {
                            _initializedContext = message.Sender;

                            var data = message.Payload.ToObject<InitializeMessage>();
                            _appPath.Value = data.ProjectFolder;
                            _configuration.Value = data.Configuration ?? "Debug";

                            SetTargetFramework(data.TargetFramework);
                        }
                        else
                        {
                            Trace.TraceInformation("[ApplicationContext]: Received Initialize message more than once for {0}", _appPath.Value);
                        }
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
                case "GetCompiledAssembly":
                    {
                        _waitingForCompiledAssemblies.Add(new CompiledAssemblyState
                        {
                            Connection = message.Sender,
                        });
                    }
                    break;
                case "GetDiagnostics":
                    {
                        _waitingForDiagnostics.Add(message.Sender);
                    }
                    break;
                case "TestDiscovery":
                    {
                        DiscoverTests();
                    }
                    break;
                case "TestExecution":
                    {
                        var data = message.Payload.ToObject<TestExecutionMessage>();
                        ExecuteTests(data.Tests);
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

                    // All target framework information
                    Frameworks = frameworks,

                    // debug/release etc
                    ProjectConfigurations = configurations,

                    Commands = state.Project.Commands
                };

                var metadata = state.MetadataProvider.GetProjectMetadata(state.Project.Name);

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

                var export = state.LibraryManager.GetLibraryExport(state.Project.Name);
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

                if (!metadata.Errors.Any())
                {
                    projectReference.Load(engine);
                }

                _local.Compiled = new CompileMessage
                {
                    AssemblyBytes = engine.AssemblyBytes ?? new byte[0],
                    PdbBytes = engine.PdbBytes ?? new byte[0],
                    AssemblyPath = engine.AssemblyPath,
                    EmbeddedReferences = embeddedReferences
                };

                foreach (var waitingForCompiledAssembly in _waitingForCompiledAssemblies)
                {
                    if (waitingForCompiledAssembly.AssemblySent)
                    {
                        waitingForCompiledAssembly.ProjectChanged = true;
                    }
                }
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

                _initializedContext.Transmit(new Message
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

                _initializedContext.Transmit(new Message
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

                _initializedContext.Transmit(new Message
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

                _initializedContext.Transmit(new Message
                {
                    ContextId = Id,
                    MessageType = "Sources",
                    Payload = JToken.FromObject(_local.Sources)
                });

                _remote.Sources = _local.Sources;
            }

            for (int i = _waitingForCompiledAssemblies.Count - 1; i >= 0; i--)
            {
                var waitingForCompiledAssembly = _waitingForCompiledAssemblies[i];

                if (!waitingForCompiledAssembly.AssemblySent)
                {
                    Trace.TraceInformation("[ApplicationContext]: OnTransmit(Assembly)");

                    waitingForCompiledAssembly.Connection.Transmit(writer =>
                    {
                        writer.Write("Assembly");
                        writer.Write(Id);
                        writer.Write(_local.Diagnostics.Warnings.Count);
                        foreach (var warning in _local.Diagnostics.Warnings)
                        {
                            writer.Write(warning);
                        }
                        writer.Write(_local.Diagnostics.Errors.Count);
                        foreach (var error in _local.Diagnostics.Errors)
                        {
                            writer.Write(error);
                        }
                        writer.Write(_local.Compiled.EmbeddedReferences.Count);
                        foreach (var pair in _local.Compiled.EmbeddedReferences)
                        {
                            writer.Write(pair.Key);
                            writer.Write(pair.Value.Length);
                            writer.Write(pair.Value);
                        }
                        writer.Write(_local.Compiled.AssemblyBytes.Length);
                        writer.Write(_local.Compiled.AssemblyBytes);
                        writer.Write(_local.Compiled.PdbBytes.Length);
                        writer.Write(_local.Compiled.PdbBytes);
                    });

                    waitingForCompiledAssembly.AssemblySent = true;
                }

                if (waitingForCompiledAssembly.ProjectChanged && !waitingForCompiledAssembly.ProjectChangedSent)
                {
                    Trace.TraceInformation("[ApplicationContext]: OnTransmit(ProjectChanged)");

                    waitingForCompiledAssembly.Connection.Transmit(writer =>
                    {
                        writer.Write("ProjectChanged");
                        writer.Write(Id);
                    });

                    waitingForCompiledAssembly.ProjectChangedSent = true;

                    _waitingForCompiledAssemblies.Remove(waitingForCompiledAssembly);
                }
            }

            foreach (var connection in _waitingForDiagnostics)
            {
                connection.Transmit(new Message
                {
                    ContextId = Id,
                    MessageType = "Diagnostics",
                    Payload = JToken.FromObject(_local.Diagnostics)
                });
            }

            _waitingForDiagnostics.Clear();
        }

        private bool IsDifferent<T>(T local, T remote) where T : class
        {
            return !object.Equals(local, remote);
        }

        private State Initialize(string appPath, FrameworkName targetFramework, string configuration)
        {
            var state = new State
            {
                Path = appPath,
                TargetFramework = targetFramework,
                Configuration = configuration
            };

            var applicationHostContext = new ApplicationHostContext(_hostServices,
                                                                    appPath,
                                                                    packagesDirectory: null,
                                                                    configuration: configuration,
                                                                    targetFramework: targetFramework,
                                                                    cache: _cache);

            Project project = applicationHostContext.Project;

            if (project == null)
            {
                // package.json sad
                return state;
            }

            state.LibraryManager = (ILibraryManager)applicationHostContext.ServiceProvider.GetService(typeof(ILibraryManager));
            state.MetadataProvider = applicationHostContext.CreateInstance<ProjectMetadataProvider>();
            state.Project = project;
            state.FrameworkResolver = applicationHostContext.FrameworkReferenceResolver;

            applicationHostContext.DependencyWalker.Walk(state.Project.Name, state.Project.Version, targetFramework);

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

            state.Dependencies = applicationHostContext.DependencyWalker
                                                       .Libraries
                                                       .Select(referenceFactory)
                                                       .ToDictionary(d => d.Name);

            return state;
        }

        private async void ExecuteTests(IList<string> tests)
        {
            if (_appPath.Value == null)
            {
                throw new InvalidOperationException("The context must be initialized with a project.");
            }

            string testCommand = null;
            Project project = null;
            if (Project.TryGetProject(_appPath.Value, out project))
            {
                project.Commands.TryGetValue("test", out testCommand);
            }

            if (testCommand == null)
            {
                // No test command means no tests.
                Trace.TraceInformation("[ApplicationContext]: OnTransmit(ExecuteTests)");
                _initializedContext.Transmit(new Message
                {
                    ContextId = Id,
                    MessageType = "TestExecution.Response",
                });

                return;
            }

            var testServices = new ServiceProvider(_hostServices);
            testServices.Add(typeof(ITestExecutionSink), new TestExecutionSink(this));

            var args = new List<string>()
            {
                "test",
                "--designtime"
            };

            if (tests != null && tests.Count > 0)
            {
                args.Add("--test");
                args.Add(string.Join(",", tests));
            }

            try
            {
                await ExecuteCommandWithServices(testServices, project, args.ToArray());
            }
            catch
            {
                // For now we're not doing anything with these exceptions, we might want to report them
                // to VS.   
            }

            Trace.TraceInformation("[ApplicationContext]: OnTransmit(ExecuteTests)");
            _initializedContext.Transmit(new Message
            {
                ContextId = Id,
                MessageType = "TestExecution.Response",
            });
        }

        private async void DiscoverTests()
        {
            if (_appPath.Value == null)
            {
                throw new InvalidOperationException("The context must be initialized with a project.");
            }

            string testCommand = null;
            Project project = null;
            if (Project.TryGetProject(_appPath.Value, out project))
            {
                project.Commands.TryGetValue("test", out testCommand);
            }

            if (testCommand == null)
            {
                // No test command means no tests.
                Trace.TraceInformation("[ApplicationContext]: OnTransmit(DiscoverTests)");
                _initializedContext.Transmit(new Message
                {
                    ContextId = Id,
                    MessageType = "TestDiscovery.Response",
                });

                return;
            }

            var testServices = new ServiceProvider(_hostServices);
            testServices.Add(typeof(ITestDiscoverySink), new TestDiscoverySink(this));

            var args = new string[] { "test", "--list", "--designtime" };

            try
            {
                await ExecuteCommandWithServices(testServices, project, args);
            }
            catch
            {
                // For now we're not doing anything with these exceptions, we might want to report them
                // to VS.   
            }

            Trace.TraceInformation("[ApplicationContext]: OnTransmit(DiscoverTests)");
            _initializedContext.Transmit(new Message
            {
                ContextId = Id,
                MessageType = "TestDiscovery.Response",
            });
        }

        private async Task<int> ExecuteCommandWithServices(IServiceProvider services, Project project, string[] args)
        {
            var environment = new ApplicationEnvironment(project, _targetFramework.Value, _configuration.Value);

            var applicationHost = new ApplicationHost.Program(
                (IAssemblyLoaderContainer)_hostServices.GetService(typeof(IAssemblyLoaderContainer)),
                environment,
                services);

            try
            {
                return await applicationHost.Main(args);
            }
            catch (Exception ex)
            {
                Trace.TraceError("[ApplicationContext]: ExecutCommandWithServices" + Environment.NewLine + ex.ToString());
                _initializedContext.Transmit(new Message()
                {
                    ContextId = Id,
                    MessageType = "Error",
                    Payload = JToken.FromObject(new ErrorMessage()
                    {
                        Message = ex.ToString(),
                    }),
                });

                throw;
            };
        }

        public void Send(Message message)
        {
            message.ContextId = Id;
            _initializedContext.Transmit(message);
        }

        private class Trigger<TValue>
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

        private struct Nada
        {
        }

        private class State
        {
            public string Path { get; set; }

            public FrameworkName TargetFramework { get; set; }

            public string Configuration { get; set; }

            public Project Project { get; set; }

            public ProjectMetadataProvider MetadataProvider { get; set; }

            public ILibraryManager LibraryManager { get; set; }

            public IDictionary<string, ReferenceDescription> Dependencies { get; set; }

            public FrameworkReferenceResolver FrameworkResolver { get; set; }
        }

        private class CompiledAssemblyState
        {
            public ConnectionContext Connection { get; set; }

            public bool AssemblySent { get; set; }

            public bool ProjectChanged { get; set; }

            public bool ProjectChangedSent { get; set; }
        }
    }
}