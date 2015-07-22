// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.IO;
using System.Threading;
using Microsoft.Dnx.DesignTimeHost.Models;
using Microsoft.Dnx.Runtime;
using Newtonsoft.Json;

namespace Microsoft.Dnx.DesignTimeHost
{
    public class ProcessingQueue
    {
        private readonly BinaryReader _reader;
        private readonly BinaryWriter _writer;

        public event Action<Message> OnReceive;

        public ProcessingQueue(Stream stream)
        {
            _reader = new BinaryReader(stream);
            _writer = new BinaryWriter(stream);
        }

        public void Start()
        {
            Logger.TraceInformation("[ProcessingQueue]: Start()");
            new Thread(ReceiveMessages).Start();
        }

        public bool Send(Action<BinaryWriter> write)
        {
            try
            {
                lock (_writer)
                {
                    write(_writer);
                    return true;
                }
            }
            catch (IOException)
            {
                // Swallow those
            }
            catch (Exception ex)
            {
                Logger.TraceInformation("[ProcessingQueue]: Error sending {0}", ex);
                throw;
            }

            return false;
        }

        public bool Send(Message message)
        {
            lock (_writer)
            {
                try
                {
                    Logger.TraceInformation("[ProcessingQueue]: Send({0})", message);
                    _writer.Write(JsonConvert.SerializeObject(message));

                    return true;
                }
                catch (IOException)
                {
                    // Swallow those
                }
                catch (Exception ex)
                {
                    Logger.TraceInformation("[ProcessingQueue]: Error sending {0}", ex);
                    throw;
                }
            }

            return false;
        }

        private void ReceiveMessages()
        {
            try
            {
                while (true)
                {
                    var message = JsonConvert.DeserializeObject<Message>(_reader.ReadString());
                    Logger.TraceInformation("[ProcessingQueue]: OnReceive({0})", message);
                    OnReceive(message);
                }
            }
            catch (IOException)
            {
                // Swallow those
            }
            catch (Exception ex)
            {
                Logger.TraceInformation("[ProcessingQueue]: Error occurred: {0}", ex);
            }
        }
    }
}
