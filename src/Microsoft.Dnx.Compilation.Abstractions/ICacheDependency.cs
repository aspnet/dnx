namespace Microsoft.Dnx.Runtime.Caching
{
    public interface ICacheDependency
    {
        bool HasChanged { get; }
    }
}