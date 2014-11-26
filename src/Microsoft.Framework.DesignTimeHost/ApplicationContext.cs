// Copyright (c) Microsoft Open Technologies, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.Versioning;
using System.Threading;
using Microsoft.CodeAnalysis.Text;
using Microsoft.Framework.DesignTimeHost.Models;
using Microsoft.Framework.DesignTimeHost.Models.IncomingMessages;
using Microsoft.Framework.DesignTimeHost.Models.OutgoingMessages;
using Microsoft.Framework.Runtime;
using Microsoft.Framework.Runtime.Loader;
using Microsoft.Framework.Runtime.Roslyn;
using Microsoft.Framework.Runtime.Roslyn.Services;
using Newtonsoft.Json.Linq;
using NuGet;

namespace Microsoft.Framework.DesignTimeHost
{
    public class ApplicationContext
    {
        private readonly IServiceProvider _hostServices;
        private readonly ICache _cache;
        private readonly ICacheContextAccessor _cacheContextAccessor;
        private readonly INamedCacheDependencyProvider _namedDependencyProvider;
        private readonly IApplicationEnvironment _appEnv;
        private readonly ISourceTextService _sourceTextService;
        private readonly Queue<Message> _inbox = new Queue<Message>();
        private readonly object _processingLock = new object();

        private readonly Trigger<string> _appPath = new Trigger<string>();
        private readonly Trigger<string> _configuration = new Trigger<string>();
        private readonly Trigger<Void> _filesChanged = new Trigger<Void>();
        private readonly Trigger<Void> _rebuild = new Trigger<Void>();
        private readonly Trigger<Void> _restoreComplete = new Trigger<Void>();
        private readonly Trigger<Void> _sourceTextChanged = new Trigger<Void>();
        private readonly Trigger<Void> _requiresCompilation = new Trigger<Void>();

        private World _remote = new World();
        private World _local = new World();

        private ConnectionContext _initializedContext;
        private readonly Dictionary<FrameworkName, List<CompiledAssemblyState>> _waitingForCompiledAssemblies = new Dictionary<FrameworkName, List<CompiledAssemblyState>>();
        private readonly List<ConnectionContext> _waitingForDiagnostics = new List<ConnectionContext>();
        private readonly Dictionary<FrameworkName, Trigger<Void>> _requiresAssemblies = new Dictionary<FrameworkName, Trigger<Void>>();
        private readonly Dictionary<FrameworkName, ProjectCompilation> _compilations = new Dictionary<FrameworkName, ProjectCompilation>();

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
            _sourceTextService = (ISourceTextService)services.GetService(typeof(ISourceTextService));
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
                    Message = ex.Message
                };

                var message = new Message
                {
                    ContextId = Id,
                    MessageType = "Error",
                    Payload = JToken.FromObject(error)
                };

                _initializedContext.Transmit(message);

                // Notify anyone waiting for diagnostics
                foreach (var connection in _waitingForDiagnostics)
                {
                    connection.Transmit(message);
                }

                _waitingForDiagnostics.Clear();
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

                if (DoStageOne())
                {
                    Reconcile();
                }

                if (DoStageTwo())
                {
                    Reconcile();
                }

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
                        _restoreComplete.Value = default(Void);
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
                        var data = message.Payload.ToObject<SourceTextChangeMessage>();
                        if (data.IsOffsetBased)
                        {
                            _sourceTextChanged.Value = default(Void);
                            _sourceTextService.RecordTextChange(data.SourcePath,
                                new TextSpan(data.Start ?? 0, data.Length ?? 0),
                                data.NewText);
                        }
                        else if (data.IsLineBased)
                        {
                            _sourceTextChanged.Value = default(Void);
                            _sourceTextService.RecordTextChange(data.SourcePath,
                                new LinePositionSpan(
                                    new LinePosition(data.StartLineNumber ?? 0, data.StartCharacter ?? 0),
                                    new LinePosition(data.EndLineNumber ?? 0, data.EndCharacter ?? 0)),
                                    data.NewText);
                        }
                    }
                    break;
                case "GetCompiledAssembly":
                    {
                        var libraryKey = message.Payload.ToObject<RemoteLibraryKey>();
                        var targetFramework = new FrameworkName(libraryKey.TargetFramework);

                        // Only set this the first time for the project
                        if (!_requiresAssemblies.ContainsKey(targetFramework))
                        {
                            _requiresAssemblies[targetFramework] = new Trigger<Void>();
                            _requiresAssemblies[targetFramework].Value = default(Void);
                        }

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
                        _requiresCompilation.Value = default(Void);

                        _waitingForDiagnostics.Add(message.Sender);
                    }
                    break;
            }

            return true;
        }

        private bool DoStageOne()
        {
            State state = null;

            if (_appPath.WasAssigned ||
                _configuration.WasAssigned ||
                _filesChanged.WasAssigned ||
                _rebuild.WasAssigned ||
                _restoreComplete.WasAssigned ||
                _sourceTextChanged.WasAssigned)
            {
                bool triggerBuildOutputs = _rebuild.WasAssigned || _filesChanged.WasAssigned;
                bool triggerRestoreComplete = _restoreComplete.WasAssigned;

                _appPath.ClearAssigned();
                _configuration.ClearAssigned();
                _filesChanged.ClearAssigned();
                _rebuild.ClearAssigned();
                _sourceTextChanged.ClearAssigned();
                _restoreComplete.ClearAssigned();

                // Trigger that the project outputs changes in case the runtime process
                // hasn't died yet
                TriggerProjectOutputsChanged();

                state = DoInitialWork(_appPath.Value, _configuration.Value, triggerBuildOutputs, triggerRestoreComplete);
            }

            if (state == null)
            {
                return false;
            }

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

            foreach (var project in state.Projects)
            {
                var frameworkData = project.TargetFramework;

                var projectWorld = new ProjectWorld
                {
                    ApplicationHostContext = project.DependencyInfo.HostContext,
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
                    }
                };

                _local.Projects[project.FrameworkName] = projectWorld;
            }

            return true;
        }

        private bool DoStageTwo()
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

                    project.Diagnostics = new DiagnosticsMessage
                    {
                        Framework = project.Sources.Framework,
                        Errors = compilation.Errors,
                        Warnings = compilation.Warnings
                    };
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
                    if (!compilation.Errors.Any() &&
                        (!compilation.HasOutputs || projectCompilationChanged))
                    {
                        var engine = new NonLoadingLoadContext();

                        compilation.ProjectReference.Load(engine);

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

                    if (project.Diagnostics == null)
                    {
                        project.Diagnostics = new DiagnosticsMessage
                        {
                            Framework = project.Sources.Framework,
                            Errors = compilation.Errors,
                            Warnings = compilation.Warnings
                        };
                    }
                }
            }

            return true;
        }

        private bool UpdateProjectCompilation(ProjectWorld project, out ProjectCompilation compilation)
        {
            var export = project.ApplicationHostContext.LibraryManager.GetLibraryExport(_local.ProjectInformation.Name);

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
                compilation.Errors = diagnostics.Errors.ToList();
                compilation.Warnings = diagnostics.Warnings.ToList();

                _compilations[project.TargetFramework] = compilation;

                return true;
            }

            compilation = oldCompilation;
            return false;
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
            var allDiagnostics = new List<DiagnosticsMessage>();

            foreach (var pair in _local.Projects)
            {
                ProjectWorld localProject = pair.Value;
                ProjectWorld remoteProject;

                if (!_remote.Projects.TryGetValue(pair.Key, out remoteProject))
                {
                    remoteProject = new ProjectWorld();
                    _remote.Projects[pair.Key] = remoteProject;
                }

                if (localProject.Diagnostics != null)
                {
                    allDiagnostics.Add(localProject.Diagnostics);
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

            SendDiagnostics(allDiagnostics);

            // Remove all processed frameworks from the remote view
            foreach (var framework in unprocessedFrameworks)
            {
                _remote.Projects.Remove(framework);
            }
        }

        private void SendDiagnostics(IList<DiagnosticsMessage> diagnostics)
        {
            if (diagnostics.Count == 0)
            {
                return;
            }

            // Send all diagnostics back
            foreach (var connection in _waitingForDiagnostics)
            {
                connection.Transmit(new Message
                {
                    ContextId = Id,
                    MessageType = "AllDiagnostics",
                    Payload = JToken.FromObject(diagnostics)
                });
            }

            _waitingForDiagnostics.Clear();
        }

        private void TriggerProjectOutputsChanged()
        {
            foreach (var pair in _waitingForCompiledAssemblies)
            {
                var waitingForCompiledAssemblies = pair.Value;

                _requiresAssemblies[pair.Key].Value = default(Void);

                for (int i = waitingForCompiledAssemblies.Count - 1; i >= 0; i--)
                {
                    var waitingForCompiledAssembly = waitingForCompiledAssemblies[i];

                    if (waitingForCompiledAssembly.AssemblySent)
                    {
                        Trace.TraceInformation("[ApplicationContext]: OnTransmit(ProjectChanged)");

                        waitingForCompiledAssembly.Connection.Transmit(writer =>
                        {
                            writer.Write("ProjectChanged");
                            writer.Write(Id);
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
                        Trace.TraceInformation("[ApplicationContext]: OnTransmit(Assembly)");

                        waitingForCompiledAssembly.Connection.Transmit(writer =>
                        {
                            WriteProjectSources(localProject, writer);
                            WriteAssembly(localProject, writer);
                        });

                        waitingForCompiledAssembly.AssemblySent = true;
                    }
                }
            }
        }

        private void WriteProjectSources(ProjectWorld project, BinaryWriter writer)
        {
            writer.Write("Sources");
            writer.Write(project.Sources.Files.Count);
            foreach (var file in project.Sources.Files)
            {
                writer.Write(file);
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
            // If no value was ever produced, then don't even bother
            if (local == null)
            {
                return false;
            }

            return !object.Equals(local, remote);
        }

        private State DoInitialWork(string appPath, string configuration, bool triggerBuildOutputs, bool triggerRestoreComplete)
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

            if (triggerRestoreComplete)
            {
                _namedDependencyProvider.Trigger(project.Name + "_RestoreComplete");
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

            var sourcesProjectWideSources = project.SourceFiles.ToList();

            foreach (var frameworkName in frameworks)
            {
                var dependencyInfo = ResolveProjectDepencies(project, configuration, frameworkName);
                var dependencySources = new List<string>(sourcesProjectWideSources);

                var frameworkResolver = dependencyInfo.HostContext.FrameworkReferenceResolver;

                var frameworkData = new FrameworkData
                {
                    ShortName = VersionUtility.GetShortFrameworkName(frameworkName),
                    FrameworkName = frameworkName.ToString(),
                    FriendlyName = frameworkResolver.GetFriendlyFrameworkName(frameworkName),
                    RedistListPath = frameworkResolver.GetFrameworkRedistListPath(frameworkName)
                };

                state.Frameworks.Add(frameworkData);

                // Add shared files
                foreach (var reference in dependencyInfo.ProjectReferences)
                {
                    Project referencedProject;
                    if (Project.TryGetProject(reference.Path, out referencedProject))
                    {
                        dependencySources.AddRange(referencedProject.SharedFiles);
                    }
                }

                var projectInfo = new ProjectInfo()
                {
                    Path = appPath,
                    Configuration = configuration,
                    TargetFramework = frameworkData,
                    FrameworkName = frameworkName,
                    // TODO: This shouldn't be roslyn specific compilation options
                    CompilationSettings = project.GetCompilationSettings(frameworkName, configuration),
                    SourceFiles = dependencySources,
                    DependencyInfo = dependencyInfo
                };

                state.Projects.Add(projectInfo);

                if (state.ProjectSearchPaths == null)
                {
                    state.ProjectSearchPaths = dependencyInfo.HostContext.ProjectResolver.SearchPaths.ToList();
                }

                if (state.GlobalJsonPath == null)
                {
                    GlobalSettings settings;
                    if (GlobalSettings.TryGetGlobalSettings(dependencyInfo.HostContext.RootDirectory, out settings))
                    {
                        state.GlobalJsonPath = settings.FilePath;
                    }
                }
            }

            return state;
        }

        private ApplicationHostContext GetApplicationHostContext(Project project, string configuration, FrameworkName frameworkName, bool useRuntimeLoadContextFactory = true)
        {
            var cacheKey = Tuple.Create("ApplicationContext", project.Name, configuration, frameworkName);

            return _cache.Get<ApplicationHostContext>(cacheKey, ctx =>
            {
                var applicationHostContext = new ApplicationHostContext(_hostServices,
                                                                        project.ProjectDirectory,
                                                                        packagesDirectory: null,
                                                                        configuration: configuration,
                                                                        targetFramework: frameworkName,
                                                                        cache: _cache,
                                                                        cacheContextAccessor: _cacheContextAccessor,
                                                                        namedCacheDependencyProvider: _namedDependencyProvider,
                                                                        loadContextFactory: GetRuntimeLoadContextFactory(project));

                applicationHostContext.DependencyWalker.Walk(project.Name, project.Version, frameworkName);

                // Watch all projects for project.json changes
                foreach (var library in applicationHostContext.DependencyWalker.Libraries)
                {
                    if (string.Equals(library.Type, "Project"))
                    {
                        ctx.Monitor(new FileWriteTimeCacheDependency(library.Path));
                    }
                }

                // Add a cache dependency on restore complete to reevaluate dependencies
                ctx.Monitor(_namedDependencyProvider.GetNamedDependency(project.Name + "_RestoreComplete"));

                return applicationHostContext;
            });
        }

        private IAssemblyLoadContextFactory GetRuntimeLoadContextFactory(Project project)
        {
            var applicationHostContext = new ApplicationHostContext(_hostServices,
                                                                    project.ProjectDirectory,
                                                                    packagesDirectory: null,
                                                                    configuration: _appEnv.Configuration,
                                                                    targetFramework: _appEnv.RuntimeFramework,
                                                                    cache: _cache,
                                                                    cacheContextAccessor: _cacheContextAccessor,
                                                                    namedCacheDependencyProvider: _namedDependencyProvider);

            applicationHostContext.DependencyWalker.Walk(project.Name, project.Version, _appEnv.RuntimeFramework);

            return new AssemblyLoadContextFactory(applicationHostContext.ServiceProvider);
        }

        private DependencyInfo ResolveProjectDepencies(Project project, string configuration, FrameworkName frameworkName)
        {
            var cacheKey = Tuple.Create("DependencyInfo", project.Name, configuration, frameworkName);

            return _cache.Get<DependencyInfo>(cacheKey, ctx =>
            {
                var applicationHostContext = GetApplicationHostContext(project, configuration, frameworkName);

                var libraryManager = applicationHostContext.LibraryManager;
                var frameworkResolver = applicationHostContext.FrameworkReferenceResolver;

                var info = new DependencyInfo
                {
                    Dependencies = new Dictionary<string, DependencyDescription>(),
                    ProjectReferences = new List<ProjectReference>(),
                    HostContext = applicationHostContext,
                    References = new List<string>(),
                    RawReferences = new Dictionary<string, byte[]>()
                };

                foreach (var library in applicationHostContext.DependencyWalker.Libraries)
                {
                    var description = CreateDependencyDescription(library);
                    info.Dependencies[description.Name] = description;

                    // Skip unresolved libraries
                    if (!library.Resolved)
                    {
                        continue;
                    }

                    if (string.Equals(library.Type, "Project") &&
                       !string.Equals(library.Identity.Name, project.Name))
                    {
                        Project referencedProject;
                        if (!Project.TryGetProject(library.Path, out referencedProject))
                        {
                            // Should never happen
                            continue;
                        }

                        var targetFrameworkInformation = referencedProject.GetTargetFramework(library.Framework);

                        // If this is an assembly reference then treat it like a file reference
                        if (!string.IsNullOrEmpty(targetFrameworkInformation.AssemblyPath) &&
                            string.IsNullOrEmpty(targetFrameworkInformation.WrappedProject))
                        {
                            string assemblyPath = GetProjectRelativeFullPath(referencedProject, targetFrameworkInformation.AssemblyPath);
                            info.References.Add(assemblyPath);

                            description.Path = assemblyPath;
                            description.Type = "Assembly";
                        }
                        else
                        {
                            string wrappedProjectPath = null;

                            if (!string.IsNullOrEmpty(targetFrameworkInformation.WrappedProject))
                            {
                                wrappedProjectPath = GetProjectRelativeFullPath(referencedProject, targetFrameworkInformation.WrappedProject);
                            }

                            info.ProjectReferences.Add(new ProjectReference
                            {
                                Framework = new FrameworkData
                                {
                                    ShortName = VersionUtility.GetShortFrameworkName(library.Framework),
                                    FrameworkName = library.Framework.ToString(),
                                    FriendlyName = frameworkResolver.GetFriendlyFrameworkName(library.Framework)
                                },
                                Path = library.Path,
                                WrappedProjectPath = wrappedProjectPath
                            });
                        }
                    }
                }

                var exportWithoutProjects = ProjectExportProviderHelper.GetExportsRecursive(
                     _cache,
                     applicationHostContext.LibraryManager,
                     applicationHostContext.LibraryExportProvider,
                     new LibraryKey
                     {
                         Configuration = configuration,
                         TargetFramework = frameworkName,
                         Name = project.Name
                     },
                     library => library.Type != "Project");

                foreach (var reference in exportWithoutProjects.MetadataReferences)
                {
                    var fileReference = reference as IMetadataFileReference;
                    if (fileReference != null)
                    {
                        info.References.Add(fileReference.Path);
                    }

                    var embedded = reference as IMetadataEmbeddedReference;
                    if (embedded != null)
                    {
                        info.RawReferences[embedded.Name] = embedded.Contents;
                    }
                }

                return info;
            });
        }

        private static string GetProjectRelativeFullPath(Project referencedProject, string path)
        {
            return Path.GetFullPath(Path.Combine(referencedProject.ProjectDirectory, path));
        }

        private static DependencyDescription CreateDependencyDescription(LibraryDescription library)
        {
            return new DependencyDescription
            {
                Name = library.Identity.Name,
                Version = library.Identity.Version == null ? null : library.Identity.Version.ToString(),
                Type = library.Resolved ? library.Type : "Unresolved",
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

            public IList<string> SourceFiles { get; set; }

            public DependencyInfo DependencyInfo { get; set; }
        }

        private class DependencyInfo
        {
            public ApplicationHostContext HostContext { get; set; }

            public IDictionary<string, byte[]> RawReferences { get; set; }

            public IDictionary<string, DependencyDescription> Dependencies { get; set; }

            public IList<string> References { get; set; }

            public IList<ProjectReference> ProjectReferences { get; set; }
        }

        private class ProjectCompilation
        {
            public ILibraryExport Export { get; set; }
            public IMetadataProjectReference ProjectReference { get; set; }
            public IDictionary<string, byte[]> EmbeddedReferences { get; set; }

            public IList<string> Warnings { get; set; }
            public IList<string> Errors { get; set; }

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
        }

        private class RemoteLibraryKey
        {
            public string Name { get; set; }
            public string TargetFramework { get; set; }
            public string Configuration { get; set; }
            public string Aspect { get; set; }
        }

        private class LibraryKey : ILibraryKey
        {
            public string Name { get; set; }
            public FrameworkName TargetFramework { get; set; }
            public string Configuration { get; set; }
            public string Aspect { get; set; }
        }

        private struct Void
        {
        }
    }
}