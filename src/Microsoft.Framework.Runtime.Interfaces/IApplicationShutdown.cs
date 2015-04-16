using System.Threading;

namespace Microsoft.Framework.Runtime
{
    /// <summary>
    /// Exposes methods that allow control over the application lifetime
    /// </summary>
    public interface IApplicationShutdown
    {
        /// <summary>
        /// Terminates the current application.
        /// </summary>
        void RequestShutdown();

        /// <summary>
        /// Gets a <see cref="CancellationToken"/> that triggers when application shutdown is requested.
        /// </summary>
        CancellationToken ShutdownRequested { get; }
    }
}
