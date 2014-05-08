using Microsoft.Framework.Runtime.Roslyn;
using Newtonsoft.Json.Linq;

namespace Microsoft.Framework.DesignTimeHost.Models.OutgoingMessages
{
    public class ConfigurationData
    {
        public string FrameworkName { get; set; }
        public string LongFrameworkName { get; set; }
        public CompilationSettings CompilationSettings { get; set; }
    }
}