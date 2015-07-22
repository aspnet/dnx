using System.Threading;

namespace Microsoft.Dnx.Runtime
{
    /// <summary>
    /// Exposes methods that allow control over the application lifetime.
    /// </summary>
    public interface IApplicationShutdown
    {
        /// <summary>
        /// Requests termination the current application.
        /// </summary>
        void RequestShutdown();

        /// <summary>
        /// Gets a <see cref="CancellationToken"/> that is signaled when application shutdown is requested.
        /// </summary>
        CancellationToken ShutdownRequested { get; }
    }
}
