namespace Microsoft.Extensions.CompilationAbstractions.Caching
{
    public interface ICacheContextAccessor
    {
        CacheContext Current { get; set; }
    }
}