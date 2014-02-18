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

        public event Action<List<Message>> OnMessage;

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

        public void Post(int contextId, int messageType, object value = null)
        {
            _writer.Write(JsonConvert.SerializeObject(new Message
            {
                ContextId = contextId,
                MessageType = messageType,
                Payload = value == null ? null : JToken.FromObject(value)
            }));
        }

        private void ProcessQueue()
        {
            var latest = new Dictionary<int, Message>();
            var seen = new HashSet<int>();

            while (true)
            {
                var messages = new List<Message>();

                lock (_queue)
                {
                    Monitor.Wait(_queue);

                    foreach (var m in _queue)
                    {
                        latest[m.MessageType] = m;
                    }

                    foreach (var m in _queue)
                    {
                        if (seen.Contains(m.MessageType))
                        {
                            continue;
                        }

                        seen.Add(m.MessageType);

                        messages.Add(latest[m.MessageType]);
                    }

                    latest.Clear();
                    seen.Clear();
                    _queue.Clear();
                }

                var h = OnMessage;
                if (h != null)
                {
                    try
                    {
                        h(messages);
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
        public int ContextId { get; set; }
        public JToken Payload { get; set; }

        public override string ToString()
        {
            return "(" + MessageType + ", " + ContextId + ") -> " + (Payload == null ? "null" : Payload.ToString(Formatting.Indented));
        }
    }
}
