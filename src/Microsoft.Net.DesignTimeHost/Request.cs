using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Microsoft.Net.Runtime.DesignTimeHost
{
    public class Request
    {
        public int Id { get; set; }

        [JsonProperty(NullValueHandling = NullValueHandling.Ignore, PropertyName = "M")]
        public string Method { get; set; }

        [JsonProperty(NullValueHandling = NullValueHandling.Ignore, PropertyName = "A")]
        public JToken[] Args { get; set; }
    }
}
