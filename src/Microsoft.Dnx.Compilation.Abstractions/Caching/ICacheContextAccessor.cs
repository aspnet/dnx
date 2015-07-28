namespace Microsoft.Dnx.Compilation.Caching
{
    public interface ICacheContextAccessor
    {
        CacheContext Current { get; set; }
    }
}