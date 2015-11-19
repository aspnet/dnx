using System;
using System.IO;
using System.Net.Security;
using System.Net.Sockets;
using System.Text;

namespace PublishRuntimeDependency
{
    public class Program
    {
        public void Main(string[] args)
        {
            if (args.Length < 1)
            {
                args = new[] { "https://www.microsoft.com" };
            }

            Console.WriteLine("=== Testing with raw Stream ===");
            try
            {
                TestStream(args[0]);
            }
            catch (Exception ex)
            {
                Console.WriteLine("FAILED:");
                Console.WriteLine(ex.ToString());
            }

            Console.WriteLine();
            Console.WriteLine("=== Testing with StreamReader ===");
            try
            {
                TestStreamReader(args[0]);
            }
            catch (Exception ex)
            {
                Console.WriteLine("FAILED:");
                Console.WriteLine(ex.ToString());
            }
        }

        public void TestStreamReader(string urlStr)
        {
            // Make a simple HTTP request
            var url = new Uri(urlStr);

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

        public void TestStream(string urlStr)
        {
            // Make a simple HTTP request
            var url = new Uri(urlStr);

            var socket = new Socket(SocketType.Stream, ProtocolType.Tcp);
            socket.Connect(url.Host, url.Port);

            using (var stream = GetStream(socket, url))
            {
                // Send the request
                var request = $"GET {url.PathAndQuery} HTTP/1.1\r\nHost: {url.Host}\r\nConnection: close\r\n\r\n";
                var buffer = Encoding.UTF8.GetBytes(request);
                stream.Write(buffer, 0, buffer.Length);

                // Read the response
                byte[] readbuffer = new byte[1024];
                int read = 0;
                Console.WriteLine("Response:");
                while ((read = stream.Read(readbuffer, 0, readbuffer.Length)) != 0)
                {
                    var converted = Encoding.UTF8.GetString(readbuffer, 0, read);
                    Console.Write(converted);
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
