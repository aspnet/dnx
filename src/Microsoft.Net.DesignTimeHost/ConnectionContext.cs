using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using Communication;

namespace Microsoft.Net.DesignTimeHost
{
    public class ConnectionContext
    {
        private readonly Socket _socket;
        private readonly IDictionary<int, ApplicationContext> _contexts = new Dictionary<int, ApplicationContext>();
        private Stream _stream;
        private ProcessingQueue _queue;

        public ConnectionContext(Socket socket)
        {
            _socket = socket;
        }

        public void Start()
        {
            _stream = new NetworkStream(_socket);
            _queue = new ProcessingQueue(_stream);
            _queue.OnMessage += OnMessage;
            _queue.Start();
        }

        public void OnMessage(Message message)
        {
            ApplicationContext applicationContext;
            if (!_contexts.TryGetValue(message.ContextId, out applicationContext))
            {
                applicationContext = new ApplicationContext(message.ContextId);
                _contexts.Add(message.ContextId, applicationContext);
            }
            applicationContext.OnMessage(message);
        }
    }
}
