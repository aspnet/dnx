using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Communication
{
    public class ProcessingQueue
    {
        private readonly List<Message> _queue = new List<Message>();
        private readonly BinaryReader _reader;
        private readonly BinaryWriter _writer;

        public event Action<Message> OnMessage;

        public ProcessingQueue(Stream stream)
        {
            _reader = new BinaryReader(stream);
            _writer = new BinaryWriter(stream);
        }

        public void Start()
        {
            new Thread(() => ReceiveMessages()).Start();
            new Thread(() => ProcessQueue()).Start();
        }

        public void Post(int messageType, object value = null)
        {
            _writer.Write(JsonConvert.SerializeObject(new Message
            {
                MessageType = messageType,
                Payload = value == null ? null : JToken.FromObject(value)
            }));
        }

        private void ProcessQueue()
        {
            while (true)
            {
                Message message = null;

                lock (_queue)
                {
                    Monitor.Wait(_queue);

                    // Look at the top of the processing queue for the message type
                    var type = _queue[0].MessageType;

                    // Remove all messages of this type and pick the last one to process
                    for (int i = _queue.Count - 1; i >= 0; i--)
                    {
                        if (type == _queue[i].MessageType)
                        {
                            if (message == null)
                            {
                                message = _queue[i];
                            }

                            _queue.RemoveAt(i);
                        }
                    }
                }

                var h = OnMessage;
                if (h != null)
                {
                    try
                    {
                        h(message);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine(ex.Message);
                    }
                }
            }
        }

        private void ReceiveMessages()
        {
            try
            {
                while (true)
                {
                    var message = JsonConvert.DeserializeObject<Message>(_reader.ReadString());

                    lock (_queue)
                    {
                        _queue.Add(message);
                        Monitor.Pulse(_queue);
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }
        }
    }

    public class Message
    {
        public int MessageType { get; set; }
        public int Priority { get; set; }
        public JToken Payload { get; set; }
    }
}
