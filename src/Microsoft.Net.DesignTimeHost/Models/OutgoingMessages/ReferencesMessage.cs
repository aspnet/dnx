using System.Collections.Generic;

namespace Microsoft.Net.DesignTimeHost.Models.OutgoingMessages
{
    public class ReferencesMessage
    {
        public IList<string> ProjectReferences { get; set; }
        public IList<string> FileReferences { get; set; }
        public IDictionary<string, byte[]> RawReferences { get; set; }
        public IDictionary<string, ReferenceDescription> Dependencies { get; set; }
    }
}