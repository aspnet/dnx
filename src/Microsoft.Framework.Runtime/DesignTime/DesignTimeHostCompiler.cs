using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Microsoft.Framework.Runtime
{
    public class DesignTimeHostCompiler : IDesignTimeHostCompiler
    {
        private readonly ProcessingQueue _queue;
        private readonly ConcurrentDictionary<string, TaskCompletionSource<CompileResponse>> _cache = new ConcurrentDictionary<string, TaskCompletionSource<CompileResponse>>();

        public DesignTimeHostCompiler(Stream stream)
        {
            _queue = new ProcessingQueue(stream);
            _queue.OnReceive += OnMessage;
            _queue.Start();
        }

        public Task<CompileResponse> Compile(CompileRequest request)
        {
            _queue.Post(new Message
            {
                HostId = "Application",
                MessageType = "Compile",
                Payload = JToken.FromObject(request)
            });

            var key = request.ProjectPath + request.TargetFramework + request.Configuration;

            return _cache.GetOrAdd(key, _ => new TaskCompletionSource<CompileResponse>()).Task;
        }

        private void OnMessage(CompileResponse response)
        {
            var key = response.ProjectPath + response.TargetFramework + response.Configuration;

            _cache.AddOrUpdate(key,
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

        private class ProcessingQueue
        {
            private readonly BinaryReader _reader;
            private readonly BinaryWriter _writer;

            public event Action<CompileResponse> OnReceive;

            public ProcessingQueue(Stream stream)
            {
                _reader = new BinaryReader(stream);
                _writer = new BinaryWriter(stream);
            }

            public void Start()
            {
                new Thread(ReceiveMessages).Start();
            }

            public void Post(Message message)
            {
                lock (_writer)
                {
                    _writer.Write(JsonConvert.SerializeObject(message));
                }
            }

            private void ReceiveMessages()
            {
                try
                {
                    while (true)
                    {
                        var messageType = _reader.ReadString();

                        if (messageType == "Compile")
                        {
                            var compileResponse = new CompileResponse();
                            compileResponse.Name = _reader.ReadString();
                            compileResponse.ProjectPath = _reader.ReadString();
                            compileResponse.Configuration = _reader.ReadString();
                            compileResponse.TargetFramework = _reader.ReadString();
                            var warningsCount = _reader.ReadInt32();
                            compileResponse.Warnings = new string[warningsCount];
                            for (int i = 0; i < warningsCount; i++)
                            {
                                compileResponse.Warnings[i] = _reader.ReadString();
                            }

                            var errorsCount = _reader.ReadInt32();
                            compileResponse.Errors = new string[errorsCount];
                            for (int i = 0; i < errorsCount; i++)
                            {
                                compileResponse.Errors[i] = _reader.ReadString();
                            }

                            var embeddedReferencesCount = _reader.ReadInt32();
                            compileResponse.EmbeddedReferences = new Dictionary<string, byte[]>();
                            for (int i = 0; i < embeddedReferencesCount; i++)
                            {
                                var key = _reader.ReadString();
                                int valueLength = _reader.ReadInt32();
                                var value = _reader.ReadBytes(valueLength);
                                compileResponse.EmbeddedReferences[key] = value;
                            }

                            var assemblyBytesLength = _reader.ReadInt32();
                            compileResponse.AssemblyBytes = _reader.ReadBytes(assemblyBytesLength);
                            var pdbBytesLength = _reader.ReadInt32();
                            compileResponse.PdbBytes = _reader.ReadBytes(pdbBytesLength);

                            OnReceive(compileResponse);
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex.Message);
                }
            }
        }
        public class Message
        {
            public string HostId { get; set; }
            public string MessageType { get; set; }
            public int ContextId { get; set; }
            public JToken Payload { get; set; }

            public override string ToString()
            {
                return "(" + HostId + ", " + MessageType + ", " + ContextId + ") -> " + (Payload == null ? "null" : Payload.ToString(Formatting.Indented));
            }
        }
    }
}