using System;
using System.Diagnostics;
using Microsoft.Framework.DesignTimeHost.Models;
using Microsoft.Framework.TestAdapter;
using Newtonsoft.Json.Linq;

namespace Microsoft.Framework.DesignTimeHost
{
    public class TestExecutionSink : ITestExecutionSink
    {
        private readonly ApplicationContext _context;

        public TestExecutionSink(ApplicationContext context)
        {
            _context = context;
        }

        public void RecordResult(TestResult testResult)
        {
            Trace.TraceInformation("[TestExecutionSink]: OnTransmit(TestExecution.TestResult)");
            _context.Send(new Message
            {
                ContextId = _context.Id,
                MessageType = "TestExecution.TestResult",
                Payload = JToken.FromObject(testResult),
            });
        }

        public void RecordStart(Test test)
        {
            Trace.TraceInformation("[TestExecutionSink]: OnTransmit(TestExecution.TestStarted)");
            _context.Send(new Message
            {
                ContextId = _context.Id,
                MessageType = "TestExecution.TestStarted",
                Payload = JToken.FromObject(test),
            });
        }
    }
}