using System;

namespace Microsoft.Framework.Runtime
{
    public interface IAssemblyLoadContextFactory
    {
        IAssemblyLoadContext Create();
    }
}