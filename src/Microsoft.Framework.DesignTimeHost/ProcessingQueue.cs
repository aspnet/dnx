// Copyright (c) Microsoft Open Technologies, Inc. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading;
using Microsoft.Framework.DesignTimeHost.Models;
using Newtonsoft.Json;

namespace Communication
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
            Trace.TraceInformation("[ProcessingQueue]: Start()");
            new Thread(ReceiveMessages).Start();
        }

        public void WriteCustom(Action<BinaryWriter> write)
        {
            write(_writer);
        }

        public void Post(Message message)
        {
            lock (_writer)
            {
                Trace.TraceInformation("[ProcessingQueue]: Post({0})", message.MessageType);
                _writer.Write(JsonConvert.SerializeObject(message));
            }
        }

        private void ReceiveMessages()
        {
            try
            {
                while (true)
                {
                    var message = JsonConvert.DeserializeObject<Message>(_reader.ReadString());
                    Trace.TraceInformation("[ProcessingQueue]: OnReceive({0})", message);
                    OnReceive(message);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }
        }
    }
}
