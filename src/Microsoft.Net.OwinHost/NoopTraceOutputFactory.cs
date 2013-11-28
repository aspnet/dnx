using System.IO;
using Microsoft.Owin.Hosting.Tracing;

namespace Microsoft.Net.OwinHost
{
    public class NoopTraceOutputFactory : ITraceOutputFactory
    {
        public TextWriter Create(string outputFile)
        {
            return TextWriter.Null;
        }
    }
}
