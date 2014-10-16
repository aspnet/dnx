// Copyright (c) Microsoft Open Technologies, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.Versioning;
using System.Threading;
using Microsoft.Framework.DesignTimeHost.Models;
using Microsoft.Framework.DesignTimeHost.Models.IncomingMessages;
using Microsoft.Framework.DesignTimeHost.Models.OutgoingMessages;
using Microsoft.Framework.Runtime;
using Microsoft.Framework.Runtime.Roslyn;
using Newtonsoft.Json.Linq;
using NuGet;
using Microsoft.Framework.Runtime.Roslyn.Services;
using Microsoft.CodeAnalysis.Text;

namespace Microsoft.Framework.DesignTimeHost
{
    public class ApplicationContext
    {
        private readonly IServiceProvider _hostServices;
        private readonly ICache _cache;
        private readonly ICacheContextAccessor _cacheContextAccessor;
        private readonly INamedCacheDependencyProvider _namedDependencyProvider;
        private readonly IApplicationEnvironment _appEnv;

        private readonly Queue<Message> _inbox = new Queue<Message>();
        private readonly object _processingLock = new object();

        private readonly Trigger<string> _appPath = new Trigger<string>();
        private readonly Trigger<string> _configuration = new Trigger<string>();
        private readonly Trigger<Void> _filesChanged = new Trigger<Void>();
        private readonly Trigger<Void> _rebuild = new Trigger<Void>();
        private readonly Trigger<Void> _sourceTextChanged = new Trigger<Void>();

        private readonly Trigger<State> _state = new Trigger<State>();

        private World _remote = new World();
        private World _local = new World();

        private ConnectionContext _initializedContext;
        private readonly Dictionary<FrameworkName, List<CompiledAssemblyState>> _waitingForCompiledAssemblies = new Dictionary<FrameworkName, List<CompiledAssemblyState>>();
        private readonly List<ConnectionContext> _waitingForDiagnostics = new List<ConnectionContext>();

        public ApplicationContext(IServiceProvider services,
                                  ICache cache,
                                  ICacheContextAccessor cacheContextAccessor,
                                  INamedCacheDependencyProvider namedDependencyProvider,
                                  int id)
        {
            _hostServices = services;
            _appEnv = (IApplicationEnvironment)services.GetService(typeof(IApplicationEnvironment));
            _cache = cache;
            _cacheContextAccessor = cacheContextAccessor;
            _namedDependencyProvider = namedDependencyProvider;
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
            while (true)
            {
                DrainInbox();
                Calculate();
                Reconcile();

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
                case "ChangeConfiguration":
                    {
                        var data = message.Payload.ToObject<ChangeConfigurationMessage>();
                        _configuration.Value = data.Configuration;
                    }
                    break;
                case "RestoreComplete":
                    {
                        _rebuild.Value = default(Void);
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
                case "SourceTextChanged":
                    {
                        var sourceTextService = (ISourceTextService) _hostServices.GetService(typeof(ISourceTextService));
                        var data = message.Payload.ToObject<SourceTextChangeMessage>();
                        if(data.isOffsetBased())
                        {
                            _sourceTextChanged.Value = default(Void);
                            sourceTextService.RecordTextChange(data.SourcePath, 
                                new TextSpan(data.Start ?? 0, data.Length ?? 0), 
                                data.NewText);
                        }
                        else if (data.isLineBased())
                        {
                            _sourceTextChanged.Value = default(Void);
                            sourceTextService.RecordTextChange(data.SourcePath, 
                                new LinePositionSpan(
                                    new LinePosition(data.StartLineNumber ?? 0, data.StartCharacter ?? 0), 
                                    new LinePosition(data.EndLineNumber ?? 0, data.EndCharacter ?? 0)), 
                                data.NewText);
                        }
                    }
                    break;
                case "GetCompiledAssembly":
                    {
                        var libraryKey = message.Payload.ToObject<LibraryKey>();
                        var targetFramework = new FrameworkName(libraryKey.TargetFramework);

                        List<CompiledAssemblyState> waitingForCompiledAssemblies;
                        if (!_waitingForCompiledAssemblies.TryGetValue(targetFramework, out waitingForCompiledAssemblies))
                        {
                            waitingForCompiledAssemblies = new List<CompiledAssemblyState>();
                            _waitingForCompiledAssemblies[targetFramework] = waitingForCompiledAssemblies;
                        }

                        waitingForCompiledAssemblies.Add(new CompiledAssemblyState
                        {
                            Connection = message.Sender
                        });
                    }
                    break;
                case "GetDiagnostics":
                    {
                        _waitingForDiagnostics.Add(message.Sender);
                    }
                    break;
            }

            return true;
        }

        private void Calculate()
        {
            if (_appPath.WasAssigned ||
                _configuration.WasAssigned ||
                _filesChanged.WasAssigned ||
                _rebuild.WasAssigned || 
                _sourceTextChanged.WasAssigned)
            {
                bool triggerBuildOutputs = _rebuild.WasAssigned;

                _appPath.ClearAssigned();
                _configuration.ClearAssigned();
                _filesChanged.ClearAssigned();
                _rebuild.ClearAssigned();
                _sourceTextChanged.ClearAssigned();

                _state.Value = Initialize(_appPath.Value, _configuration.Value, triggerBuildOutputs);
            }

            var state = _state.Value;

            if (_state.WasAssigned && state != null)
            {
                _state.ClearAssigned();

                _local = new World();
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

                for (int i = 0; i < state.Projects.Count; i++)
                {
                    var project = state.Projects[i];
                    var frameworkData = project.TargetFramework;

                    var projectWorld = new ProjectWorld
                    {
                        TargetFramework = project.FrameworkName,
                        Sources = new SourcesMessage
                        {
                            Framework = frameworkData,
                            Files = project.Metadata.SourceFiles
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
                            Dependencies = project.Dependencies
                        },
                        References = new ReferencesMessage
                        {
                            Framework = frameworkData,
                            ProjectReferences = project.ProjectReferences,
                            FileReferences = project.Metadata.References,
                            RawReferences = project.Metadata.RawReferences
                        },
                        Diagnostics = new DiagnosticsMessage
                        {
                            Framework = frameworkData,
                            Errors = project.Metadata.Errors,
                            Warnings = project.Metadata.Warnings
                        },
                        Outputs = new OutputsMessage
                        {
                            FrameworkData = frameworkData,
                            AssemblyBytes = project.Output.AssemblyBytes,
                            PdbBytes = project.Output.PdbBytes,
                            AssemblyPath = project.Output.AssemblyPath,
                            EmbeddedReferences = project.Output.EmbeddedReferences
                        }
                    };

                    _local.Projects[project.FrameworkName] = projectWorld;

                    List<CompiledAssemblyState> waitingForCompiledAssemblies;
                    if (_waitingForCompiledAssemblies.TryGetValue(project.FrameworkName, out waitingForCompiledAssemblies))
                    {
                        foreach (var waitingForCompiledAssembly in waitingForCompiledAssemblies)
                        {
                            if (waitingForCompiledAssembly.AssemblySent)
                            {
                                waitingForCompiledAssembly.ProjectChanged = true;
                            }
                        }
                    }
                }
            }
        }

        private void Reconcile()
        {
            if (IsDifferent(_local.ProjectInformation, _remote.ProjectInformation))
            {
                Trace.TraceInformation("[ApplicationContext]: OnTransmit(ProjectInformation)");

                _initializedContext.Transmit(new Message
                {
                    ContextId = Id,
                    MessageType = "ProjectInformation",
                    Payload = JToken.FromObject(_local.ProjectInformation)
                });

                _remote.ProjectInformation = _local.ProjectInformation;
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

                unprocessedFrameworks.Remove(pair.Key);

                if (IsDifferent(localProject.Dependencies, remoteProject.Dependencies))
                {
                    Trace.TraceInformation("[ApplicationContext]: OnTransmit(Dependencies)");

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
                    Trace.TraceInformation("[ApplicationContext]: OnTransmit(CompilerOptions)");

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
                    Trace.TraceInformation("[ApplicationContext]: OnTransmit(References)");

                    _initializedContext.Transmit(new Message
                    {
                        ContextId = Id,
                        MessageType = "References",
                        Payload = JToken.FromObject(localProject.References)
                    });

                    remoteProject.References = localProject.References;
                }

                if (IsDifferent(localProject.Diagnostics, remoteProject.Diagnostics))
                {
                    Trace.TraceInformation("[ApplicationContext]: OnTransmit(Diagnostics)");

                    _initializedContext.Transmit(new Message
                    {
                        ContextId = Id,
                        MessageType = "Diagnostics",
                        Payload = JToken.FromObject(localProject.Diagnostics)
                    });

                    remoteProject.Diagnostics = localProject.Diagnostics;
                }

                if (IsDifferent(localProject.Sources, remoteProject.Sources))
                {
                    Trace.TraceInformation("[ApplicationContext]: OnTransmit(Sources)");

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

            SendDiagnostics();

            // Remove all processed frameworks from the remote view
            foreach (var framework in unprocessedFrameworks)
            {
                _remote.Projects.Remove(framework);
            }
        }

        private void SendDiagnostics()
        {
            // Send all diagnostics back
            if (_waitingForDiagnostics.Count > 0)
            {
                var allDiagnostics = _local.Projects.Select(d => d.Value.Diagnostics).ToList();

                foreach (var connection in _waitingForDiagnostics)
                {
                    connection.Transmit(new Message
                    {
                        ContextId = Id,
                        MessageType = "AllDiagnostics",
                        Payload = JToken.FromObject(allDiagnostics)
                    });
                }

                _waitingForDiagnostics.Clear();
            }
        }

        private void SendCompiledAssemblies(ProjectWorld localProject)
        {
            List<CompiledAssemblyState> waitingForCompiledAssemblies;
            if (_waitingForCompiledAssemblies.TryGetValue(localProject.TargetFramework, out waitingForCompiledAssemblies))
            {
                for (int i = waitingForCompiledAssemblies.Count - 1; i >= 0; i--)
                {
                    var waitingForCompiledAssembly = waitingForCompiledAssemblies[i];

                    if (!waitingForCompiledAssembly.AssemblySent)
                    {
                        Trace.TraceInformation("[ApplicationContext]: OnTransmit(Assembly)");

                        waitingForCompiledAssembly.Connection.Transmit(writer =>
                        {
                            WriteAssembly(localProject, writer);
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

                        waitingForCompiledAssemblies.Remove(waitingForCompiledAssembly);
                    }
                }
            }
        }

        private void WriteAssembly(ProjectWorld project, BinaryWriter writer)
        {
            writer.Write("Assembly");
            writer.Write(Id);
            writer.Write(project.Diagnostics.Warnings.Count);
            foreach (var warning in project.Diagnostics.Warnings)
            {
                writer.Write(warning);
            }
            writer.Write(project.Diagnostics.Errors.Count);
            foreach (var error in project.Diagnostics.Errors)
            {
                writer.Write(error);
            }
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
            return !object.Equals(local, remote);
        }

        private State Initialize(string appPath, string configuration, bool triggerBuildOutputs)
        {
            var state = new State
            {
                Frameworks = new List<FrameworkData>(),
                Projects = new List<ProjectInfo>()
            };

            Project project;
            if (!Project.TryGetProject(appPath, out project))
            {
                throw new InvalidOperationException(string.Format("Unable to find project.json in '{0}'", appPath));
            }

            if (triggerBuildOutputs)
            {
                // Trigger the build outputs for this project
                _namedDependencyProvider.Trigger(project.Name + "_BuildOutputs");
            }

            state.Name = project.Name;
            state.Configurations = project.GetConfigurations().ToList();
            state.Commands = project.Commands;

            var frameworks = new List<FrameworkName>(
                project.GetTargetFrameworks()
                .Select(tf => tf.FrameworkName));

            if (!frameworks.Any())
            {
                frameworks.Add(VersionUtility.ParseFrameworkName("aspnet50"));
            }

            var appHostContextCache = new Dictionary<Tuple<string, FrameworkName>, ApplicationHostContext>();

            // We need to create an app env for the design time host's target framework
            var runtimeApplicationHostContext = new ApplicationHostContext(_hostServices,
                                                                           appPath,
                                                                           packagesDirectory: null,
                                                                           configuration: _appEnv.Configuration,
                                                                           targetFramework: _appEnv.RuntimeFramework,
                                                                           cache: _cache,
                                                                           cacheContextAccessor: _cacheContextAccessor,
                                                                           namedCacheDependencyProvider: _namedDependencyProvider);

            runtimeApplicationHostContext.DependencyWalker.Walk(project.Name, project.Version, _appEnv.RuntimeFramework);

            var runtimeAppHostContextCacheKey = Tuple.Create(_appEnv.Configuration, _appEnv.RuntimeFramework);

            // TODO: Move this caching to the ICache
            appHostContextCache[runtimeAppHostContextCacheKey] = runtimeApplicationHostContext;

            foreach (var frameworkName in frameworks)
            {
                var cacheKey = Tuple.Create(configuration, frameworkName);

                ApplicationHostContext applicationHostContext;
                if (!appHostContextCache.TryGetValue(cacheKey, out applicationHostContext))
                {
                    applicationHostContext = new ApplicationHostContext(_hostServices,
                                                                        appPath,
                                                                        packagesDirectory: null,
                                                                        configuration: configuration,
                                                                        targetFramework: frameworkName,
                                                                        cache: _cache,
                                                                        cacheContextAccessor: _cacheContextAccessor,
                                                                        namedCacheDependencyProvider: _namedDependencyProvider,
                                                                        loadContextFactory: runtimeApplicationHostContext.AssemblyLoadContextFactory);

                    applicationHostContext.DependencyWalker.Walk(project.Name, project.Version, frameworkName);

                    appHostContextCache[cacheKey] = applicationHostContext;
                }

                var libraryManager = applicationHostContext.LibraryManager;
                var metadataProvider = applicationHostContext.CreateInstance<ProjectMetadataProvider>();
                var frameworkResolver = applicationHostContext.FrameworkReferenceResolver;
                var metadata = metadataProvider.GetProjectMetadata(project.Name);

                var dependencies = applicationHostContext.DependencyWalker
                                                         .Libraries
                                                         .Select(CreateDependencyDescription)
                                                         .ToDictionary(d => d.Name);

                var projectReferences = applicationHostContext.DependencyWalker
                                                              .Libraries
                                                              .Where(d => string.Equals(d.Type, "Project") && !string.Equals(d.Identity.Name, project.Name))
                                                              .Select(d => new ProjectReference
                                                              {
                                                                  Framework = new FrameworkData
                                                                  {
                                                                      ShortName = VersionUtility.GetShortFrameworkName(d.Framework),
                                                                      FrameworkName = d.Framework.ToString(),
                                                                      FriendlyName = frameworkResolver.GetFriendlyFrameworkName(d.Framework)
                                                                  },
                                                                  Path = d.Path
                                                              })
                                                              .ToList();

                var frameworkData = new FrameworkData
                {
                    ShortName = VersionUtility.GetShortFrameworkName(frameworkName),
                    FrameworkName = frameworkName.ToString(),
                    FriendlyName = frameworkResolver.GetFriendlyFrameworkName(frameworkName),
                    RedistListPath = frameworkResolver.GetFrameworkRedistListPath(frameworkName)
                };

                state.Frameworks.Add(frameworkData);

                var projectInfo = new ProjectInfo()
                {
                    Path = appPath,
                    Configuration = configuration,
                    TargetFramework = frameworkData,
                    FrameworkName = frameworkName,
                    // TODO: This shouldn't be roslyn specific compilation options
                    CompilationSettings = project.GetCompilationSettings(frameworkName, configuration),
                    Dependencies = dependencies,
                    ProjectReferences = projectReferences,
                    Metadata = metadata,
                    Output = new ProjectOutput()
                };

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

                var engine = new NonLoadingLoadContext();

                if (!metadata.Errors.Any())
                {
                    projectReference.Load(engine);
                }

                projectInfo.Output.AssemblyBytes = engine.AssemblyBytes ?? new byte[0];
                projectInfo.Output.PdbBytes = engine.PdbBytes ?? new byte[0];
                projectInfo.Output.AssemblyPath = engine.AssemblyPath;
                projectInfo.Output.EmbeddedReferences = embeddedReferences;

                state.Projects.Add(projectInfo);

                if (state.ProjectSearchPaths == null)
                {
                    state.ProjectSearchPaths = applicationHostContext.ProjectResolver.SearchPaths.ToList();
                }

                if (state.GlobalJsonPath == null)
                {
                    GlobalSettings settings;
                    if (GlobalSettings.TryGetGlobalSettings(applicationHostContext.RootDirectory, out settings))
                    {
                        state.GlobalJsonPath = settings.FilePath;
                    }
                }
            }

            return state;
        }

        private static DependencyDescription CreateDependencyDescription(LibraryDescription library)
        {
            return new DependencyDescription
            {
                Name = library.Identity.Name,
                Version = library.Identity.Version == null ? null : library.Identity.Version.ToString(),
                Type = library.Type ?? "Unresolved",
                Path = library.Path,
                Dependencies = library.Dependencies.Select(lib => new DependencyItem
                {
                    Name = lib.Name,
                    Version = lib.Version == null ? null : lib.Version.ToString()
                })
            };
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

        private class State
        {
            public string Name { get; set; }

            public IList<string> ProjectSearchPaths { get; set; }

            public string GlobalJsonPath { get; set; }

            public IList<string> Configurations { get; set; }

            public IList<FrameworkData> Frameworks { get; set; }

            public IDictionary<string, string> Commands { get; set; }

            public IList<ProjectInfo> Projects { get; set; }
        }

        // Represents a project that should be used for intellisense
        private class ProjectInfo
        {
            public string Path { get; set; }

            public string Configuration { get; set; }

            public FrameworkName FrameworkName { get; set; }

            public FrameworkData TargetFramework { get; set; }

            public CompilationSettings CompilationSettings { get; set; }

            public ProjectMetadata Metadata { get; set; }

            public IDictionary<string, DependencyDescription> Dependencies { get; set; }

            public IList<ProjectReference> ProjectReferences { get; set; }

            public ProjectOutput Output { get; set; }
        }

        private class ProjectOutput
        {
            public IDictionary<string, byte[]> EmbeddedReferences { get; set; }

            public byte[] AssemblyBytes { get; set; }
            public byte[] PdbBytes { get; set; }

            public string AssemblyPath { get; set; }
        }

        private class CompiledAssemblyState
        {
            public ConnectionContext Connection { get; set; }

            public bool AssemblySent { get; set; }

            public bool ProjectChanged { get; set; }

            public bool ProjectChangedSent { get; set; }
        }

        private class LibraryKey
        {
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