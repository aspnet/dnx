using System;

namespace Microsoft.Net.Runtime.Services
{
    [AssemblyNeutral]
    public interface IFileMonitor
    {
        event Action<string> OnChanged;
    }
}
