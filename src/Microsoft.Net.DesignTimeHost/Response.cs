using Newtonsoft.Json.Linq;

namespace Microsoft.Net.Runtime.DesignTimeHost
{
    public class Response
    {
        public int Id { get; set; }

        public JToken Result { get; set; }

        public string Error { get; set; }
    }
}
