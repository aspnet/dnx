// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Threading;

namespace Microsoft.Dnx.Runtime
{
    internal class DisposableAction : IDisposable
    {
        private Action _action;

        public DisposableAction(Action action)
        {
            _action = action;
        }

        public void Dispose()
        {
            Interlocked.Exchange(ref _action, () => { }).Invoke();
        }
    }
}