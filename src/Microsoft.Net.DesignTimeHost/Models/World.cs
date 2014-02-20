using Microsoft.Net.DesignTimeHost.Models.OutgoingMessages;

namespace Microsoft.Net.DesignTimeHost.Models
{
    public class World
    {
        public ConfigurationsMessage Configurations { get; set; }
        public ReferencesMessage References { get; set; }
        public DiagnosticsMessage Diagnostics { get; set; }
    }
}
