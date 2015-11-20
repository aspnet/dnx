// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Versioning;
using System.Threading;
using Microsoft.Dnx.Compilation;
using Microsoft.Dnx.Compilation.Caching;
using Microsoft.Dnx.Compilation.DesignTime;
using Microsoft.Dnx.DesignTimeHost.InternalModels;
using Microsoft.Dnx.DesignTimeHost.Models;
using Microsoft.Dnx.DesignTimeHost.Models.IncomingMessages;
using Microsoft.Dnx.DesignTimeHost.Models.OutgoingMessages;
using Microsoft.Dnx.Runtime;
using Microsoft.Dnx.Runtime.Loader;
using Microsoft.Extensions.CompilationAbstractions;
using Microsoft.Extensions.CompilationAbstractions.Caching;
using Microsoft.Extensions.PlatformAbstractions;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Microsoft.Dnx.DesignTimeHost
{
    public class ApplicationContext
    {
        private readonly IApplicationEnvironment _applicationEnvironment;
        private readonly IRuntimeEnvironment _runtimeEnvironment;
        private readonly IAssemblyLoadContext _defaultLoadContext;
        private readonly FrameworkReferenceResolver _frameworkResolver;
        private readonly Queue<Message> _inbox = new Queue<Message>();
        private readonly object _processingLock = new object();

        private readonly Trigger<string> _appPath = new Trigger<string>();
        private readonly Trigger<string> _configuration = new Trigger<string>();
        private readonly Trigger<Void> _pluginWorkNeeded = new Trigger<Void>();
        private readonly Trigger<Void> _filesChanged = new Trigger<Void>();
        private readonly Trigger<Void> _rebuild = new Trigger<Void>();
        private readonly Trigger<Void> _refreshDependencies = new Trigger<Void>();
        private readonly Trigger<Void> _requiresCompilation = new Trigger<Void>();

        private World _remote = new World();
        private World _local = new World();

        private ConnectionContext _initializedContext;
        private readonly Dictionary<FrameworkName, List<CompiledAssemblyState>> _waitingForCompiledAssemblies = new Dictionary<FrameworkName, List<CompiledAssemblyState>>();
        private readonly List<ConnectionContext> _waitingForDiagnostics = new List<ConnectionContext>();
        private readonly Dictionary<FrameworkName, Trigger<Void>> _requiresAssemblies = new Dictionary<FrameworkName, Trigger<Void>>();
        private readonly Dictionary<FrameworkName, ProjectCompilation> _compilations = new Dictionary<FrameworkName, ProjectCompilation>();
        private readonly PluginHandler _pluginHandler;
        private readonly ProtocolManager _protocolManager;
        private readonly CompilationEngine _compilationEngine;
        private int? _contextProtocolVersion;

        private ProjectStateResolver _projectStateResolver;

        public ApplicationContext(IServiceProvider services,
                                  IApplicationEnvironment applicationEnvironment,
                                  IRuntimeEnvironment runtimeEnvironment,
                                  IAssemblyLoadContextAccessor loadContextAccessor,
                                  ProtocolManager protocolManager,
                                  CompilationEngine compilationEngine,
                                  FrameworkReferenceResolver frameworkResolver,
                                  int id)
        {
            _applicationEnvironment = applicationEnvironment;
            _runtimeEnvironment = runtimeEnvironment;
            _defaultLoadContext = loadContextAccessor.Default;
            _pluginHandler = new PluginHandler(services, SendPluginMessage);
            _protocolManager = protocolManager;
            _compilationEngine = compilationEngine;
            _frameworkResolver = frameworkResolver;
            Id = id;

            _projectStateResolver = new ProjectStateResolver(
                _compilationEngine,
                _frameworkResolver,
                (ctx, project, frameworkName) => CreateApplicationHostContext(ctx, project, frameworkName, Enumerable.Empty<string>()));
        }

        public int Id { get; private set; }

        public string ApplicationPath { get { return _appPath.Value; } }

        public int ProtocolVersion
        {
            get
            {
                if (_contextProtocolVersion.HasValue)
                {
                    return _contextProtocolVersion.Value;
                }
                else
                {
                    return _protocolManager.CurrentVersion;
                }
            }
        }

        public void OnReceive(Message message)
        {
            lock (_inbox)
            {
                _inbox.Enqueue(message);
            }

            ThreadPool.QueueUserWorkItem(state => ((ApplicationContext)state).ProcessLoop(), this);
        }

        public void ProcessLoop()
        {
            if (!Monitor.TryEnter(_processingLock))
            {
                return;
            }

            try
            {
                lock (_inbox)
                {
                    if (!_inbox.Any())
                    {
                        return;
                    }
                }

                DoProcessLoop();
            }
            catch (Exception ex)
            {
                Logger.TraceError("[ApplicationContext]: Error occurred: {0}", ex);

                // Unhandled errors
                var error = new ErrorMessage
                {
                    Message = ex.Message
                };

                var fileFormatException = ex as FileFormatException;
                if (fileFormatException != null)
                {
                    error.Path = fileFormatException.Path;
                    error.Line = fileFormatException.Line;
                    error.Column = fileFormatException.Column;
                }

                var message = new Message
                {
                    ContextId = Id,
                    MessageType = "Error",
                    Payload = JToken.FromObject(error)
                };

                _initializedContext.Transmit(message);
                _remote.GlobalErrorMessage = error;

                // Notify anyone waiting for diagnostics
                foreach (var connection in _waitingForDiagnostics)
                {
                    connection.Transmit(message);
                }

                _waitingForDiagnostics.Clear();

                // Notify the runtime of errors
                foreach (var frameworkGroup in _waitingForCompiledAssemblies.Values)
                {
                    foreach (var connection in frameworkGroup)
                    {
                        if (connection.Version > 0)
                        {
                            connection.AssemblySent = true;
                            connection.Connection.Transmit(message);
                        }
                    }
                }

                _waitingForCompiledAssemblies.Clear();
                _requiresAssemblies.Clear();
            }
            finally
            {
                Monitor.Exit(_processingLock);
            }
        }

        public void DoProcessLoop()
        {
            while (true)
            {
                DrainInbox();

                var allDiagnostics = new List<DiagnosticsListMessage>();

                if (ResolveDependencies())
                {
                    SendOutgoingMessages(allDiagnostics);
                }

                if (PerformCompilation())
                {
                    SendOutgoingMessages(allDiagnostics);
                }

                SendDiagnostics(allDiagnostics);

                PerformPluginWork();

                lock (_inbox)
                {
                    // If there's no more messages queued then bail out.
                    if (_inbox.Count == 0)
                    {
                        return;
                    }
                }
            }
        }

        private void DrainInbox()
        {
            Logger.TraceInformation($"[{nameof(ApplicationContext)}]: Begin draining inbox.");

            // Process all of the messages in the inbox
            while (ProcessMessage()) { }

            Logger.TraceInformation($"[{nameof(ApplicationContext)}]: Finish draining inbox.");
        }

        private bool ProcessMessage()
        {
            Message message;

            lock (_inbox)
            {
                if (!_inbox.Any())
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

            Logger.TraceInformation("[ApplicationContext]: Received {0}", message.MessageType);

            switch (message.MessageType)
            {
                case "Initialize":
                    {
                        // This should only be sent once
                        if (_initializedContext == null)
                        {
                            _initializedContext = message.Sender;

                            var data = new InitializeMessage
                            {
                                Version = GetValue<int>(message.Payload, "Version"),
                                Configuration = GetValue(message.Payload, "Configuration"),
                                ProjectFolder = GetValue(message.Payload, "ProjectFolder")
                            };

                            _appPath.Value = data.ProjectFolder;
                            _configuration.Value = data.Configuration ?? "Debug";

                            // Therefore context protocol version is set only when the version is not 0 (meaning 'Version'
                            // protocol is not missing) and protocol version is not overridden by environment variable.
                            if (data.Version != 0 && !_protocolManager.EnvironmentOverridden)
                            {
                                _contextProtocolVersion = Math.Min(data.Version, _protocolManager.MaxVersion);
                                Logger.TraceInformation($"[{nameof(ApplicationContext)}]: Set context protocol version to {_contextProtocolVersion.Value}");
                            }
                        }
                        else
                        {
                            Logger.TraceInformation("[ApplicationContext]: Received Initialize message more than once for {0}", _appPath.Value);
                        }
                    }
                    break;
                case "Teardown":
                    {
                        // TODO: Implement
                    }
                    break;
                case "ChangeConfiguration":
                    {
                        var data = new ChangeConfigurationMessage
                        {
                            Configuration = GetValue(message.Payload, "Configuration")
                        };
                        _configuration.Value = data.Configuration;
                    }
                    break;
                case "RefreshDependencies":
                case "RestoreComplete":
                    {
                        _refreshDependencies.Value = default(Void);
                    }
                    break;
                case "Rebuild":
                    {
                        _rebuild.Value = default(Void);
                    }
                    break;
                case "FilesChanged":
                    {
                        _filesChanged.Value = default(Void);
                    }
                    break;
                case "GetCompiledAssembly":
                    {
                        var libraryKey = new RemoteLibraryKey
                        {
                            Name = GetValue(message.Payload, "Name"),
                            TargetFramework = GetValue(message.Payload, "TargetFramework"),
                            Configuration = GetValue(message.Payload, "Configuration"),
                            Aspect = GetValue(message.Payload, "Aspect"),
                            Version = GetValue<int>(message.Payload, nameof(RemoteLibraryKey.Version)),
                        };

                        var targetFramework = new FrameworkName(libraryKey.TargetFramework);

                        // Only set this the first time for the project
                        if (!_requiresAssemblies.ContainsKey(targetFramework))
                        {
                            _requiresAssemblies[targetFramework] = new Trigger<Void>();
                        }

                        _requiresAssemblies[targetFramework].Value = default(Void);

                        List<CompiledAssemblyState> waitingForCompiledAssemblies;
                        if (!_waitingForCompiledAssemblies.TryGetValue(targetFramework, out waitingForCompiledAssemblies))
                        {
                            waitingForCompiledAssemblies = new List<CompiledAssemblyState>();
                            _waitingForCompiledAssemblies[targetFramework] = waitingForCompiledAssemblies;
                        }

                        waitingForCompiledAssemblies.Add(new CompiledAssemblyState
                        {
                            Connection = message.Sender,
                            Version = libraryKey.Version
                        });
                    }
                    break;
                case "GetDiagnostics":
                    {
                        _requiresCompilation.Value = default(Void);

                        _waitingForDiagnostics.Add(message.Sender);
                    }
                    break;
                case "Plugin":
                    {
                        var pluginMessage = message.Payload.ToObject<PluginMessage>();
                        var result = _pluginHandler.OnReceive(pluginMessage);

                        _refreshDependencies.Value = default(Void);
                        _pluginWorkNeeded.Value = default(Void);

                        if (result == PluginHandlerOnReceiveResult.RefreshDependencies)
                        {
                            _refreshDependencies.Value = default(Void);
                        }
                    }
                    break;
            }

            return true;
        }

        private bool ResolveDependencies()
        {
            ProjectState state = null;

            if (_appPath.WasAssigned ||
                _configuration.WasAssigned ||
                _filesChanged.WasAssigned ||
                _rebuild.WasAssigned ||
                _refreshDependencies.WasAssigned)
            {
                bool triggerBuildOutputs = _rebuild.WasAssigned || _filesChanged.WasAssigned;
                bool triggerDependencies = _refreshDependencies.WasAssigned || _rebuild.WasAssigned;

                _appPath.ClearAssigned();
                _configuration.ClearAssigned();
                _filesChanged.ClearAssigned();
                _rebuild.ClearAssigned();
                _refreshDependencies.ClearAssigned();

                // Trigger that the project outputs changes in case the runtime process
                // hasn't died yet
                TriggerProjectOutputsChanged();

                state = _projectStateResolver.Resolve(_appPath.Value,
                                                      _configuration.Value,
                                                      triggerBuildOutputs,
                                                      triggerDependencies,
                                                      ProtocolVersion,
                                                      _remote.ProjectInformation?.ProjectSearchPaths);
            }

            if (state == null)
            {
                return false;
            }

            _local = new World();
            _local.Project = state.Project;
            _local.ProjectInformation = new ProjectMessage
            {
                Name = state.Name,

                // All target framework information
                Frameworks = state.Frameworks,

                // debug/release etc
                Configurations = state.Configurations,

                Commands = state.Commands,

                ProjectSearchPaths = state.ProjectSearchPaths,

                GlobalJsonPath = state.GlobalJsonPath
            };

            _local.ProjectDiagnostics = new DiagnosticsListMessage(state.Diagnostics);

            foreach (var project in state.Projects)
            {
                var frameworkData = project.TargetFramework;

                var projectWorld = new ProjectWorld
                {
                    TargetFramework = project.FrameworkName,
                    Sources = new SourcesMessage
                    {
                        Framework = frameworkData,
                        Files = project.SourceFiles
                    },
                    CompilerOptions = new CompilationOptionsMessage
                    {
                        Framework = frameworkData,
                        CompilationOptions = project.CompilationSettings
                    },
                    Dependencies = new DependenciesMessage
                    {
                        Framework = frameworkData,
                        RootDependency = state.Name,
                        Dependencies = project.DependencyInfo.Dependencies
                    },
                    References = new ReferencesMessage
                    {
                        Framework = frameworkData,
                        ProjectReferences = project.DependencyInfo.ProjectReferences,
                        FileReferences = project.DependencyInfo.References,
                        RawReferences = project.DependencyInfo.RawReferences
                    },
                    DependencyDiagnostics = new DiagnosticsListMessage(project.DependencyInfo.Diagnostics, frameworkData)
                };

                _local.Projects[project.FrameworkName] = projectWorld;
            }

            if (_pluginHandler.FaultedPluginRegistrations)
            {
                var assemblyLoadContext = GetAppRuntimeLoadContext();

                _pluginHandler.TryRegisterFaultedPlugins(assemblyLoadContext);
            }

            return true;
        }

        private bool PerformCompilation()
        {
            bool calculateDiagnostics = _requiresCompilation.WasAssigned;

            if (calculateDiagnostics)
            {
                _requiresCompilation.ClearAssigned();
            }

            foreach (var pair in _local.Projects)
            {
                var project = pair.Value;
                var projectCompilationChanged = false;
                ProjectCompilation compilation = null;

                if (calculateDiagnostics)
                {
                    projectCompilationChanged = UpdateProjectCompilation(project, out compilation);

                    project.CompilationDiagnostics = new DiagnosticsListMessage(
                        compilation.Diagnostics,
                        project.Sources.Framework);
                }

                Trigger<Void> requiresAssemblies;
                if ((_requiresAssemblies.TryGetValue(pair.Key, out requiresAssemblies) &&
                    requiresAssemblies.WasAssigned))
                {
                    requiresAssemblies.ClearAssigned();

                    // If we didn't already update the compilation then do it on demand
                    if (compilation == null)
                    {
                        projectCompilationChanged = UpdateProjectCompilation(project, out compilation);
                    }

                    // Only emit the assembly if there are no errors and
                    // this is the very first time or there were changes
                    if (!compilation.Diagnostics.HasErrors() &&
                        (!compilation.HasOutputs || projectCompilationChanged))
                    {
                        var engine = new NonLoadingLoadContext();

                        compilation.ProjectReference.Load(new AssemblyName(_local.ProjectInformation.Name), engine);

                        compilation.AssemblyBytes = engine.AssemblyBytes ?? new byte[0];
                        compilation.PdbBytes = engine.PdbBytes ?? new byte[0];
                        compilation.AssemblyPath = engine.AssemblyPath;
                    }

                    project.Outputs = new OutputsMessage
                    {
                        FrameworkData = project.Sources.Framework,
                        AssemblyBytes = compilation.AssemblyBytes ?? new byte[0],
                        PdbBytes = compilation.PdbBytes ?? new byte[0],
                        AssemblyPath = compilation.AssemblyPath,
                        EmbeddedReferences = compilation.EmbeddedReferences
                    };

                    if (project.CompilationDiagnostics == null)
                    {
                        project.CompilationDiagnostics = new DiagnosticsListMessage(
                            compilation.Diagnostics,
                            project.Sources.Framework);
                    }
                }
            }

            return true;
        }

        private void PerformPluginWork()
        {
            if (_pluginWorkNeeded.WasAssigned)
            {
                _pluginWorkNeeded.ClearAssigned();

                var assemblyLoadContext = GetAppRuntimeLoadContext();

                _pluginHandler.ProcessMessages(assemblyLoadContext);
            }
        }

        private IAssemblyLoadContext GetAppRuntimeLoadContext()
        {
            Project project;
            if (!Project.TryGetProject(_appPath.Value, out project))
            {
                throw new InvalidOperationException(
                    Resources.FormatPlugin_UnableToFindProjectJson(_appPath.Value));
            }

            var hostContextKey = Tuple.Create("RuntimeLoadContext", project.Name, _applicationEnvironment.RuntimeFramework);

            return _compilationEngine.CompilationCache.Cache.Get<IAssemblyLoadContext>(hostContextKey, ctx =>
            {
                var appHostContext = CreateApplicationHostContext(ctx, project, _applicationEnvironment.RuntimeFramework, _runtimeEnvironment.GetAllRuntimeIdentifiers());

                return new RuntimeLoadContext($"{project.Name}_plugin", appHostContext.LibraryManager.GetLibraryDescriptions(), _compilationEngine, _defaultLoadContext, _configuration.Value);
            });
        }

        private bool UpdateProjectCompilation(ProjectWorld project, out ProjectCompilation compilation)
        {
            var exporter = _compilationEngine.CreateProjectExporter(_local.Project, project.TargetFramework, _configuration.Value);
            var export = exporter.GetExport(_local.ProjectInformation.Name);

            ProjectCompilation oldCompilation;
            if (!_compilations.TryGetValue(project.TargetFramework, out oldCompilation) ||
                export != oldCompilation?.Export)
            {
                compilation = new ProjectCompilation();
                compilation.Export = export;
                compilation.EmbeddedReferences = new Dictionary<string, byte[]>();
                foreach (var reference in compilation.Export.MetadataReferences)
                {
                    if (compilation.ProjectReference == null)
                    {
                        compilation.ProjectReference = reference as IMetadataProjectReference;
                    }

                    var embedded = reference as IMetadataEmbeddedReference;
                    if (embedded != null)
                    {
                        compilation.EmbeddedReferences[embedded.Name] = embedded.Contents;
                    }
                }

                var diagnostics = compilation.ProjectReference.GetDiagnostics();
                compilation.Diagnostics = diagnostics.Diagnostics.ToList();

                _compilations[project.TargetFramework] = compilation;

                return true;
            }

            compilation = oldCompilation;
            return false;
        }

        private void SendOutgoingMessages(List<DiagnosticsListMessage> diagnostics)
        {
            if (IsDifferent(_local.ProjectInformation, _remote.ProjectInformation))
            {
                Logger.TraceInformation("[ApplicationContext]: OnTransmit(ProjectInformation)");

                _initializedContext.Transmit(new Message
                {
                    ContextId = Id,
                    MessageType = "ProjectInformation",
                    Payload = JToken.FromObject(_local.ProjectInformation)
                });

                _remote.ProjectInformation = _local.ProjectInformation;
            }

            if (_local.ProjectDiagnostics != null)
            {
                diagnostics.Add(_local.ProjectDiagnostics);
            }

            if (IsDifferent(_local.ProjectDiagnostics, _remote.ProjectDiagnostics))
            {
                _initializedContext.Transmit(new Message
                {
                    ContextId = Id,
                    MessageType = "Diagnostics",
                    Payload = _local.ProjectDiagnostics.ConvertToJson(ProtocolVersion)
                });

                _remote.ProjectDiagnostics = _local.ProjectDiagnostics;
            }

            var unprocessedFrameworks = new HashSet<FrameworkName>(_remote.Projects.Keys);

            foreach (var pair in _local.Projects)
            {
                ProjectWorld localProject = pair.Value;
                ProjectWorld remoteProject;

                if (!_remote.Projects.TryGetValue(pair.Key, out remoteProject))
                {
                    remoteProject = new ProjectWorld();
                    _remote.Projects[pair.Key] = remoteProject;
                }

                if (localProject.DependencyDiagnostics != null)
                {
                    diagnostics.Add(localProject.DependencyDiagnostics);
                }

                if (localProject.CompilationDiagnostics != null)
                {
                    diagnostics.Add(localProject.CompilationDiagnostics);
                }

                unprocessedFrameworks.Remove(pair.Key);

                if (IsDifferent(localProject.DependencyDiagnostics, remoteProject.DependencyDiagnostics))
                {
                    Logger.TraceInformation("[ApplicationContext]: OnTransmit(DependencyDiagnostics)");

                    _initializedContext.Transmit(new Message
                    {
                        ContextId = Id,
                        MessageType = "DependencyDiagnostics",
                        Payload = localProject.DependencyDiagnostics.ConvertToJson(ProtocolVersion)
                    });

                    remoteProject.DependencyDiagnostics = localProject.DependencyDiagnostics;
                }

                if (IsDifferent(localProject.Dependencies, remoteProject.Dependencies))
                {
                    Logger.TraceInformation("[ApplicationContext]: OnTransmit(Dependencies)");

                    _initializedContext.Transmit(new Message
                    {
                        ContextId = Id,
                        MessageType = "Dependencies",
                        Payload = JToken.FromObject(localProject.Dependencies)
                    });

                    remoteProject.Dependencies = localProject.Dependencies;
                }

                if (IsDifferent(localProject.CompilerOptions, remoteProject.CompilerOptions))
                {
                    Logger.TraceInformation("[ApplicationContext]: OnTransmit(CompilerOptions)");

                    _initializedContext.Transmit(new Message
                    {
                        ContextId = Id,
                        MessageType = "CompilerOptions",
                        Payload = JToken.FromObject(localProject.CompilerOptions)
                    });

                    remoteProject.CompilerOptions = localProject.CompilerOptions;
                }

                if (IsDifferent(localProject.References, remoteProject.References))
                {
                    Logger.TraceInformation("[ApplicationContext]: OnTransmit(References)");

                    _initializedContext.Transmit(new Message
                    {
                        ContextId = Id,
                        MessageType = "References",
                        Payload = JToken.FromObject(localProject.References)
                    });

                    remoteProject.References = localProject.References;
                }

                if (IsDifferent(localProject.Sources, remoteProject.Sources))
                {
                    Logger.TraceInformation("[ApplicationContext]: OnTransmit(Sources)");

                    _initializedContext.Transmit(new Message
                    {
                        ContextId = Id,
                        MessageType = "Sources",
                        Payload = JToken.FromObject(localProject.Sources)
                    });

                    remoteProject.Sources = localProject.Sources;
                }

                SendCompiledAssemblies(localProject);
            }

            // Remove all processed frameworks from the remote view
            foreach (var framework in unprocessedFrameworks)
            {
                _remote.Projects.Remove(framework);
            }
        }

        private void SendDiagnostics(IEnumerable<DiagnosticsListMessage> allDiagnostics)
        {
            Logger.TraceInformation($"[{nameof(ApplicationContext)}]: SendDiagnostics, {allDiagnostics.Count()} diagnostics, {_waitingForDiagnostics.Count()} waiting for diagnostics.");

            if (!allDiagnostics.Any())
            {
                return;
            }

            // Group all of the diagnostics into group by target framework

            var messages = new List<DiagnosticsListMessage>();

            foreach (var g in allDiagnostics.GroupBy(g => g.Framework))
            {
                var messageGroup = g.SelectMany(d => d.Diagnostics).ToList();
                messages.Add(new DiagnosticsListMessage(messageGroup, g.Key));
            }

            var payload = JToken.FromObject(messages.Select(d => d.ConvertToJson(ProtocolVersion)));

            if (IsDifferent(_local.GlobalErrorMessage, _remote.GlobalErrorMessage))
            {
                var message = new Message
                {
                    ContextId = Id,
                    MessageType = "Error",
                    Payload = JToken.FromObject(_local.GlobalErrorMessage)
                };

                _initializedContext.Transmit(message);
                foreach (var connection in _waitingForDiagnostics)
                {
                    connection.Transmit(message);
                }

                _remote.GlobalErrorMessage = _local.GlobalErrorMessage;
            }

            // Send all diagnostics back
            foreach (var connection in _waitingForDiagnostics)
            {
                connection.Transmit(new Message
                {
                    ContextId = Id,
                    MessageType = "AllDiagnostics",
                    Payload = payload
                });
            }

            _waitingForDiagnostics.Clear();
        }

        public void SendPluginMessage(object data)
        {
            SendMessage(data, messageType: "Plugin");
        }

        public void SendMessage(object data, string messageType)
        {
            var message = new Message
            {
                ContextId = Id,
                MessageType = messageType,
                Payload = JToken.FromObject(data)
            };

            _initializedContext.Transmit(message);
        }

        private void TriggerProjectOutputsChanged()
        {
            foreach (var pair in _waitingForCompiledAssemblies)
            {
                var waitingForCompiledAssemblies = pair.Value;

                for (int i = waitingForCompiledAssemblies.Count - 1; i >= 0; i--)
                {
                    var waitingForCompiledAssembly = waitingForCompiledAssemblies[i];

                    if (waitingForCompiledAssembly.AssemblySent)
                    {
                        Logger.TraceInformation("[ApplicationContext]: OnTransmit(ProjectChanged)");

                        waitingForCompiledAssembly.Connection.Transmit(writer =>
                        {
                            if (waitingForCompiledAssembly.Version == 0)
                            {
                                writer.Write("ProjectChanged");
                                writer.Write(Id);
                            }
                            else
                            {
                                var obj = new JObject();
                                obj["MessageType"] = "ProjectChanged";
                                obj["ContextId"] = Id;
                                writer.Write(obj.ToString(Formatting.None));
                            }
                        });

                        waitingForCompiledAssemblies.Remove(waitingForCompiledAssembly);
                    }
                }
            }
        }

        private void SendCompiledAssemblies(ProjectWorld localProject)
        {
            if (localProject.Outputs == null)
            {
                return;
            }

            List<CompiledAssemblyState> waitingForCompiledAssemblies;
            if (_waitingForCompiledAssemblies.TryGetValue(localProject.TargetFramework, out waitingForCompiledAssemblies))
            {
                foreach (var waitingForCompiledAssembly in waitingForCompiledAssemblies)
                {
                    if (!waitingForCompiledAssembly.AssemblySent)
                    {
                        Logger.TraceInformation("[ApplicationContext]: OnTransmit(Assembly)");

                        int version = waitingForCompiledAssembly.Version;

                        waitingForCompiledAssembly.Connection.Transmit(writer =>
                        {
                            WriteProjectSources(version, localProject, writer);
                            WriteAssembly(version, localProject, writer);
                        });

                        waitingForCompiledAssembly.AssemblySent = true;
                    }
                }
            }
        }

        private void WriteProjectSources(int version, ProjectWorld project, BinaryWriter writer)
        {
            if (version == 0)
            {
                writer.Write("Sources");
                writer.Write(project.Sources.Files.Count);
                foreach (var file in project.Sources.Files)
                {
                    writer.Write(file);
                }
            }
            else
            {
                var obj = new JObject();
                obj["MessageType"] = "Sources";
                obj["Files"] = new JArray(project.Sources.Files);
                writer.Write(obj.ToString(Formatting.None));
            }
        }

        private void WriteAssembly(int version, ProjectWorld project, BinaryWriter writer)
        {
            if (version == 0)
            {
                writer.Write("Assembly");
                writer.Write(Id);

                writer.Write(project.CompilationDiagnostics.Warnings.Count);
                foreach (var warning in project.CompilationDiagnostics.Warnings)
                {
                    writer.Write(warning.FormattedMessage);
                }

                writer.Write(project.CompilationDiagnostics.Errors.Count);
                foreach (var error in project.CompilationDiagnostics.Errors)
                {
                    writer.Write(error.FormattedMessage);
                }

                WriteAssembly(project, writer);
            }
            else
            {
                var obj = new JObject();
                obj["MessageType"] = "Assembly";
                obj["ContextId"] = Id;
                obj[nameof(CompileResponse.Diagnostics)] = ConvertToJArray(project.CompilationDiagnostics.Diagnostics);
                obj[nameof(CompileResponse.AssemblyPath)] = project.Outputs.AssemblyPath;
                obj["Blobs"] = 2;
                writer.Write(obj.ToString(Formatting.None));

                WriteAssembly(project, writer);
            }
        }

        private static JArray ConvertToJArray(IList<DiagnosticMessageView> diagnostics)
        {
            var values = diagnostics.Select(diagnostic => new JObject
            {
                [nameof(DiagnosticMessage.SourceFilePath)] = diagnostic.SourceFilePath,
                [nameof(DiagnosticMessage.Message)] = diagnostic.Message,
                [nameof(DiagnosticMessage.FormattedMessage)] = diagnostic.FormattedMessage,
                [nameof(DiagnosticMessage.Severity)] = (int)diagnostic.Severity,
                [nameof(DiagnosticMessage.StartColumn)] = diagnostic.StartColumn,
                [nameof(DiagnosticMessage.StartLine)] = diagnostic.StartLine,
                [nameof(DiagnosticMessage.EndColumn)] = diagnostic.EndColumn,
                [nameof(DiagnosticMessage.EndLine)] = diagnostic.EndLine,
            });

            return new JArray(values);
        }

        private static void WriteAssembly(ProjectWorld project, BinaryWriter writer)
        {
            writer.Write(project.Outputs.EmbeddedReferences.Count);
            foreach (var pair in project.Outputs.EmbeddedReferences)
            {
                writer.Write(pair.Key);
                writer.Write(pair.Value.Length);
                writer.Write(pair.Value);
            }
            writer.Write(project.Outputs.AssemblyBytes.Length);
            writer.Write(project.Outputs.AssemblyBytes);
            writer.Write(project.Outputs.PdbBytes.Length);
            writer.Write(project.Outputs.PdbBytes);
        }

        private bool IsDifferent<T>(T local, T remote) where T : class
        {
            // If no value was ever produced, then don't even bother
            if (local == null)
            {
                return false;
            }

            return !object.Equals(local, remote);
        }

        private ApplicationHostContext CreateApplicationHostContext(CacheContext ctx, Project project, FrameworkName frameworkName, IEnumerable<string> runtimeIdentifiers)
        {
            var applicationHostContext = new ApplicationHostContext
            {
                Project = project,
                TargetFramework = frameworkName,
                RuntimeIdentifiers = runtimeIdentifiers,
                FrameworkResolver = _frameworkResolver
            };

            ApplicationHostContext.Initialize(applicationHostContext);

            // Watch all projects for project.json changes
            foreach (var library in applicationHostContext.LibraryManager.GetLibraryDescriptions())
            {
                if (string.Equals(library.Type, "Project"))
                {
                    ctx.Monitor(new FileWriteTimeCacheDependency(library.Path));
                }
            }

            // Add a cache dependency on restore complete to reevaluate dependencies
            ctx.Monitor(_compilationEngine.CompilationCache.NamedCacheDependencyProvider.GetNamedDependency(project.Name + "_Dependencies"));

            return applicationHostContext;
        }

        private static string GetValue(JToken token, string name)
        {
            return GetValue<string>(token, name);
        }

        private static TVal GetValue<TVal>(JToken token, string name)
        {
            var value = token?[name];
            if (value != null)
            {
                return value.Value<TVal>();
            }

            return default(TVal);
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

        private class ProjectCompilation
        {
            public LibraryExport Export { get; set; }

            public IMetadataProjectReference ProjectReference { get; set; }

            public Dictionary<string, byte[]> EmbeddedReferences { get; set; }

            public List<DiagnosticMessage> Diagnostics { get; set; }

            public bool HasOutputs
            {
                get
                {
                    return AssemblyBytes != null || AssemblyPath != null;
                }
            }

            public byte[] AssemblyBytes { get; set; }

            public byte[] PdbBytes { get; set; }

            public string AssemblyPath { get; set; }
        }

        private class CompiledAssemblyState
        {
            public ConnectionContext Connection { get; set; }

            public bool AssemblySent { get; set; }

            public int Version { get; set; }
        }

        private class RemoteLibraryKey
        {
            public int Version { get; set; }

            public string Name { get; set; }

            public string TargetFramework { get; set; }

            public string Configuration { get; set; }

            public string Aspect { get; set; }
        }

        private struct Void
        {
        }
    }
}
