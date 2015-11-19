namespace Microsoft.Extensions.CompilationAbstractions.Caching
{
    public interface ICacheDependency
    {
        bool HasChanged { get; }
    }
}