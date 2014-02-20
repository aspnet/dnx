using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Versioning;
using System.Threading;
using Communication;
using Microsoft.Net.DesignTimeHost.Models;
using Microsoft.Net.DesignTimeHost.Models.IncomingMessages;
using Microsoft.Net.DesignTimeHost.Models.OutgoingMessages;
using Microsoft.Net.Runtime;
using Microsoft.Net.Runtime.FileSystem;
using Microsoft.Net.Runtime.Loader;
using Microsoft.Net.Runtime.Loader.MSBuildProject;
using Microsoft.Net.Runtime.Loader.NuGet;
using Microsoft.Net.Runtime.Loader.Roslyn;
using Microsoft.Net.Runtime.Roslyn;
using Newtonsoft.Json.Linq;
using NuGet;

namespace Microsoft.Net.DesignTimeHost
{
    public class ApplicationContext
    {
        private readonly IAssemblyLoaderEngine _loaderEngine;

        public class State
        {
            public string Path { get; set; }
            public FrameworkName TargetFramework { get; set; }
            public Project Project { get; set; }

            public RoslynAssemblyLoader Compiler { get; set; }
            public AssemblyLoader CompositeLoader { get; set; }
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
        private readonly Trigger<DateTimeOffset> _remoteHeartbeat = new Trigger<DateTimeOffset>();
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
            Message message;
            lock (_inbox)
            {
                message = _inbox.Dequeue();
                if (message == null)
                {
                    return;
                }
            }
            switch (message.MessageType)
            {
                case "Initialize":
                    {
                        var data = message.Payload.ToObject<InitializeMessage>();
                        _appPath.Value = data.ProjectFolder;
                        _targetFramework.Value = new FrameworkName(data.TargetFramework);
                    }
                    break;
                case "Heartbeat":
                    {
                        _remoteHeartbeat.Value = DateTimeOffset.UtcNow;
                    }
                    break;
                case "Teardown":
                    {
                    }
                    break;
                case "ChangeTargetFramework":
                    {
                        var data = message.Payload.ToObject<ChangeTargetFrameworkMessage>();
                        _targetFramework.Value = new FrameworkName(data.TargetFramework);
                    }
                    break;
                case "FilesChanged":
                    {
                        _filesChanged.Value = default(Nada);
                    }
                    break;
            }
        }

        private void Calculate()
        {
            if (_appPath.WasAssigned || _targetFramework.WasAssigned || _filesChanged.WasAssigned)
            {
                _appPath.ClearAssigned();
                _targetFramework.ClearAssigned();
                _filesChanged.ClearAssigned();

                _state.Value = Initialize(_appPath.Value, _targetFramework.Value);
            }

            var state = _state.Value;
            if (_state.WasAssigned && state != null)
            {
                _state.ClearAssigned();

                _local.Configurations = new ConfigurationsMessage
                {
                    Configurations = state.Project.GetTargetFrameworkConfigurations().Select(c => new ConfigurationData
                    {
                        CompilationOptions = state.Project.GetConfiguration(c.FrameworkName).Value["compilationOptions"],
                        FrameworkName = VersionUtility.GetShortFrameworkName(c.FrameworkName),
                    }).ToList()
                };

                var metadata = state.Compiler.GetProjectMetadata(state.Project.Name, state.TargetFramework);

                _local.References = new ReferencesMessage
                {
                    ProjectReferences = metadata.ProjectReferences,
                    FileReferences = metadata.References
                };

                _local.Diagnostics = new DiagnosticsMessage
                {
                    Warnings = metadata.Warnings.ToList(),
                    Errors = metadata.Errors.ToList(),
                };
            }
        }

        private void Reconcile()
        {
            if (IsDifferent(_local.Configurations, _remote.Configurations))
            {
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
                OnTransmit(new Message
                {
                    ContextId = Id,
                    MessageType = "Diagnostics",
                    Payload = JToken.FromObject(_local.Diagnostics)
                });
                _remote.Diagnostics = _local.Diagnostics;
            }
            OnTransmit(new Message
            {
                ContextId = Id,
                MessageType = "Heartbeat"
            });
        }

        private bool IsDifferent(ConfigurationsMessage local, ConfigurationsMessage remote)
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

        public State Initialize(string appPath, FrameworkName targetFramework)
        {
            var state = new State
            {
                Path = appPath,
                TargetFramework = targetFramework
            };

            Project project;
            if (!Project.TryGetProject(appPath, out project))
            {
                // package.json sad
                return state;
            }

            var globalAssemblyCache = new DefaultGlobalAssemblyCache();

            state.CompositeLoader = new AssemblyLoader();
            string rootDirectory = DefaultHost.ResolveRootDirectory(state.Path);
            var resolver = new FrameworkReferenceResolver(globalAssemblyCache);
            var resourceProvider = new ResxResourceProvider();
            var projectResolver = new ProjectResolver(state.Path, rootDirectory);
            state.Compiler = new RoslynAssemblyLoader(_loaderEngine, projectResolver, NoopWatcher.Instance, resolver, globalAssemblyCache, state.CompositeLoader, resourceProvider);
            state.CompositeLoader.Add(state.Compiler);
            state.CompositeLoader.Add(new MSBuildProjectAssemblyLoader(_loaderEngine, rootDirectory, NoopWatcher.Instance));
            state.CompositeLoader.Add(new NuGetAssemblyLoader(_loaderEngine, state.Path));

            state.CompositeLoader.Walk(state.Project.Name, state.Project.Version, state.TargetFramework);
            return state;
        }
    }
}