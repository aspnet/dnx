using Microsoft.Framework.DesignTimeHost.Models.OutgoingMessages;

namespace Microsoft.Framework.DesignTimeHost.Models
{
    public class World
    {
        public ConfigurationsMessage Configurations { get; set; }
        public ReferencesMessage References { get; set; }
        public DiagnosticsMessage Diagnostics { get; set; }
        public SourcesMessage Sources { get; set; }
    }
}
