
namespace Microsoft.Net.Runtime
{
    public class UnresolvedMetadataReference : IMetadataReference
    {
        public UnresolvedMetadataReference(string name)
        {
            Name = name;
        }

        public string Name
        {
            get;
            private set;
        }

        public override string ToString()
        {
            return Name;
        }
    }
}
