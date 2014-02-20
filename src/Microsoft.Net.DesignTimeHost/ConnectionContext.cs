using System.Collections.Generic;
using System.IO;
using Communication;
using Microsoft.Net.DesignTimeHost.Models;

namespace Microsoft.Net.DesignTimeHost
{
    public class ConnectionContext
    {
        private readonly IDictionary<int, ApplicationContext> _contexts = new Dictionary<int, ApplicationContext>();
        private readonly Stream _stream;
        private ProcessingQueue _queue;

        public ConnectionContext(Stream stream)
        {
            _stream = stream;
        }

        public void Start()
        {
            _queue = new ProcessingQueue(_stream);
            _queue.OnReceive += OnReceive;
            _queue.Start();
        }

        public void OnReceive(Message message)
        {
            ApplicationContext applicationContext;
            if (!_contexts.TryGetValue(message.ContextId, out applicationContext))
            {
                applicationContext = new ApplicationContext(message.ContextId);
                applicationContext.OnTransmit += OnTransmit;
                _contexts.Add(message.ContextId, applicationContext);
            }
            applicationContext.OnReceive(message);
        }

        public void OnTransmit(Message message)
        {
            _queue.Post(message);
        }
    }
}
