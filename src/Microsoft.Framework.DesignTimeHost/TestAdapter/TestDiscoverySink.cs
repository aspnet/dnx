using System.Diagnostics;
using Microsoft.Framework.DesignTimeHost.Models;
using Microsoft.Framework.TestAdapter;
using Newtonsoft.Json.Linq;

namespace Microsoft.Framework.DesignTimeHost
{
    public class TestDiscoverySink : ITestDiscoverySink
    {
        private readonly ApplicationContext _context;

        public TestDiscoverySink(ApplicationContext context)
        {
            _context = context;
        }

        public void SendTest(Test test)
        {
            Trace.TraceInformation("[TestDiscoverySink]: OnTransmit(TestDiscovery.TestFound)");
            _context.Send(new Message
            {
                ContextId = _context.Id,
                MessageType = "TestDiscovery.TestFound",
                Payload = JToken.FromObject(test),
            });
        }
    }
}