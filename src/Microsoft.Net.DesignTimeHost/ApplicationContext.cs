using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Runtime.Versioning;
using System.Threading;
using Communication;
using Microsoft.Net.DesignTimeHost.Models;
using Microsoft.Net.Runtime;
using Microsoft.Net.Runtime.FileSystem;
using Microsoft.Net.Runtime.Loader;
using Microsoft.Net.Runtime.Loader.MSBuildProject;
using Microsoft.Net.Runtime.Loader.NuGet;
using Microsoft.Net.Runtime.Loader.Roslyn;
using Microsoft.Net.Runtime.Roslyn;
using NuGet;

namespace Microsoft.Net.DesignTimeHost
{
    public class ApplicationContext
    {
        private class State
        {
            public RoslynAssemblyLoader Compiler { get; set; }
            public AssemblyLoader CompositeLoader { get; set; }
            public FrameworkName TargetFramework { get; set; }
        }

        private readonly Queue<Message> _inbox = new Queue<Message>();
        private readonly object _processingLock = new object();

        private FrameworkName _changeTargetFramework;
        private DateTimeOffset _lastRemoteHeartbeat;

        private State _state;
        private World _remote = new World();
        private World _local = new World();

        public ApplicationContext(int id)
        {
            Id = id;
            CheckList = new CheckList();
        }

        public Project Project
        {
            get
            {
                Project project;
                Project.TryGetProject(Path, out project);
                return project;
            }
        }

        public CheckList CheckList { get; private set; }
        public int Id { get; private set; }
        public string Path { get; private set; }

        public void OnMessage(Message message)
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
                        OnInitialize(message.Payload.ToObject<IncomingMessages.InitializeData>());
                    }
                    break;
                case "Heartbeat":
                    {
                        OnHeartbeat();
                    }
                    break;
                case "Teardown":
                    {
                        OnTeardown();
                    }
                    break;
                case "ChangeTargetFramework":
                    {
                        OnChangeTargetFramework(message.Payload.ToObject<IncomingMessages.TargetFrameworkData>());
                    }
                    break;
                case "FilesChanged":
                    {
                        OnFilesChanged();
                    }
                    break;
            }
        }

        private void OnInitialize(IncomingMessages.InitializeData message)
        {
            Path = message.ProjectFolder;
            _changeTargetFramework = new FrameworkName(message.TargetFramework);
        }

        private void OnHeartbeat()
        {
            _lastRemoteHeartbeat = DateTimeOffset.UtcNow;
        }

        private void OnTeardown()
        {
        }

        private void OnChangeTargetFramework(IncomingMessages.TargetFrameworkData message)
        {
            _changeTargetFramework = new FrameworkName(message.TargetFramework);
        }

        private void OnFilesChanged()
        {
        }



        public RoslynProjectMetadata GetProjectMetadata()
        {
            return _compiler.GetProjectMetadata(Project.Name, _targetFramework);
        }

        public void Initialize(FrameworkName targetFramework)
        {
            _targetFramework = targetFramework;

            string projectDir = Project.ProjectDirectory;

            var globalAssemblyCache = new DefaultGlobalAssemblyCache();

            _compositeLoader = new AssemblyLoader();
            string rootDirectory = DefaultHost.ResolveRootDirectory(projectDir);
            var resolver = new FrameworkReferenceResolver(globalAssemblyCache);
            var resourceProvider = new ResxResourceProvider();
            var projectResolver = new ProjectResolver(projectDir, rootDirectory);
            _compiler = new RoslynAssemblyLoader(projectResolver, NoopWatcher.Instance, resolver, globalAssemblyCache, _compositeLoader, resourceProvider);
            _compositeLoader.Add(_compiler);
            _compositeLoader.Add(new MSBuildProjectAssemblyLoader(rootDirectory, NoopWatcher.Instance));
            _compositeLoader.Add(new NuGetAssemblyLoader(projectDir));

            _compositeLoader.Walk(Project.Name, Project.Version, _targetFramework);
        }

        public void RefreshDepedencies()
        {
            _compositeLoader.Walk(Project.Name, Project.Version, _targetFramework);
        }
    }
}