using System.Collections.Generic;

namespace Microsoft.Framework.DesignTimeHost.Models.OutgoingMessages
{
    public class DiagnosticsMessage
    {
        public IList<string> Warnings { get; set; }
        public IList<string> Errors { get; set; }
    }
}