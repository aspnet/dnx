namespace Microsoft.Dnx.Runtime.Caching
{
    public interface ICacheContextAccessor
    {
        CacheContext Current { get; set; }
    }
}