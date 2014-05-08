using System;

namespace Microsoft.Framework.Runtime
{
    [AssemblyNeutral]
    public interface IFileMonitor
    {
        event Action<string> OnChanged;
    }
}
