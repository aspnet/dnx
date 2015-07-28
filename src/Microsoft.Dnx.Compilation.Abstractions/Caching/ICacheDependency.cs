namespace Microsoft.Dnx.Compilation.Caching
{
    public interface ICacheDependency
    {
        bool HasChanged { get; }
    }
}