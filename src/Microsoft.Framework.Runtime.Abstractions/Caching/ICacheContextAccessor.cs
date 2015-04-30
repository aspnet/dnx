namespace Microsoft.Framework.Runtime.Caching
{
    public interface ICacheContextAccessor
    {
        CacheContext Current { get; set; }
    }
}