#if NET45 // NETWORKING
using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;
using Microsoft.Net.Runtime;

namespace Microsoft.Net.DesignTimeHost
{
    public class Program
    {
        private IAssemblyLoaderEngine _loaderEngine;
        private readonly ConcurrentDictionary<int, ApplicationContext> _contexts = new ConcurrentDictionary<int, ApplicationContext>();

        public Program(IAssemblyLoaderEngine loaderEngine)
        {
            _loaderEngine = loaderEngine;
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
                Environment.Exit(0);
            };

            string hostId = args[2];

            OpenChannel(port, hostId).Wait();
        }

        private async Task OpenChannel(int port, string hostId)
        {
            var listenSocket = new Socket(SocketType.Stream, ProtocolType.Tcp);
            listenSocket.Bind(new IPEndPoint(IPAddress.Loopback, port));
            listenSocket.Listen(10);

            Console.WriteLine("Listening on port {0}", port);

            for (; ; )
            {
                var acceptSocket = await AcceptAsync(listenSocket);

                Console.WriteLine("Client accepted {0}", acceptSocket.LocalEndPoint);

                var connection = new ConnectionContext(_loaderEngine, new NetworkStream(acceptSocket), hostId);

                connection.Start();
            }
        }


        private static Task<Socket> AcceptAsync(Socket socket)
        {
            return Task.Factory.FromAsync((cb, state) => socket.BeginAccept(cb, state), ar => socket.EndAccept(ar), null);
        }

    }
}
#endif