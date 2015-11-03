namespace Microsoft.Extensions.Compilation.Caching
{
    public interface ICacheDependency
    {
        bool HasChanged { get; }
    }
}