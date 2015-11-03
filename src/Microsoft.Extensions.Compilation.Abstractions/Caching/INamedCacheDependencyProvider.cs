namespace Microsoft.Extensions.Compilation.Caching
{
    public interface INamedCacheDependencyProvider
    {
        ICacheDependency GetNamedDependency(string name);

        void Trigger(string name);
    }
}