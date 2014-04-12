using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.Versioning;
using System.Threading;
using Microsoft.Net.DesignTimeHost.Models;
using Microsoft.Net.DesignTimeHost.Models.IncomingMessages;
using Microsoft.Net.DesignTimeHost.Models.OutgoingMessages;
using Microsoft.Net.Runtime;
using Microsoft.Net.Runtime.FileSystem;
using Microsoft.Net.Runtime.Loader.NuGet;
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

            public RoslynMetadataProvider MetadataProvider { get; set; }
            public IDictionary<string, ReferenceDescription> Dependencies { get; set; }
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
            Message message;

            lock (_inbox)
            {
                message = _inbox.Dequeue();

                if (message == null)
                {
                    return;
                }
            }

            Trace.TraceInformation("[ApplicationContext]: Received {0}", message.MessageType);

            switch (message.MessageType)
            {
                case "Initialize":
                    {
                        var data = message.Payload.ToObject<InitializeMessage>();
                        _appPath.Value = data.ProjectFolder;

                        SetTargetFramework(data.TargetFramework);
                    }
                    break;
                case "Teardown":
                    {
                    }
                    break;
                case "ChangeTargetFramework":
                    {
                        var data = message.Payload.ToObject<ChangeTargetFrameworkMessage>();
                        SetTargetFramework(data.TargetFramework);
                    }
                    break;
                case "FilesChanged":
                    {
                        _filesChanged.Value = default(Nada);
                    }
                    break;
            }
        }

        private void SetTargetFramework(string targetFrameworkValue)
        {
            var targetFramework = VersionUtility.ParseFrameworkName(targetFrameworkValue ?? "net45");
            _targetFramework.Value = targetFramework == VersionUtility.UnsupportedFrameworkName ? new FrameworkName(targetFrameworkValue) : targetFramework;
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
                        CompilationSettings = state.Project.GetCompilationSettings(c.FrameworkName),
                        FrameworkName = VersionUtility.GetShortFrameworkName(c.FrameworkName),
                    }).ToList(),

                    Commands = state.Project.Commands
                };

                var metadata = state.MetadataProvider.GetMetadata(state.Project.Name, state.TargetFramework);

                _local.References = new ReferencesMessage
                {
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

        private bool IsDifferent(SourcesMessage local, SourcesMessage remote)
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

            var projectDir = project.ProjectDirectory;
            var rootDirectory = DefaultHost.ResolveRootDirectory(projectDir);
            var projectResolver = new ProjectResolver(projectDir, rootDirectory);

            var nugetDependencyResolver = new NuGetDependencyResolver(projectDir);
            var referenceAssemblyDependencyExporter = new ReferenceAssemblyLibraryExportProvider();
            var compositeDependencyExporter = new CompositeLibraryExportProvider(new ILibraryExportProvider[] { 
                nugetDependencyResolver,
                referenceAssemblyDependencyExporter, 
                new GacLibraryExportProvider()
            });

            var roslynCompiler = new RoslynCompiler(projectResolver,
                                                    NoopWatcher.Instance,
                                                    compositeDependencyExporter);

            state.MetadataProvider = new RoslynMetadataProvider(roslynCompiler);
            state.Project = project;

            var dependencyWalker = new DependencyWalker(new IDependencyProvider[] { 
                new ProjectReferenceDependencyProvider(projectResolver),
                nugetDependencyResolver,
                new UnresolvedDependencyProvider() // A catch all for unresolved dependencies
            });

            dependencyWalker.Walk(state.Project.Name, state.Project.Version, targetFramework);

            Func<LibraryDescription, ReferenceDescription> referenceFactory = library =>
            {
                var type = library.Type ?? ReferenceDescriptionType.Unresolved;
 
                if (VersionUtility.IsDesktop(targetFramework) && 
                    globalAssemblyCache.Contains(library.Identity.Name))
                {
                    // Special case GAC references
                    type = ReferenceDescriptionType.GAC;
                }

                return new ReferenceDescription
                {
                    Name = library.Identity.Name,
                    Version = library.Identity.Version == null ? null : library.Identity.Version.ToString(),
                    Type = type,
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

        private class UnresolvedDependencyProvider : IDependencyProvider
        {
            public LibraryDescription GetDescription(string name, SemanticVersion version, FrameworkName targetFramework)
            {
                return new LibraryDescription
                {
                    Identity = new Library { Name = name, Version = version },
                    Dependencies = Enumerable.Empty<Library>()
                };
            }

            public void Initialize(IEnumerable<LibraryDescription> dependencies, FrameworkName targetFramework)
            {

            }
        }
    }
}