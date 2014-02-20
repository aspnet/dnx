using Newtonsoft.Json.Linq;

namespace Microsoft.Net.DesignTimeHost.Models.OutgoingMessages
{
    public class ConfigurationData
    {
        public string FrameworkName { get; set; }
        public JToken CompilationOptions { get; set; }
    }
}