using Microsoft.Extensions.CompilationAbstractions.Caching;

namespace Microsoft.Dnx.Compilation.CSharp.Tests
{
    internal class FakeNamedDependencyProvider : INamedCacheDependencyProvider
    {
        public ICacheDependency GetNamedDependency(string name)
        {
            return null;
        }

        public void Trigger(string name)
        {
        }
    }
}