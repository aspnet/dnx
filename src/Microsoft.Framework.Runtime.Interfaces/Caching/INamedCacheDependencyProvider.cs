namespace Microsoft.Framework.Runtime.Caching
{
    public interface INamedCacheDependencyProvider
    {
        ICacheDependency GetNamedDependency(string name);

        void Trigger(string name);
    }
}