// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;
using Microsoft.Dnx.Compilation;
using Microsoft.Dnx.Compilation.Caching;
using Microsoft.Dnx.DesignTimeHost.Models;
using Microsoft.Dnx.Runtime;
using Microsoft.Dnx.Runtime.Common.DependencyInjection;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Microsoft.Dnx.DesignTimeHost
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

            // In mono 3.10, the Exited event fires immediately, so the
            // caller will need to terminate this process.
            if (!RuntimeEnvironmentHelper.IsMono)
            {
                // Add a watch to the host PID. If it goes away we will self terminate.
                var hostProcess = Process.GetProcessById(hostPID);
                hostProcess.EnableRaisingEvents = true;
                hostProcess.Exited += (s, e) =>
                {
                    Process.GetCurrentProcess().Kill();
                };
            }

            string hostId = args[2];

            OpenChannel(port, hostId).Wait();
        }

        private async Task OpenChannel(int port, string hostId)
        {
            var contexts = new Dictionary<int, ApplicationContext>();
            var services = new ServiceProvider(_services);
            var protocolManager = new ProtocolManager(maxVersion: 2);

            // This fixes the mono incompatibility but ties it to ipv4 connections
            var listenSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            listenSocket.Bind(new IPEndPoint(IPAddress.Loopback, port));
            listenSocket.Listen(10);

            Console.WriteLine($"Process ID {Process.GetCurrentProcess().Id}");
            Console.WriteLine("Listening on port {0}", port);

            for (; ;)
            {
                var acceptSocket = await AcceptAsync(listenSocket);

                Console.WriteLine("Client accepted {0}", acceptSocket.LocalEndPoint);

                var stream = new NetworkStream(acceptSocket);
                var queue = new ProcessingQueue(stream);
                var connection = new ConnectionContext(
                    contexts,
                    services,
                    queue,
                    protocolManager,
                    hostId);

                queue.OnReceive += message =>
                {
                    // Enumerates all project contexts and return them to the
                    // sender
                    if (message.MessageType == "EnumerateProjectContexts")
                    {
                        WriteProjectContexts(message, queue, contexts);
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

        private static void WriteProjectContexts(Message message, ProcessingQueue queue, IDictionary<int, ApplicationContext> contexts)
        {
            try
            {
                var projectContexts = contexts.Values.Select(p => new
                {
                    Id = p.Id,
                    ProjectPath = p.ApplicationPath
                })
                .ToList();

                var versionToken = message.Payload.HasValues ? message.Payload?["Version"] : null;
                var version = versionToken != null ? versionToken.Value<int>() : 0;

                queue.Send(writer =>
                {
                    if (version == 0)
                    {
                        writer.Write("ProjectContexts");
                        writer.Write(projectContexts.Count);
                        for (int i = 0; i < projectContexts.Count; i++)
                        {
                            writer.Write(projectContexts[i].ProjectPath);
                            writer.Write(projectContexts[i].Id);
                        }
                    }
                    else
                    {
                        var obj = new JObject();
                        obj["MessageType"] = "ProjectContexts";
                        var projects = new JObject();
                        obj["Projects"] = projects;

                        foreach (var pair in projectContexts)
                        {
                            projects[pair.ProjectPath] = pair.Id;
                        }

                        writer.Write(obj.ToString(Formatting.None));
                    }
                });
            }
            catch (Exception ex)
            {
                var error = new JObject();
                error["Message"] = ex.Message;

                queue.Send(new Message
                {
                    MessageType = "Error",
                    Payload = error
                });

                throw;
            }
        }
    }
}