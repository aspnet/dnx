using System;

namespace Microsoft.Framework.Runtime
{
    [AssemblyNeutral]
    public interface IAssemblyLoadContextFactory
    {
        IAssemblyLoadContext Create();
    }
}