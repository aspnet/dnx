#if NET45 // NETWORKING
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Communication;
using Microsoft.Net.Runtime.Roslyn;
using NuGet;

namespace Microsoft.Net.DesignTimeHost
{
    public class Program
    {
        private readonly ConcurrentDictionary<int, ApplicationContext> _contexts = new ConcurrentDictionary<int, ApplicationContext>();
        private Timer _update;
        private Timer _compute;
        private ProcessingQueue _queue;

        private readonly List<Result> _results = new List<Result>();

        public void Main(string[] args)
        {
            int port = args.Length == 0 ? 1334 : Int32.Parse(args[0]);

            OpenChannel(port).Wait();
        }

        private async Task OpenChannel(int port)
        {
            var socket = new Socket(SocketType.Stream, ProtocolType.Tcp);
            socket.Bind(new IPEndPoint(IPAddress.Loopback, port));
            socket.Listen(10);

            Console.WriteLine("Listening on port {0}", port);

            var client = await AcceptAsync(socket);

            Console.WriteLine("Client accepted {0}", client.LocalEndPoint);

            var connection = new ConnectionContext(client);

            connection.Start();

            var ns = new NetworkStream(client);

            _queue = new ProcessingQueue(ns);

            _queue.OnMessage += OnMessage;

            _queue.Start();

            _compute = new Timer(Compute, null, TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(1));

            _update = new Timer(Update, null, TimeSpan.FromMilliseconds(500), TimeSpan.FromMilliseconds(500));

            Console.ReadLine();
        }

        private void Compute(object state)
        {
            foreach (var ctx in _contexts.Values)
            {
                RoslynProjectMetadata metadata = null;

                if (Interlocked.CompareExchange(ref ctx.CheckList.NeedHeartbeat, 0, 1) == 1)
                {
                    Console.WriteLine("NeedHeartbeat");

                    lock (_results)
                    {
                        _results.Add(new Result
                        {
                            ContextId = ctx.Id,
                            MessageType = OutgoingMessages.MessageType.Heartbeat
                        });
                    }
                }

                if (Interlocked.CompareExchange(ref ctx.CheckList.NeedReferences, 0, 1) == 1)
                {
                    Console.WriteLine("NeedReferences");

                    ctx.RefreshDepedencies();

                    metadata = ctx.GetProjectMetadata();

                    object result = new OutgoingMessages.ReferenceData
                    {
                        ProjectReferences = metadata.ProjectReferences,
                        FileReferences = metadata.References
                    };

                    object configs = ctx.Project.GetTargetFrameworkConfigurations().Select(c => new
                    {
                        CompliationOptions = ctx.Project.GetConfiguration(c.FrameworkName).Value["compilationOptions"],
                        FrameworkName = VersionUtility.GetShortFrameworkName(c.FrameworkName),
                    });

                    lock (_results)
                    {
                        _results.Add(new Result
                        {
                            ContextId = ctx.Id,
                            MessageType = OutgoingMessages.MessageType.References,
                            Value = result
                        });

                        _results.Add(new Result
                        {
                            ContextId = ctx.Id,
                            MessageType = OutgoingMessages.MessageType.CompilationSettings,
                            Value = configs
                        });
                    }
                }

                if (Interlocked.CompareExchange(ref ctx.CheckList.NeedCompilerSettings, 0, 1) == 1)
                {
                    Console.WriteLine("NeedCompilerSettings");

                    object configs = ctx.Project.GetTargetFrameworkConfigurations().Select(c => new
                    {
                        CompliationOptions = ctx.Project.GetConfiguration(c.FrameworkName).Value["compilationOptions"],
                        FrameworkName = VersionUtility.GetShortFrameworkName(c.FrameworkName),
                    });

                    lock (_results)
                    {
                        _results.Add(new Result
                        {
                            ContextId = ctx.Id,
                            MessageType = OutgoingMessages.MessageType.References,
                            Value = configs
                        });

                    }
                }

                if (Interlocked.CompareExchange(ref ctx.CheckList.NeedWarningsAndErrors, 0, 1) == 1)
                {
                    Console.WriteLine("NeedWarningsAndErrors");

                    var data = metadata ?? ctx.GetProjectMetadata();

                    object result = new OutgoingMessages.DiagnosticData
                    {
                        Warnings = data.Warnings,
                        Errors = data.Errors
                    };

                    lock (_results)
                    {
                        _results.Add(new Result
                        {
                            ContextId = ctx.Id,
                            MessageType = OutgoingMessages.MessageType.WarningsAndErrors,
                            Value = result
                        });
                    }
                }
            }
        }

        private void Update(object state)
        {
            lock (_results)
            {
                foreach (var result in _results)
                {
                    _queue.Post(result.ContextId, (int)result.MessageType, result.Value);
                }

                _results.Clear();
            }
        }

        private void OnMessage(List<Message> messages)
        {
            foreach (var m in messages)
            {
                Console.WriteLine(m);

                ApplicationContext ctx;
                _contexts.TryGetValue(m.ContextId, out ctx);

                switch ((IncomingMessages.MessageType)m.MessageType)
                {
                    case IncomingMessages.MessageType.Initialize:
                        CreateContext(m);
                        break;
                    case IncomingMessages.MessageType.Teardown:
                        ApplicationContext dummy;
                        _contexts.TryRemove(m.ContextId, out dummy);
                        break;
                    case IncomingMessages.MessageType.ChangeTargetFramework:
                        var data = m.Payload.ToObject<IncomingMessages.TargetFrameworkData>();
                        var targetFramework = VersionUtility.ParseFrameworkName(data.TargetFramework);
                        ctx.Initialize(targetFramework);
                        break;
                    case IncomingMessages.MessageType.Heartbeat:
                        Interlocked.CompareExchange(ref ctx.CheckList.NeedHeartbeat, 1, 0);
                        break;
                    case IncomingMessages.MessageType.References:
                        Interlocked.CompareExchange(ref ctx.CheckList.NeedReferences, 1, 0);
                        break;
                    case IncomingMessages.MessageType.CompilerSettings:
                        Interlocked.CompareExchange(ref ctx.CheckList.NeedCompilerSettings, 1, 0);
                        break;
                    case IncomingMessages.MessageType.WarningsAndErrors:
                        Interlocked.CompareExchange(ref ctx.CheckList.NeedWarningsAndErrors, 1, 0);
                        break;
                }
            }
        }

        private void CreateContext(Message message)
        {
            var initData = message.Payload.ToObject<IncomingMessages.InitializeData>();
            var context = new ApplicationContext(message.ContextId, initData.ProjectFolder);
            var targetFramework = VersionUtility.ParseFrameworkName(initData.TargetFramework ?? "net45");

            context.Initialize(targetFramework);

            Interlocked.CompareExchange(ref context.CheckList.NeedCompilerSettings, 1, 0);

            _contexts.TryAdd(message.ContextId, context);
        }

        private static Task<Socket> AcceptAsync(Socket socket)
        {
            return Task.Factory.FromAsync((cb, state) => socket.BeginAccept(cb, state), ar => socket.EndAccept(ar), null);
        }
    }

    public class IncomingMessages
    {
        public class InitializeData
        {
            public string TargetFramework { get; set; }
            public string ProjectFolder { get; set; }
        }

        public class TargetFrameworkData
        {
            public string TargetFramework { get; set; }
        }
    }

    public class OutgoingMessages
    {
        public class DiagnosticData
        {
            public IList<string> Warnings { get; set; }
            public IList<string> Errors { get; set; }
        }

        public class ReferenceData
        {
            public IList<string> ProjectReferences { get; set; }
            public IList<string> FileReferences { get; set; }
        }

        public enum MessageType
        {
            Heartbeat = 0,
            CompilationSettings = 1,
            References = 2,
            WarningsAndErrors = 3
        }
    }

    public class Result
    {
        public int ContextId { get; set; }
        public OutgoingMessages.MessageType MessageType { get; set; }
        public object Value { get; set; }
    }

    public class CheckList
    {
        public int NeedCompilerSettings;
        public int NeedReferences;
        public int NeedWarningsAndErrors;
        public int NeedHeartbeat;
    }
}
#endif