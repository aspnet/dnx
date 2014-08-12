using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Microsoft.Framework.Runtime
{
    internal class DesignTimeMessage
    {
        public string HostId { get; set; }
        public string MessageType { get; set; }
        public int ContextId { get; set; }
        public JToken Payload { get; set; }

        public override string ToString()
        {
            return "(" + HostId + ", " + MessageType + ", " + ContextId + ") -> " + (Payload == null ? "null" : Payload.ToString(Formatting.Indented));
        }
    }
}