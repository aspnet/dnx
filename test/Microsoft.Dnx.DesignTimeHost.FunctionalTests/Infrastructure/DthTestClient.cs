// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Microsoft.Dnx.DesignTimeHost.FunctionalTests.Infrastructure
{
    public class DthTestClient : IDisposable
    {
        private readonly int _contextId;
        private readonly string _hostId;
        private readonly BinaryReader _reader;
        private readonly BinaryWriter _writer;
        private readonly NetworkStream _networkStream;

        public DthTestClient(DthTestServer server, int contextId)
        {
            var socket = new Socket(AddressFamily.InterNetwork,
                                    SocketType.Stream,
                                    ProtocolType.Tcp);

            socket.Connect(new IPEndPoint(IPAddress.Loopback, server.Port));

            _hostId = server.HostId;
            _contextId = contextId;

            _networkStream = new NetworkStream(socket);
            _reader = new BinaryReader(_networkStream);
            _writer = new BinaryWriter(_networkStream);
        }

        public void SendPayLoad(string messageType, object payload)
        {
            SendMessage(new
            {
                ContextId = _contextId,
                HostId = _hostId,
                MessageType = messageType,
                Payload = payload
            });
        }

        public void Initialize(string projectPath)
        {
            SendPayLoad("Initialize", new { ProjectFolder = projectPath });
        }

        public DthMessage<T> GetResponse<T>()
        {
            return GetResponse<T>(TimeSpan.FromSeconds(5));
        }

        public DthMessage<T> GetResponse<T>(TimeSpan timeout)
        {
            var raw = string.Empty;

            Exception exception = null;
            var thread = new Thread(() =>
            {
                try
                {
                    raw = _reader.ReadString();
                }
                catch (Exception ex)
                {
                    exception = ex;
                }
            });
            thread.Start();

            if (thread.Join(timeout))
            {
                if (exception != null)
                {
                    throw new InvalidOperationException($"Unexpected exception during reading content from network stream.", exception);
                }
                else
                {
                    try
                    {
                        return JsonConvert.DeserializeObject<DthMessage<T>>(raw);
                    }
                    catch (Exception deserializException)
                    {
                        throw new InvalidOperationException(
                            $"Fail to deserailze data into {nameof(DthMessage<T>)}.\nContent: {raw}.\nException: {deserializException.Message}.",
                            deserializException);
                    }
                }
            }
            else
            {
                throw new InvalidOperationException($"Response time out after {timeout.TotalSeconds}s.");
            }
        }

        public void Dispose()
        {
            _reader.Close();
            _writer.Close();
            _networkStream.Close();
        }

        private void SendMessage(object message)
        {
            lock (_writer)
            {
                _writer.Write(JsonConvert.SerializeObject(message));
            }
        }
    }
}
