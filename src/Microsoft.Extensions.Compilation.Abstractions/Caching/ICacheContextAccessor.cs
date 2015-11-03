namespace Microsoft.Extensions.Compilation.Caching
{
    public interface ICacheContextAccessor
    {
        CacheContext Current { get; set; }
    }
}