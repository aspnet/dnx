using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Framework.Runtime.Compilation;
using Newtonsoft.Json.Linq;

namespace Microsoft.Framework.Runtime
{
    public class DesignTimeHostCompiler : IDesignTimeHostCompiler
    {
        private readonly ProcessingQueue _queue;
        private readonly IApplicationShutdown _shutdown;
        private readonly ConcurrentDictionary<int, TaskCompletionSource<CompileResponse>> _compileResponses = new ConcurrentDictionary<int, TaskCompletionSource<CompileResponse>>();
        private readonly TaskCompletionSource<Dictionary<string, int>> _projectContexts = new TaskCompletionSource<Dictionary<string, int>>();

        public DesignTimeHostCompiler(IApplicationShutdown shutdown, IFileWatcher watcher, Stream stream)
        {
            _shutdown = shutdown;
            _queue = new ProcessingQueue(stream);
            _queue.ProjectCompiled += OnProjectCompiled;
            _queue.ProjectsInitialized += ProjectContextsInitialized;
            _queue.ProjectChanged += _ => { };
            _queue.ProjectSources += files =>
            {
                foreach (var file in files)
                {
                    watcher.WatchFile(file);
                }
            };
            _queue.Error += OnError;

            _queue.Closed += OnClosed;
            _queue.Start();

            var obj = new JObject();
            obj["Version"] = 1;

            _queue.Send(new DesignTimeMessage
            {
                HostId = "Application",
                MessageType = "EnumerateProjectContexts",
                Payload = obj
            });
        }

        private void OnError(int? contextId, string error)
        {
            var exception = new InvalidOperationException(error);
            if (contextId == null || contextId == -1)
            {
                _projectContexts.TrySetException(exception);
                _shutdown.RequestShutdown();
            }
            else
            {
                _compileResponses.AddOrUpdate(contextId.Value,
                _ =>
                {
                    var tcs = new TaskCompletionSource<CompileResponse>();
                    tcs.SetException(exception);
                    return tcs;
                },
                (_, existing) =>
                {
                    if (!existing.TrySetException(exception))
                    {
                        var tcs = new TaskCompletionSource<CompileResponse>();
                        tcs.TrySetException(exception);
                        return tcs;
                    }

                    return existing;
                });
            }
        }

        public async Task<CompileResponse> Compile(string projectPath, ILibraryKey library)
        {
            var contexts = await _projectContexts.Task;

            int contextId;
            if (!contexts.TryGetValue(projectPath, out contextId))
            {
                // This should never happen
                throw new InvalidOperationException();
            }

            var obj = new JObject();
            obj["Name"] = library.Name;
            obj["Configuration"] = library.Configuration;
            obj["TargetFramework"] = library.TargetFramework.ToString();
            obj["Aspect"] = library.Aspect;
            obj["Version"] = 1;

            _queue.Send(new DesignTimeMessage
            {
                HostId = "Application",
                MessageType = "GetCompiledAssembly",
                Payload = obj,
                ContextId = contextId
            });

            return await _compileResponses.GetOrAdd(contextId, _ => new TaskCompletionSource<CompileResponse>()).Task;
        }

        private void OnClosed()
        {
            // Cancel all pending responses
            foreach (var q in _compileResponses)
            {
                q.Value.TrySetCanceled();
            }
        }

        private void ProjectContextsInitialized(Dictionary<string, int> projectContexts)
        {
            _projectContexts.TrySetResult(projectContexts);
        }

        private void OnProjectCompiled(int contextId, CompileResponse response)
        {
            _compileResponses.AddOrUpdate(contextId,
                _ =>
                {
                    var tcs = new TaskCompletionSource<CompileResponse>();
                    tcs.SetResult(response);
                    return tcs;
                },
                (_, existing) =>
                {
                    if (!existing.TrySetResult(response))
                    {
                        var tcs = new TaskCompletionSource<CompileResponse>();
                        tcs.SetResult(response);
                        return tcs;
                    }

                    return existing;
                });
        }
    }
}