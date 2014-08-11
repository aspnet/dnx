using System;

namespace Microsoft.Framework.Runtime
{
    public interface ICacheDependency
    {
        bool HasChanged { get; }
    }
}