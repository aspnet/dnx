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

        public void Main(string[] args)
        {
            int port = args.Length == 0 ? 1334 : Int32.Parse(args[0]);

            OpenChannel(port).Wait();
        }

        private async Task OpenChannel(int port)
        {
            var listenSocket = new Socket(SocketType.Stream, ProtocolType.Tcp);
            listenSocket.Bind(new IPEndPoint(IPAddress.Loopback, port));
            listenSocket.Listen(10);

            Console.WriteLine("Listening on port {0}", port);

            for (; ; )
            {
                var acceptSocket = await AcceptAsync(listenSocket);

                Console.WriteLine("Client accepted {0}", acceptSocket.LocalEndPoint);

                var connection = new ConnectionContext(new NetworkStream(acceptSocket));

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