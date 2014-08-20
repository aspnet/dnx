// Copyright (c) Microsoft Open Technologies, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;
using Microsoft.Framework.Runtime;

namespace Microsoft.Framework.DesignTimeHost
{
    public class Program
    {
        private readonly IServiceProvider _services;

        public Program(IServiceProvider services)
        {
            _services = services;
        }

        public void Main(string[] args)
        {
            // Expect: port, host processid, hostID string
            if (args.Length < 3)
            {
                Console.WriteLine("Invalid command line arguments");
                return;
            }
            int port = Int32.Parse(args[0]);
            int hostPID = Int32.Parse(args[1]);

            // Add a watch to the host PID. If it goes away we will self terminate
            var hostProcess = Process.GetProcessById(hostPID);
            hostProcess.EnableRaisingEvents = true;
            hostProcess.Exited += (s, e) =>
            {
                Process.GetCurrentProcess().Kill();
            };

            string hostId = args[2];

            OpenChannel(port, hostId).Wait();
        }

        private async Task OpenChannel(int port, string hostId)
        {
            var cacheContextAccessor = new CacheContextAccessor();
            var cache = new Cache(cacheContextAccessor);
            var contexts = new Dictionary<int, ApplicationContext>();

            // This fixes the mono incompatibility but ties it to ipv4 connections
            var listenSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            listenSocket.Bind(new IPEndPoint(IPAddress.Loopback, port));
            listenSocket.Listen(10);

            Console.WriteLine("Listening on port {0}", port);

            for (; ;)
            {
                var acceptSocket = await AcceptAsync(listenSocket);

                Console.WriteLine("Client accepted {0}", acceptSocket.LocalEndPoint);

                var stream = new NetworkStream(acceptSocket);
                var queue = new ProcessingQueue(stream);
                var connection = new ConnectionContext(contexts, _services, cache, cacheContextAccessor, queue, hostId);

                queue.OnReceive += message =>
                {
                    // Enumerates all project contexts and return them to the
                    // sender
                    if (message.MessageType == "EnumerateProjectContexts")
                    {
                        WriteProjectContexts(queue, contexts);
                    }
                    else
                    {
                        // Otherwise it's a context specific message
                        connection.OnReceive(message);
                    }
                };

                queue.Start();
            }
        }

        private static Task<Socket> AcceptAsync(Socket socket)
        {
            return Task.Factory.FromAsync((cb, state) => socket.BeginAccept(cb, state), ar => socket.EndAccept(ar), null);
        }

        private static void WriteProjectContexts(ProcessingQueue queue, IDictionary<int, ApplicationContext> contexts)
        {
            var projects = contexts.Values.Select(p => new
            {
                Id = p.Id,
                ProjectPath = p.ApplicationPath
            })
            .ToList();

            queue.Send(writer =>
            {
                writer.Write("ProjectContexts");
                writer.Write(projects.Count);
                for (int i = 0; i < projects.Count; i++)
                {
                    writer.Write(projects[i].ProjectPath);
                    writer.Write(projects[i].Id);
                }
            });
        }
    }
}