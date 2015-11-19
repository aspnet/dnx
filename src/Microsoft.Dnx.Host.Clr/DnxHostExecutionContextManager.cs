// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Threading;

namespace Microsoft.Dnx.Host.Clr
{
    internal class DnxHostExecutionContextManager : HostExecutionContextManager
    {
        private delegate void RevertAction();

        public override HostExecutionContext Capture()
        {
            var threadContext = new ThreadContext(Thread.CurrentThread);

            return new DnxHostExecutionContext(base.Capture(), threadContext);
        }

        public override object SetHostExecutionContext(HostExecutionContext hostExecutionContext)
        {
            var castHostExecutionContext = hostExecutionContext as DnxHostExecutionContext;
            if (castHostExecutionContext != null)
            {
                object baseRevertParameter = null;
                if (castHostExecutionContext.BaseContext != null)
                {
                    baseRevertParameter = base.SetHostExecutionContext(castHostExecutionContext.BaseContext);
                }

                // Capture the current status of the thread context and then update the values
                // The captured snapshot will be used to revert the status later
                var originalThreadContext = castHostExecutionContext.ThreadContext.CaptureAndReplace(Thread.CurrentThread);

                return (RevertAction)(() =>
                {
                    originalThreadContext.CaptureAndReplace(Thread.CurrentThread);
                    if (baseRevertParameter != null)
                    {
                        base.Revert(baseRevertParameter);
                    }
                });
            }
            else
            {
                return base.SetHostExecutionContext(hostExecutionContext);
            }
        }

        public override void Revert(object previousState)
        {
            var revertAction = previousState as RevertAction;
            if (revertAction != null)
            {
                revertAction();
            }
            else
            {
                base.Revert(previousState);
            }
        }

        private class DnxHostExecutionContext : HostExecutionContext
        {
            internal DnxHostExecutionContext(HostExecutionContext baseContext, ThreadContext threadContext)
            {
                BaseContext = baseContext;
                ThreadContext = threadContext;
            }

            private DnxHostExecutionContext(DnxHostExecutionContext original)
                : this(CreateCopyHelper(original.BaseContext), original.ThreadContext)
            {
            }

            public HostExecutionContext BaseContext { get; private set; }

            public ThreadContext ThreadContext { get; private set; }

            public override HostExecutionContext CreateCopy()
            {
                return new DnxHostExecutionContext(this);
            }

            private static HostExecutionContext CreateCopyHelper(HostExecutionContext hostExecutionContext)
            {
                return (hostExecutionContext != null) ? hostExecutionContext.CreateCopy() : null;
            }

            public override void Dispose(bool disposing)
            {
                if (disposing && BaseContext != null)
                {
                    BaseContext.Dispose();
                }
            }
        }
    }
}