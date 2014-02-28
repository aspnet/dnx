using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Microsoft.Net.DesignTimeHost.Models
{
    public class Message
    {
        public string HostID {get;set;}
        public string MessageType { get; set; }
        public int ContextId { get; set; }
        public JToken Payload { get; set; }

        public override string ToString()
        {
            return "(" + HostID + ", " + MessageType + ", " + ContextId + ") -> " + (Payload == null ? "null" : Payload.ToString(Formatting.Indented));
        }
    }
}