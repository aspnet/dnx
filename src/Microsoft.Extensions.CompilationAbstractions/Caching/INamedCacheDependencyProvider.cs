namespace Microsoft.Extensions.CompilationAbstractions.Caching
{
    public interface INamedCacheDependencyProvider
    {
        ICacheDependency GetNamedDependency(string name);

        void Trigger(string name);
    }
}