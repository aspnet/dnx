// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Diagnostics;
using System.Threading;

namespace Microsoft.Dnx.Runtime
{
    public class DebuggerDetachWatcher
    {
        private Timer _debuggerEventsTimer;
        private Action _detachCallback;

        private readonly object _debuggerEventsSyncLock = new object();

        public DebuggerDetachWatcher(Action detachCallback)
        {
            _detachCallback = detachCallback;
        }

        public void ScheduleDetachCallback()
        {
            lock (_debuggerEventsSyncLock)
            {
                if (_debuggerEventsTimer == null)
                {
                    _debuggerEventsTimer = new Timer(CheckDebuggerDetached, state: null, dueTime: 0, period: 1000);
                }
            }
        }

        private void CheckDebuggerDetached(object state)
        {
            if (!Debugger.IsAttached)
            {
                // Stop the timer so we don't get anymore events
                _debuggerEventsTimer.Dispose();

                // Trigger the callback
                Interlocked.Exchange(ref _detachCallback, () => { }).Invoke();
            }
        }
    }
}