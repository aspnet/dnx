using System.Runtime.Versioning;

namespace Microsoft.Framework.Runtime
{
    public interface IFrameworkReferenceResolver
    {
        bool TryGetAssembly(string name, FrameworkName frameworkName, out string path);
    }
}