using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace Microsoft.Framework.Runtime
{
    public class DesignTimeHostCompiler : IDesignTimeHostCompiler
    {
        private readonly ProcessingQueue _queue;
        private readonly ConcurrentDictionary<int, TaskCompletionSource<CompileResponse>> _cache = new ConcurrentDictionary<int, TaskCompletionSource<CompileResponse>>();
        private readonly TaskCompletionSource<Dictionary<string, int>> _projectContexts = new TaskCompletionSource<Dictionary<string, int>>();

        public DesignTimeHostCompiler(IApplicationShutdown shutdown, Stream stream)
        {
            _queue = new ProcessingQueue(stream);
            _queue.ProjectCompiled += OnProjectCompiled;
            _queue.ProjectsInitialized += ProjectContextsInitialized;
            _queue.ProjectChanged += _ => shutdown.RequestShutdown();
            _queue.Start();

            _queue.Send(new DesignTimeMessage
            {
                HostId = "Application",
                MessageType = "EnumerateProjectContexts"
            });
        }

        public async Task<CompileResponse> Compile(string projectPath)
        {
            var contexts = await _projectContexts.Task;

            int contextId;
            if (!contexts.TryGetValue(projectPath, out contextId))
            {
                // This should never happen
                throw new InvalidOperationException();
            }

            _queue.Send(new DesignTimeMessage
            {
                HostId = "Application",
                MessageType = "GetCompiledAssembly",
                ContextId = contextId
            });

            return await _cache.GetOrAdd(contextId, _ => new TaskCompletionSource<CompileResponse>()).Task;
        }

        private void ProjectContextsInitialized(Dictionary<string, int> projectContexts)
        {
            _projectContexts.TrySetResult(projectContexts);
        }

        private void OnProjectCompiled(int contextId, CompileResponse response)
        {
            _cache.AddOrUpdate(contextId,
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