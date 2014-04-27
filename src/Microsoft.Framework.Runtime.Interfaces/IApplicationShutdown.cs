using System.Threading;

namespace Microsoft.Framework.Runtime
{
    [AssemblyNeutral]
    public interface IApplicationShutdown
    {
        void RequestShutdown();

        CancellationToken ShutdownRequested { get; }
    }
}
