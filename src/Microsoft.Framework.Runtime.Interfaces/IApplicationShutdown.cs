using System.Threading;

namespace Microsoft.Framework.Runtime
{
    public interface IApplicationShutdown
    {
        void RequestShutdown();

        CancellationToken ShutdownRequested { get; }
    }
}
