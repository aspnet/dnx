using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using Microsoft.Net.DesignTimeHost.Models;
using Newtonsoft.Json;

namespace Communication
{
    public class ProcessingQueue
    {
        private readonly List<Message> _queue = new List<Message>();
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
            new Thread(ReceiveMessages).Start();
        }

        public void Post(Message message)
        {
            _writer.Write(JsonConvert.SerializeObject(message));
        }

        private void ReceiveMessages()
        {
            try
            {
                while (true)
                {
                    OnReceive(JsonConvert.DeserializeObject<Message>(_reader.ReadString()));
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }
        }
    }
}
