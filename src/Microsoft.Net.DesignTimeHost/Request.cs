using Newtonsoft.Json.Linq;

namespace Microsoft.Net.Runtime.DesignTimeHost
{
    public class Request
    {
        public int Id { get; set; }

        public string Method { get; set; }

        public JToken[] Args { get; set; }
    }
}
