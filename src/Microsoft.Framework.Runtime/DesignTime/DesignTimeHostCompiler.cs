using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

namespace Microsoft.Framework.Runtime
{
    public class DesignTimeHostCompiler : IDesignTimeHostCompiler
    {
        private readonly ProcessingQueue _queue;
        private readonly ConcurrentDictionary<int, TaskCompletionSource<CompileResponse>> _compileResponses = new ConcurrentDictionary<int, TaskCompletionSource<CompileResponse>>();
        private readonly TaskCompletionSource<Dictionary<string, int>> _projectContexts = new TaskCompletionSource<Dictionary<string, int>>();

        public DesignTimeHostCompiler(IApplicationShutdown shutdown, IFileWatcher watcher, Stream stream)
        {
            _queue = new ProcessingQueue(stream);
            _queue.ProjectCompiled += OnProjectCompiled;
            _queue.ProjectsInitialized += ProjectContextsInitialized;
            _queue.ProjectChanged += _ => shutdown.RequestShutdown();
            _queue.ProjectSources += files =>
            {
                foreach (var file in files)
                {
                    watcher.WatchFile(file);
                }
            };

            _queue.Closed += OnClosed;
            _queue.Start();

            _queue.Send(new DesignTimeMessage
            {
                HostId = "Application",
                MessageType = "EnumerateProjectContexts"
            });
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