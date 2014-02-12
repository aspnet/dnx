#if NET45 // NETWORKING
using System;
using System.Net;
using System.Net.Sockets;
using System.Runtime.Versioning;
using System.Threading.Tasks;
using Microsoft.Net.Runtime.DesignTimeHost;
using Microsoft.Net.Runtime.Services;

namespace Microsoft.Net.DesignTimeHost
{
    public class Program
    {
        private readonly IProjectMetadataProvider _projectMetadataProvider;
        private readonly IDependencyRefresher _refresher;
        private readonly IApplicationEnvironment _appEnvironment;

        public Program(IProjectMetadataProvider projectMetadataProvider,
                       IDependencyRefresher refresher,
                       IApplicationEnvironment appEnvironment)
        {
            _projectMetadataProvider = projectMetadataProvider;
            _refresher = refresher;
            _appEnvironment = appEnvironment;
        }

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

            while (true)
            {
                Console.WriteLine("Listening on port {0}", port);

                var client = await AcceptAsync(socket);

                Console.WriteLine("Client accepted {0}", client.LocalEndPoint);

                // TODO: Cleanup the channel when the client goes away
                var channel = new Channel(new NetworkStream(client));

                channel.Bind(this);
            }
        }

        private static Task<Socket> AcceptAsync(Socket socket)
        {
            return Task.Factory.FromAsync((cb, state) => socket.BeginAccept(cb, state), ar => socket.EndAccept(ar), null);
        }

        public IProjectMetadata GetProjectMetadata()
        {
            return _projectMetadataProvider.GetProjectMetadata(_appEnvironment.ApplicationName, _appEnvironment.TargetFramework);
        }

        public void RefreshDependencies()
        {
            _refresher.RefreshDependencies(_appEnvironment.ApplicationName, _appEnvironment.Version, _appEnvironment.TargetFramework);
        }
    }
}
#endif