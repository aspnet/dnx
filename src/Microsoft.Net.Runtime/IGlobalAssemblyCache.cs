namespace Microsoft.Net.Runtime
{
    public interface IGlobalAssemblyCache
    {
        bool TryResolvePartialName(string name, out string assemblyLocation);

        bool Contains(string name);

        bool IsInGac(string path);
    }
}
