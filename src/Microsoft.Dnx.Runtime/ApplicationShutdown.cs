// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Diagnostics;
using System.Threading;

namespace Microsoft.Dnx.Runtime
{
    public class ApplicationShutdown : IApplicationShutdown
    {
        private readonly CancellationTokenSource _cts = new CancellationTokenSource();
        private readonly DebuggerDetachWatcher _debuggerDetachWatcher;
        private int _scheduledDetachCallback;

        public ApplicationShutdown()
        {
            ShutdownRequested = _cts.Token;

            _debuggerDetachWatcher = new DebuggerDetachWatcher(RequestShutdown);
        }

        public CancellationToken ShutdownRequested { get; private set; }

        public void RequestShutdownWaitForDebugger()
        {
            // If we already scheduled the detach callback then noop
            if (_scheduledDetachCallback == 1)
            {
                return;
            }

            // If cancellation is already requested then do nothing
            if (ShutdownRequested.IsCancellationRequested)
            {
                return;
            }

            if (Debugger.IsAttached)
            {
                // Schedule the callback after the debugger has been detached
                if (Interlocked.Exchange(ref _scheduledDetachCallback, 1) == 0)
                {
                    Logger.TraceInformation("[{0}]: Scheduling shutdown request for debugger detach.", GetType().Name);
                    _debuggerDetachWatcher.ScheduleDetachCallback();
                }
            }
            else
            {
                // Shut the process down
                RequestShutdown();
            }
        }

        public void RequestShutdown()
        {
            // Trigger the ShutdownRequested to tell the application to unwind
            Logger.TraceInformation("[{0}]: Requesting shutdown.", GetType().Name);
            _cts.Cancel();
            Logger.TraceInformation("[{0}]: Requested shutdown.", GetType().Name);
        }
    }
}
