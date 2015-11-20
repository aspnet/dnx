using System;
using System.Data.SqlClient;
using System.IO;
using System.Net.NetworkInformation;
using System.Net.Security;
using System.Net.Sockets;
using System.Net.WebSockets;
using System.Reflection;
using System.Text;
using System.Threading;

namespace PublishRuntimeDependency
{
    public class Program
    {
        public void Main(string[] args)
        {
            RunTest("System.Net.Security", TestSslStream);
            RunTest("System.Net.NetworkInformation", TestNetworkInformation);
            RunTest("System.Net.WebSockets.Client", TestWebSockets);
            RunTest("System.Data.SqlClient", TestSqlClient);
            RunTest("System.Reflection.DispatchProxy", TestDispatchProxy);
        }

        public interface ITestInterface
        {
            void DoTheThing();
        }
        public class TestProxy : DispatchProxy
        {
            public bool WasCalled { get; private set; }

            protected override object Invoke(MethodInfo targetMethod, object[] args)
            {
                WasCalled = true;
                Console.WriteLine($"Intercepted: {targetMethod.Name} with dispatch proxy");
                return null;
            }
        }

        private void TestDispatchProxy()
        {
            var proxy = DispatchProxy.Create<ITestInterface, TestProxy>();
            proxy.DoTheThing();

            if (!((TestProxy)proxy).WasCalled)
            {
                throw new Exception("The proxy was not called!");
            }
            Console.WriteLine("The proxy was called");
        }

        private void TestSqlClient()
        {
            // Don't want to take a dependency on Sql Server so we just force loading by constructing a type
            var con = new SqlConnection();
            Console.WriteLine("Successfully loaded System.Data.SqlClient");
        }

        private void RunTest(string name, Action act)
        {
            Console.WriteLine();
            Console.WriteLine($"=== Testing {name} ===");
            try
            {
                act();
            }
            catch (Exception ex)
            {
                Console.WriteLine("FAILED:");
                Console.WriteLine(ex.ToString());
            }
        }

        private void TestWebSockets()
        {
            var socket = new ClientWebSocket();
            Console.WriteLine("Connecting");
            socket.ConnectAsync(new Uri("wss://echo.websocket.org"), CancellationToken.None).Wait();

            Console.WriteLine("Sending");
            socket.SendAsync(new ArraySegment<byte>(Encoding.UTF8.GetBytes("Hello")), WebSocketMessageType.Text, true, CancellationToken.None).Wait();

            var buffer = new byte[1024];
            Console.WriteLine("Receiving");
            var result = socket.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None).Result;

            Console.WriteLine($"Recieved: {Encoding.UTF8.GetString(buffer, 0, result.Count)}");
        }

        private void TestNetworkInformation()
        {
            foreach (var iface in NetworkInterface.GetAllNetworkInterfaces())
            {
                Console.WriteLine($"Network Interface: {iface.Id} {iface.Name} ({iface.NetworkInterfaceType})");
            }
        }

        public void TestSslStream()
        {
            // Make a simple HTTP request
            var url = new Uri("https://www.microsoft.com");

            var socket = new Socket(SocketType.Stream, ProtocolType.Tcp);
            socket.Connect(url.Host, url.Port);

            using (var stream = GetStream(socket, url))
            {
                // Send the request
                var request = $"GET {url.PathAndQuery} HTTP/1.1\r\nHost: {url.Host}\r\nConnection: close\r\n\r\n";
                var buffer = Encoding.UTF8.GetBytes(request);
                stream.Write(buffer, 0, buffer.Length);

                // Read the response
                using (var reader = new StreamReader(stream, Encoding.UTF8))
                {
                    Console.WriteLine(reader.ReadToEnd());
                }
            }
        }

        private static Stream GetStream(Socket socket, Uri uri)
        {
            var stream = new NetworkStream(socket);
            if (uri.Scheme.Equals("https"))
            {
                var ssl = new SslStream(stream);
                ssl.AuthenticateAsClient(uri.Host);
                return ssl;
            }
            return stream;
        }
    }
}
