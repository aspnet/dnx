namespace Microsoft.Framework.Runtime.Caching
{
    public interface ICacheDependency
    {
        bool HasChanged { get; }
    }
}