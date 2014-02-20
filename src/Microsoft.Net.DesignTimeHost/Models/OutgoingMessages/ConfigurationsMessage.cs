using System.Collections.Generic;

namespace Microsoft.Net.DesignTimeHost.Models.OutgoingMessages
{
    public class ConfigurationsMessage
    {
        public IList<ConfigurationData> Configurations { get; set; }
    }
}