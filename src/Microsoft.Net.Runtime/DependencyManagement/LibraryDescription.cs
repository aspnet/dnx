using System.Collections.Generic;

namespace Microsoft.Net.Runtime
{
    public class LibraryDescription
    {
        public Library Identity { get; set; }
        public IEnumerable<Library> Dependencies { get; set; }
    }
}
